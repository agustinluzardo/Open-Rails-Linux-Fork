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

using System.Linq;

using FreeTrainSimulator.Common.Display;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Common
{
    /// <summary>
    /// The error dialog's paragraph arrives on one line; unwrapped, SDL made a box wider than the
    /// screen and cut the explanation off at both edges.
    /// </summary>
    [TestClass]
    public class MessageDialogTests
    {
        private const string Paragraph =
            "An error occurred during the simulator start up and it could not continue. " +
            "You can help improve Riel by reporting this error in our bug tracker and attaching the log file.";

        [TestMethod]
        public void NoLineIsLongerThanTheWidth()
        {
            string wrapped = MessageDialog.Wrap(Paragraph, 40);
            Assert.IsTrue(wrapped.Split('\n').All(line => line.Length <= 40), wrapped);
        }

        [TestMethod]
        public void NothingIsLostOrReordered()
        {
            string wrapped = MessageDialog.Wrap(Paragraph, 40);
            Assert.AreEqual(Paragraph, wrapped.Replace('\n', ' '));
        }

        [TestMethod]
        public void ExistingLineBreaksAreKept()
        {
            string wrapped = MessageDialog.Wrap("first\n\nsecond", 40);
            Assert.AreEqual("first\n\nsecond", wrapped);
        }

        /// <summary>A path cut in the middle is worse than a line that runs long.</summary>
        [TestMethod]
        public void AWordLongerThanTheWidthStaysWhole()
        {
            const string path = "/root/.local/state/riel/Logs/Riel_Simulator_Log.txt";
            string wrapped = MessageDialog.Wrap("see " + path + " for details", 20);
            CollectionAssert.Contains(wrapped.Split('\n'), path);
        }

        [TestMethod]
        public void WindowsLineEndingsBecomeOne()
        {
            Assert.AreEqual("a\nb", MessageDialog.Wrap("a\r\nb", 40));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        public void EmptyStaysEmpty(string text)
        {
            Assert.AreEqual(text, MessageDialog.Wrap(text, 40));
        }
    }
}
