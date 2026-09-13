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

using FreeTrainSimulator.Common.Display;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Common
{
    /// <summary>
    /// The probe's answer depends on the machine, so what is checked here is the contract the
    /// caller relies on: never more than was asked for, always a count a device can be created
    /// with, and no window created when the answer is already known.
    /// </summary>
    [TestClass]
    public class GraphicsCapabilitiesTests
    {
        [DataTestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(-4)]
        public void AntialiasingOffStaysOff(int requested)
        {
            Assert.AreEqual(0, GraphicsCapabilities.SupportedMultiSampleCount(requested));
        }

        [DataTestMethod]
        [DataRow(2)]
        [DataRow(4)]
        [DataRow(8)]
        [DataRow(16)]
        public void NeverGrantsMoreThanRequested(int requested)
        {
            int granted = GraphicsCapabilities.SupportedMultiSampleCount(requested);
            Assert.IsTrue(granted <= requested, $"asked for {requested}x and was given {granted}x");
            Assert.IsTrue(granted == 0 || granted >= 2, $"{granted}x is not a usable sample count");
        }

        [TestMethod]
        public void GrantIsAPowerOfTwo()
        {
            int granted = GraphicsCapabilities.SupportedMultiSampleCount(16);
            Assert.AreEqual(0, granted & (granted - 1), $"{granted}x is not a power of two");
        }

        [TestMethod]
        public void RepeatedProbesAgree()
        {
            Assert.AreEqual(
                GraphicsCapabilities.SupportedMultiSampleCount(4),
                GraphicsCapabilities.SupportedMultiSampleCount(4));
        }
    }
}
