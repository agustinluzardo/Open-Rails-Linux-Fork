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

using System.Collections.Generic;
using System.IO;

using FreeTrainSimulator.Common.Info;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Common.Info
{
    /// <summary>
    /// The trail is what is left of a run that crashed before its log existed, so it has to read
    /// back exactly - and never be mistaken for another run's.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class StartupTrailTests
    {
        [TestMethod]
        public void StepsReadBackInOrderWithTheirDetails()
        {
            string file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                StartupTrail.Begin(file, 4242);
                StartupTrail.Mark(StartupStage.OpeningWindow);
                StartupTrail.Mark(StartupStage.GraphicsDeviceReady, "llvmpipe; x11\nsecond line");

                IReadOnlyList<StartupStep> steps = StartupTrail.Read(file, 4242);

                Assert.AreEqual(3, steps.Count);
                Assert.AreEqual(StartupStage.Started, steps[0].Stage);
                Assert.AreEqual(StartupStage.OpeningWindow, steps[1].Stage);
                Assert.AreEqual(StartupStage.GraphicsDeviceReady, steps[2].Stage);
                Assert.AreEqual("llvmpipe; x11 second line", steps[2].Detail, "a detail stays on its line");
                Assert.IsNull(steps[1].Detail);
                Assert.IsTrue(steps[2].Elapsed >= steps[0].Elapsed);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [TestMethod]
        public void AnotherRunsTrailIsNotRead()
        {
            string file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                StartupTrail.Begin(file, 4242);
                Assert.AreEqual(0, StartupTrail.Read(file, 4243).Count);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [TestMethod]
        public void NoTrailReadsAsEmpty()
        {
            Assert.AreEqual(0, StartupTrail.Read(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), 1).Count);
        }

        [TestMethod]
        public void UnknownLinesAreSkipped()
        {
            string file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                File.WriteAllText(file, "pid 7\n12\tStarted\ngarbage\n30\tNoSuchStage\n40\tLoading\n");
                IReadOnlyList<StartupStep> steps = StartupTrail.Read(file, 7);
                Assert.AreEqual(2, steps.Count);
                Assert.AreEqual(StartupStage.Loading, steps[1].Stage);
            }
            finally
            {
                File.Delete(file);
            }
        }
    }
}
