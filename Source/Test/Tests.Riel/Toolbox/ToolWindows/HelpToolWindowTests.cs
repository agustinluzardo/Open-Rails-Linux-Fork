using Riel.Common;
using Riel.Toolbox;
using Riel.Toolbox.Settings;
using Riel.Toolbox.ToolWindows;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Riel.Toolbox.ToolWindows
{
    [TestClass]
    public class HelpToolWindowTests
    {
        [TestMethod]
        public void WhenInactiveRefreshSnapshotThenRowsRemainEmpty()
        {
            HelpToolWindow helpToolWindow = new HelpToolWindow()
            {
                Active = false,
            };

            helpToolWindow.RefreshSnapshot();

            Assert.IsEmpty(helpToolWindow.CaptureSnapshot().Rows);
        }

        [TestMethod]
        public void WhenActiveRefreshSnapshotThenAllCommandsArePublished()
        {
            HelpToolWindow helpToolWindow = new HelpToolWindow()
            {
                Active = true,
            };

            helpToolWindow.RefreshSnapshot();

            Assert.HasCount(EnumExtension.GetValues<UserCommand>().Length, helpToolWindow.CaptureSnapshot().Rows);
        }

        [TestMethod]
        public void WhenSearchByCommandTextThenOnlyMatchingRowIsPublished()
        {
            HelpToolWindow helpToolWindow = new HelpToolWindow()
            {
                Active = true,
            };
            helpToolWindow.SetSearch("Help", HelpSearchColumn.Command);

            helpToolWindow.RefreshSnapshot();

            Assert.HasCount(1, helpToolWindow.CaptureSnapshot().Rows);
        }

        [TestMethod]
        public void WhenSearchByCommandTextThenRowKeyMatchesConfiguredBinding()
        {
            HelpToolWindow helpToolWindow = new HelpToolWindow()
            {
                Active = true,
            };
            helpToolWindow.SetSearch("Help", HelpSearchColumn.Command);

            helpToolWindow.RefreshSnapshot();

            Assert.AreEqual(InputSettings.UserCommands[UserCommand.DisplayHelpWindow].ToString(), helpToolWindow.CaptureSnapshot().Rows[0].Value);
        }

        [TestMethod]
        public void WhenSearchByKeyTextThenOnlyMatchingRowIsPublished()
        {
            HelpToolWindow helpToolWindow = new HelpToolWindow()
            {
                Active = true,
            };
            helpToolWindow.SetSearch("Left", HelpSearchColumn.Key);

            helpToolWindow.RefreshSnapshot();

            Assert.HasCount(1, helpToolWindow.CaptureSnapshot().Rows);
        }

        [TestMethod]
        public void WhenSearchTextIsWhitespaceThenAllRowsArePublished()
        {
            HelpToolWindow helpToolWindow = new HelpToolWindow()
            {
                Active = true,
            };
            helpToolWindow.SetSearch("   ", HelpSearchColumn.Command);

            helpToolWindow.RefreshSnapshot();

            Assert.HasCount(EnumExtension.GetValues<UserCommand>().Length, helpToolWindow.CaptureSnapshot().Rows);
        }
    }
}
