using System.Collections.Immutable;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Models.Content
{
    [TestClass]
    public class ContentModelCacheRevisionTests
    {
        [TestMethod]
        public void CurrentImporterRevisionDoesNotRequireRefresh()
        {
            ContentModel model = CurrentModel(ContentModel.ImportRevision);

            Assert.IsFalse(model.RefreshRequired());
        }

        [TestMethod]
        public void MissingImporterRevisionRequiresRefresh()
        {
            ContentModel model = new ContentModel()
            {
                Version = VersionInfo.Version,
                Tags = ImmutableDictionary<string, string>.Empty,
            };

            Assert.IsTrue(model.RefreshRequired());
        }

        [TestMethod]
        public void OldImporterRevisionRequiresRefresh()
        {
            ContentModel model = CurrentModel("old");

            Assert.IsTrue(model.RefreshRequired());
        }

        private static ContentModel CurrentModel(string revision)
            => new ContentModel()
            {
                Version = VersionInfo.Version,
                Tags = ImmutableDictionary<string, string>.Empty
                    .Add(ContentModel.ImportRevisionTag, revision),
            };
    }
}
