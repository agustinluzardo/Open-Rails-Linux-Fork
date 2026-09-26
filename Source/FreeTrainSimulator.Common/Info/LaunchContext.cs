// COPYRIGHT 2026 by the Riel project.
//
// This file is part of Riel, a fork of Open Rails.
//
// Riel is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Riel is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Riel.  If not, see <http://www.gnu.org/licenses/>.

using System;

namespace FreeTrainSimulator.Common.Info
{
    /// <summary>
    /// What started the simulator, where that changes how it should behave.
    /// </summary>
    public static class LaunchContext
    {
        /// <summary>
        /// Set by a launcher that watches the simulator and shows its failures itself.
        /// </summary>
        public const string LauncherReportsErrorsVariable = "RIEL_LAUNCHER_REPORTS_ERRORS";

        /// <summary>
        /// Whether the launcher that started this run will explain a failure itself, in which
        /// case the simulator's own error dialog would only be a second box to click through
        /// before the better one appears. The failure is still logged either way; the log is
        /// what the launcher reads.
        /// </summary>
        public static bool LauncherReportsErrors { get; } = IsSet(LauncherReportsErrorsVariable);

        /// <summary>
        /// Set to run without sound, whatever the settings say.
        /// </summary>
        public const string NoSoundVariable = "RIEL_NO_SOUND";

        /// <summary>
        /// Whether this run leaves the sound system alone: no audio device is opened at all. The
        /// launcher offers it after the simulator crashes in native code, to tell a sound driver
        /// problem apart from a graphics one - and to get a train moving meanwhile.
        /// </summary>
        public static bool NoSound { get; } = IsSet(NoSoundVariable);

        /// <summary>
        /// Set to run with the simplest graphics setup the simulator has.
        /// </summary>
        public const string BasicGraphicsVariable = "RIEL_BASIC_GRAPHICS";

        /// <summary>
        /// Whether this run avoids the parts of the graphics setup that depend most on the driver:
        /// a window rather than full screen, no antialiasing, no dynamic shadows and no hardware
        /// instancing. Only this run changes; the saved settings stay as they are.
        /// </summary>
        public static bool BasicGraphics { get; } = IsSet(BasicGraphicsVariable);

        private static bool IsSet(string variable) =>
            string.Equals(Environment.GetEnvironmentVariable(variable), "1", StringComparison.Ordinal);
    }
}
