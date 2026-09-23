// COPYRIGHT 2013 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Native;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;

using Orts.ActivityRunner.Processes;
using Orts.ActivityRunner.Viewer3D;
using Orts.ActivityRunner.Viewer3D.Debugging;

[assembly: CLSCompliant(false)]

namespace Orts.ActivityRunner
{
    internal static class Program
    {
        private static readonly char[] optionSeparators = new[] { '=', ':' };

        public static Viewer Viewer;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <remarks>
        /// Deliberately not async. An await that yields - reading a settings file does - carries the
        /// rest of the method over to a thread-pool thread, and the rest of this method is the whole
        /// game: the window, the OpenGL context and the render loop would all live on a borrowed
        /// pool thread instead of the process's main thread, which is where SDL expects its video
        /// calls and where every native game runs its graphics driver. Waiting here keeps them on
        /// the main thread; a console application has no synchronization context to deadlock on.
        /// </remarks>
        private static void Main(string[] args)
        {
            List<string> argumentList = args.ToList();
            string profileName = ParseCommandLineOption(argumentList, "Profile");

            ProfileModel profile = (string.IsNullOrEmpty(profileName) ?
                ((ProfileModel)null).Current(CancellationToken.None) :
                ((ProfileModel)null).Get(profileName, CancellationToken.None)).GetAwaiter().GetResult();

            ProfileUserSettingsModel userSettings = profile.LoadSettingsModel<ProfileUserSettingsModel>(CancellationToken.None).GetAwaiter().GetResult();
            userSettings.MultiPlayer = !string.IsNullOrEmpty(ParseCommandLineOption(argumentList, "MultiplayerClient"));
            ApplySafeMode(userSettings);

            StartupTrail.Begin();

            // Windows ships a 32 and a 64 bit soft_oal.dll under the same name, so the right
            // folder has to be added to the search path first. On Linux the loader finds the
            // distribution's libopenal.so.1 through the resolver in OpenAL.Unix.cs instead.
            string path = Path.Combine(RuntimeInfo.ApplicationFolder, "Native", (Environment.Is64BitProcess) ? "x64" : "x86");
            NativeMethods.SetDllDirectory(path);

            StartupTrail.Mark(StartupStage.OpeningWindow);
            using (GameHost game = new GameHost(userSettings))
            {
#pragma warning disable CA2000 // Dispose objects before losing scope
                game.PushState(new GameStateRunActivity(argumentList.ToArray()));
#pragma warning restore CA2000 // Dispose objects before losing scope
                game.Run();
            }
        }

        /// <summary>
        /// Turns off, for this run only, whatever the launcher asked to leave out after a crash.
        /// </summary>
        /// <remarks>
        /// Only the copy in memory changes: the settings saved on exit are the ones listed in
        /// UpdateRuntimeUserSettingsModel, and none of these is among them.
        /// </remarks>
        private static void ApplySafeMode(ProfileUserSettingsModel userSettings)
        {
            if (LaunchContext.NoSound)
                userSettings.SoundDetailLevel = 0;

            if (LaunchContext.BasicGraphics)
            {
                userSettings.ScreenMode = ScreenMode.Windowed;
                userSettings.MultiSamplingCount = 0;
                userSettings.DynamicShadows = false;
                userSettings.ModelInstancing = false;
            }
        }

        private static string ParseCommandLineOption(List<string> arguments, string argumentName)
        {

            string argumentValue = arguments.Where(a => a.StartsWith($"-{argumentName}", StringComparison.OrdinalIgnoreCase) || 
            a.StartsWith($"/{argumentName}", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            if (!string.IsNullOrEmpty(argumentValue))
            {
                arguments.RemoveAll(a => a.StartsWith($"-{argumentName}", StringComparison.OrdinalIgnoreCase) || 
                a.StartsWith($"/{argumentName}", StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(argumentValue))
            {
                string[] kvp = argumentValue.Split(optionSeparators, 2);

                string v = kvp.Length > 1 ? kvp[1] : "yes";
                return v;
            }
            return argumentValue;
        }
    }
}
