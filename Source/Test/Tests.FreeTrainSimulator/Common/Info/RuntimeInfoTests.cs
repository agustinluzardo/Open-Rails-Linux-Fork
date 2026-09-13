using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Content;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Common.Info
{
    [TestClass]
    public class RuntimeInfoTests
    {
        [TestMethod]
        public void ApplicationNameTest()
        {
            string expected = RuntimeInfo.ApplicationName;
            Assert.AreEqual("testhost", expected);
        }

        [TestMethod]
        public void ProductNameTest()
        {
            string expected = RuntimeInfo.ProductName;
            Assert.AreEqual("Riel", expected);
        }

        /// <summary>
        /// The scanned content cache is kept only when the build that wrote it is at or above
        /// <see cref="ContentModel.MinimumVersion"/>. A floor stated in a version line the
        /// program no longer uses puts every build below it, and the symptom is not an error -
        /// it is a full rescan of every route on every single command.
        /// </summary>
        [TestMethod]
        public void ContentCacheFloorIsNotAboveThisBuild()
        {
            Assert.IsTrue(VersionInfo.Compare(ContentModel.MinimumVersion) >= 0,
                $"this build is {VersionInfo.Version}, below the content floor {ContentModel.MinimumVersion}, so content would be rescanned every time");
        }
    }
}
