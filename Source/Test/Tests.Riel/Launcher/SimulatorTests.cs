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
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Riel.Launcher;

namespace Tests.Riel.Launcher
{
    [TestClass]
    [DoNotParallelize]
    public class SimulatorTests
    {
        [TestMethod]
        public void AConfiguredRelativeSimulatorPathIsResolvedBeforeChangingDirectory()
        {
            string executable = Path.GetTempFileName();
            string previous = Environment.GetEnvironmentVariable("RIEL_SIMULATOR");
            try
            {
                string relative = Path.GetRelativePath(Environment.CurrentDirectory, executable);
                Environment.SetEnvironmentVariable("RIEL_SIMULATOR", relative);
                Assert.AreEqual(Path.GetFullPath(executable), Simulator.Locate());
            }
            finally
            {
                Environment.SetEnvironmentVariable("RIEL_SIMULATOR", previous);
                File.Delete(executable);
            }
        }

        [TestMethod]
        public void TheReproductionCommandHandlesSpacesAndQuotesInExecutableAndArguments()
        {
            if (!OperatingSystem.IsLinux())
            {
                Assert.Inconclusive("The reproduction command uses the Linux shell.");
                return;
            }

            string directory = Path.Combine(Path.GetTempPath(), $"riel shell's test {Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            string executable = Path.Combine(directory, "runner's script");
            const string argument = "Train Simulator's route";
            try
            {
                File.WriteAllText(executable, "#!/bin/sh\nprintf '%s\\n' \"$1\" >&2\n");
                File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

                using SimulatorRun run = new SimulatorRun(executable, new[] { argument }, true, SafeMode.None);
                SimulatorOutcome outcome = run.Wait();
                Assert.AreEqual(0, outcome.ExitCode);
                Assert.AreEqual(argument, outcome.StandardError);

                using SimulatorRun reproduced = new SimulatorRun("/bin/sh", new[] { "-c", run.CommandLine }, true, SafeMode.None);
                SimulatorOutcome reproducedOutcome = reproduced.Wait();
                Assert.AreEqual(0, reproducedOutcome.ExitCode);
                Assert.AreEqual(argument, reproducedOutcome.StandardError);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
