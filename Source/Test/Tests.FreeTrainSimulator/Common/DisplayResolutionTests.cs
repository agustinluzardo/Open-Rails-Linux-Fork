using System.Drawing;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Display;
using FreeTrainSimulator.Models.Settings;

using MemoryPack;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Common
{
    [TestClass]
    public class DisplayResolutionTests
    {
        [TestMethod]
        public void FullscreenUsesPhysicalMonitorPixels()
        {
            Rectangle bounds = new Rectangle(1920, 0, 2560, 1440);
            Rectangle working = new Rectangle(1920, 0, 2560, 1390);
            Assert.AreEqual((2560, 1440), DisplayResolution.SizeFor(ScreenMode.WindowedFullscreen, bounds, working));
            Assert.AreEqual((2560, 1440), DisplayResolution.SizeFor(ScreenMode.BorderlessFullscreen, bounds, working));
        }

        [TestMethod]
        public void WindowedLeavesRoomForPanelsAndFrame()
        {
            Assert.AreEqual((1728, 936), DisplayResolution.SizeFor(ScreenMode.Windowed,
                new Rectangle(0, 0, 1920, 1080), new Rectangle(0, 0, 1920, 1040)));
            Assert.AreEqual((720, 432), DisplayResolution.SizeFor(ScreenMode.Windowed,
                new Rectangle(0, 0, 800, 480), new Rectangle(0, 0, 800, 480)));
        }

        [TestMethod]
        public void ResolutionPreferenceSurvivesProfileSerialization()
        {
            ProfileUserSettingsModel settings = new ProfileUserSettingsModel { UseDesktopResolution = false };
            ProfileUserSettingsModel restored = MemoryPackSerializer.Deserialize<ProfileUserSettingsModel>(
                MemoryPackSerializer.Serialize(settings));
            Assert.IsFalse(restored.UseDesktopResolution);
            Assert.IsTrue(new ProfileUserSettingsModel().UseDesktopResolution);
        }
    }
}
