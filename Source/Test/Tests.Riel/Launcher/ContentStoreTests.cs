// COPYRIGHT 2026 by the Riel project.
// This file is part of Riel, a fork of Open Rails, under the GPL version 3 or later.

using System.Collections.Immutable;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Riel.Launcher;
using Riel.Models.Content;

namespace Tests.Riel.Launcher
{
    [TestClass]
    public class ContentStoreTests
    {
        [TestMethod]
        public void AnEmptyNameCannotSelectTheOnlyContentFolder()
        {
            FolderModel folder = new FolderModel("MSTS", "/content", null);
            ContentModel content = new ContentModel(ImmutableArray.Create(folder));
            foreach (string name in new[] { null, string.Empty, " \t " })
                Assert.ThrowsExactly<LauncherException>(() => ContentStore.MatchFolder(content, name));
        }

        [TestMethod]
        public void ExactNamesAndIdsWinBeforePrefixesAndSubstrings()
        {
            FolderModel exact = new FolderModel("Coast", "/one", null) { Id = "coastal-installation" };
            FolderModel prefix = new FolderModel("Coastal trains", "/two", null);
            FolderModel substring = new FolderModel("East Coast trains", "/three", null);
            ContentModel content = new ContentModel(ImmutableArray.Create(exact, prefix, substring));

            Assert.AreSame(exact, ContentStore.MatchFolder(content, "coast"));
            Assert.AreSame(exact, ContentStore.MatchFolder(content, "coastal-installation"));
            Assert.AreSame(prefix, ContentStore.MatchFolder(content, "coastal"));
            Assert.AreSame(substring, ContentStore.MatchFolder(content, "east coast"));
            Assert.ThrowsExactly<LauncherException>(() => ContentStore.MatchFolder(content, "trains"));
        }
    }
}
