// COPYRIGHT 2026 by the Open Rails Linux Fork project.
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
using System.Diagnostics;
using System.IO;

namespace FreeTrainSimulator.Launcher
{
    /// <summary>Starts the simulator and waits for it.</summary>
    internal static class Simulator
    {
        private const string ExecutableName = "ActivityRunner";

        /// <summary>
        /// Runs the simulator with <paramref name="arguments"/> and returns its exit code.
        /// </summary>
        internal static int Start(string[] arguments)
        {
            string executable = Locate();

            ProcessStartInfo startInfo = new ProcessStartInfo(executable) { UseShellExecute = false };
            foreach (string argument in arguments)
                startInfo.ArgumentList.Add(argument);

            // Useful when a run fails: the same line can be pasted into a shell or a debugger.
            Console.Error.WriteLine($"Starting {executable} {string.Join(' ', arguments)}");

            try
            {
                using Process process = Process.Start(startInfo)
                    ?? throw new LauncherException($"could not start {executable}");
                process.WaitForExit();
                return process.ExitCode;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new LauncherException($"could not start {executable}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Finds the simulator: next to the launcher, where both the build output and the
        /// installed package put it, or wherever FTS_ACTIVITYRUNNER says.
        /// </summary>
        private static string Locate()
        {
            string configured = Environment.GetEnvironmentVariable("FTS_ACTIVITYRUNNER");
            if (!string.IsNullOrEmpty(configured))
            {
                return File.Exists(configured)
                    ? configured
                    : throw new LauncherException($"FTS_ACTIVITYRUNNER points at '{configured}', which does not exist");
            }

            string beside = Path.Combine(AppContext.BaseDirectory, ExecutableName);
            if (File.Exists(beside))
                return beside;

            throw new LauncherException(
                $"{ExecutableName} was not found next to the launcher ({AppContext.BaseDirectory}). " +
                "Set FTS_ACTIVITYRUNNER to its path.");
        }
    }
}
