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

using FreeTrainSimulator.Common.Info;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Riel.Launcher;

namespace Tests.FreeTrainSimulator.Launcher
{
    /// <summary>
    /// The one sentence the launcher shows when the simulator fails has to be the actual reason,
    /// taken from the exceptions exactly as the log and standard error print them.
    /// </summary>
    [TestClass]
    public class SimulatorOutcomeTests
    {
        private const string LoggedMissingFile =
            "Error: FreeTrainSimulator.Common.FatalException: A fatal error has occurred\n" +
            " ---> System.IO.FileNotFoundException: Could not find file '/mnt/datos/MSTS/GLOBAL/SHAPES/Bridge1.s'.\n" +
            "   at Orts.Formats.Msts.Files.ShapeFile..ctor(String fileName)\n" +
            "   --- End of inner exception stack trace ---";

        [TestMethod]
        public void TheFileThatIsMissingIsTheCause()
        {
            (string type, string message) = SimulatorOutcome.FindCause(LoggedMissingFile, null);
            Assert.AreEqual("FileNotFoundException", type);
            Assert.AreEqual("Could not find file '/mnt/datos/MSTS/GLOBAL/SHAPES/Bridge1.s'.", message);
        }

        [TestMethod]
        public void TheInnermostOfSeveralWrappersWins()
        {
            string nested =
                "Error: FreeTrainSimulator.Common.FatalException: A fatal error has occurred\n" +
                " ---> System.IO.FileLoadException: Could not load the route.\n" +
                " ---> System.IO.DirectoryNotFoundException: Could not find a part of the path '/x/Global/tsection.dat'.";
            Assert.AreEqual("DirectoryNotFoundException", SimulatorOutcome.FindCause(nested, null).Type);
        }

        /// <summary>
        /// A graphics device that cannot be created ends in a null reference inside the driver
        /// binding; the exception around it is the one that says what failed.
        /// </summary>
        [TestMethod]
        public void ANullReferenceIsPassedOverForTheExceptionAroundIt()
        {
            string graphics =
                "Unhandled exception. Microsoft.Xna.Framework.Graphics.NoSuitableGraphicsDeviceException: Failed to create graphics device!\n" +
                " ---> System.NullReferenceException: Object reference not set to an instance of an object.\n" +
                "   at MonoGame.OpenGL.GL.GetString(StringName name)";
            (string type, string message) = SimulatorOutcome.FindCause(null, graphics);
            Assert.AreEqual("NoSuitableGraphicsDeviceException", type);
            Assert.AreEqual("Failed to create graphics device!", message);
        }

        [TestMethod]
        public void ANullReferenceAloneIsStillReported()
        {
            string alone = "Error: FreeTrainSimulator.Common.FatalException: A fatal error has occurred\n" +
                " ---> System.NullReferenceException: Object reference not set to an instance of an object.";
            Assert.AreEqual("NullReferenceException", SimulatorOutcome.FindCause(alone, null).Type);
        }

        [TestMethod]
        public void TheLogIsPreferredToStandardError()
        {
            Assert.AreEqual("FileNotFoundException",
                SimulatorOutcome.FindCause(LoggedMissingFile, "Unhandled exception. System.InvalidOperationException: something else").Type);
        }

        [TestMethod]
        public void StandardErrorIsUsedWhenThereIsNoLog()
        {
            Assert.AreEqual("InvalidOperationException",
                SimulatorOutcome.FindCause(null, "Unhandled exception. System.InvalidOperationException: something else").Type);
        }

        /// <summary>A message can contain ": " itself; only the first one separates the type.</summary>
        [TestMethod]
        public void AColonInTheMessageIsKept()
        {
            string text = " ---> System.IO.IOException: Read failed: disk /dev/sdb1 went away";
            Assert.AreEqual("Read failed: disk /dev/sdb1 went away", SimulatorOutcome.FindCause(text, null).Message);
        }

        [TestMethod]
        public void NoReasonGivenMeansNoCause()
        {
            Assert.AreEqual((null, null), SimulatorOutcome.FindCause(null, "ALSA lib pcm.c:2721: Unknown PCM default"));
            Assert.AreEqual((null, null), SimulatorOutcome.FindCause(null, null));
        }

        // ------------------------------------------------------------------ native crashes

        private static StartupStep Step(StartupStage stage) => new StartupStep(stage, TimeSpan.Zero, null);

        [TestMethod]
        public void ASegmentationFaultIsANativeCrash()
        {
            SimulatorOutcome outcome = new SimulatorOutcome(139, null, null, null);
            Assert.AreEqual(11, outcome.Signal);
            Assert.AreEqual("SIGSEGV", outcome.SignalName);
            Assert.IsTrue(outcome.CrashedNatively);
            Assert.IsTrue(outcome.Failed);
        }

        /// <summary>exit(-1) is 255: a simulator that reported its own error, not a signal.</summary>
        [TestMethod]
        public void AnExitCodeIsNotASignal()
        {
            SimulatorOutcome outcome = new SimulatorOutcome(255, null, null, null);
            Assert.IsNull(outcome.Signal);
            Assert.IsNull(outcome.SignalName);
            Assert.IsFalse(outcome.CrashedNatively);
        }

