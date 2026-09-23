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
    }
}