        /// <summary>
        /// An unhandled exception also ends in SIGABRT, but it printed itself on the way out, and
        /// then it is the exception that explains the failure.
        /// </summary>
        [TestMethod]
        public void AnAbortOverAnExceptionIsNotANativeCrash()
        {
            string stderr = "Unhandled exception. System.InvalidOperationException: Operation not called on UI thread.";
            SimulatorOutcome outcome = new SimulatorOutcome(134, null, null, stderr);
            Assert.AreEqual("SIGABRT", outcome.SignalName);
            Assert.IsFalse(outcome.CrashedNatively);
            Assert.AreEqual("InvalidOperationException", outcome.CauseType);
        }

        [TestMethod]
        public void BeingKilledIsNotACrash()
        {
            Assert.AreEqual("SIGKILL", new SimulatorOutcome(137, null, null, null).SignalName);
            Assert.IsFalse(new SimulatorOutcome(137, null, null, null).CrashedNatively);
        }

        [TestMethod]
        public void TheLastStepOfTheMainSequenceIsTheStage()
        {
            Assert.AreEqual(StartupStage.CreatingGraphicsDevice, SimulatorOutcome.StageAtExit(new[]
            {
                Step(StartupStage.Started), Step(StartupStage.OpeningWindow), Step(StartupStage.CreatingGraphicsDevice),
            }));
            Assert.IsNull(SimulatorOutcome.StageAtExit(Array.Empty<StartupStep>()));
        }

        /// <summary>The window holds everything else up, so until it has shown a frame it is the suspect.</summary>
        [TestMethod]
        public void AWindowThatNeverShowedIsBlamedFirst()
        {
            Assert.AreEqual(StartupStage.GraphicsDeviceReady, SimulatorOutcome.StageAtExit(new[]
            {
                Step(StartupStage.GraphicsDeviceReady), Step(StartupStage.StartingSound), Step(StartupStage.Loading),
            }));
        }

        [TestMethod]
        public void ASoundDeviceStillOpeningIsBlamedBeforeLoading()
        {
            Assert.AreEqual(StartupStage.StartingSound, SimulatorOutcome.StageAtExit(new[]
            {
                Step(StartupStage.GraphicsDeviceReady), Step(StartupStage.StartingSound), Step(StartupStage.Loading), Step(StartupStage.FirstFrame),
            }));
            Assert.AreEqual(StartupStage.Loading, SimulatorOutcome.StageAtExit(new[]
            {
                Step(StartupStage.GraphicsDeviceReady), Step(StartupStage.StartingSound), Step(StartupStage.Loading), Step(StartupStage.FirstFrame), Step(StartupStage.SoundReady),
            }));
        }

        [TestMethod]
        public void TheCrashingLibraryNamesTheSuspect()
        {
            Assert.AreEqual(CrashSuspect.Graphics, SimulatorOutcome.FindSuspect(Crash("libnvidia-glcore.so.550.54.14"), StartupStage.StartingSound),
                "the library outranks the step");
            Assert.AreEqual(CrashSuspect.Graphics, SimulatorOutcome.FindSuspect(Crash("radeonsi_dri.so"), null));
            Assert.AreEqual(CrashSuspect.Sound, SimulatorOutcome.FindSuspect(Crash("libpipewire-0.3.so.0"), null));
            Assert.AreEqual(CrashSuspect.Sound, SimulatorOutcome.FindSuspect(Crash("libopenal.so"), null));
            Assert.AreEqual(CrashSuspect.Window, SimulatorOutcome.FindSuspect(Crash("libSDL2-2.0.so.0"), null));
        }

        [TestMethod]
        public void WithoutALibraryTheCallerOrTheStepDecides()
        {
            Assert.AreEqual(CrashSuspect.Sound, SimulatorOutcome.FindSuspect(Crash("libc.so.6", "Orts.ActivityRunner.Viewer3D.OpenAL.Initialize()"), null));
            Assert.AreEqual(CrashSuspect.Graphics, SimulatorOutcome.FindSuspect(Crash("libc.so.6", "Microsoft.Xna.Framework.Graphics.GraphicsDevice.Present()"), null));
            Assert.AreEqual(CrashSuspect.Window, SimulatorOutcome.FindSuspect(null, StartupStage.OpeningWindow));
            Assert.AreEqual(CrashSuspect.Unknown, SimulatorOutcome.FindSuspect(null, StartupStage.Loading));
        }

        private static CrashReport Crash(string module, string caller = null)
        {
            string callerFrame = caller == null ? string.Empty : $", {{ \"is_managed\": \"true\", \"method_name\": \"{caller}\" }}";
            return CrashReport.Parse("report.json",
                $"{{ \"payload\": {{ \"threads\": [ {{ \"crashed\": \"true\", \"stack_frames\": [ {{ \"is_managed\": \"false\", \"native_module\": \"{module}\" }}{callerFrame} ] }} ] }} }}");
        }
    }
}
