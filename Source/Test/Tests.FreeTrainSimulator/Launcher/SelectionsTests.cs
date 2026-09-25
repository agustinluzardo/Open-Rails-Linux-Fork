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
using System.IO;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Settings;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Riel.Launcher;

namespace Tests.FreeTrainSimulator.Launcher
{
    /// <summary>
    /// The command line a selection turns into must be one the simulator's own parser accepts;
    /// these are the shapes GameStateRunActivity.ResolveSelectionsFromCommandLine reads.
    /// </summary>
    [TestClass]
    public class SelectionsTests
    {
        [TestMethod]
        public void AnActivityIsFolderRouteAndActivity()
        {
            ProfileSelectionsModel activity = new ProfileSelectionsModel
            {
                GamePlayAction = GamePlayAction.SingleplayerNewGame,
                ActivityType = ActivityType.Activity,
                FolderName = "Train Simulator",
                RouteId = "USA2",
                ActivityId = "coal",
            };
            CollectionAssert.AreEqual(
                new[] { "-SingleplayerNewGame", "-Activity", "Train Simulator", "USA2", "coal" },
                Selections.Arguments(activity));
        }

        [TestMethod]
        public void ExploringIsSevenValuesWithTheTimeAsHoursAndMinutes()
        {
            ProfileSelectionsModel explore = new ProfileSelectionsModel
            {
                GamePlayAction = GamePlayAction.SingleplayerNewGame,
                ActivityType = ActivityType.Explorer,
                FolderName = "MSTS",
                RouteId = "EUROPE1",
                PathId = "Innsbruck-Bludenz",
                WagonSetId = "freight 1",
                StartTime = new TimeOnly(8, 5),
                Season = SeasonType.Autumn,
                Weather = WeatherType.Rain,
            };
            CollectionAssert.AreEqual(
                new[] { "-SingleplayerNewGame", "-Explorer", "MSTS", "EUROPE1", "Innsbruck-Bludenz", "freight 1", "08:05", "Autumn", "Rain" },
                Selections.Arguments(explore));
        }

        [TestMethod]
        public void LegacyExploreActivityStillUsesItsExplicitMode()
        {
            ProfileSelectionsModel explore = new ProfileSelectionsModel
            {
                GamePlayAction = GamePlayAction.SingleplayerNewGame,
                ActivityType = ActivityType.ExploreActivity,
                FolderName = "MSTS",
                RouteId = "EUROPE1",
                PathId = "Innsbruck-Bludenz",
                WagonSetId = "freight 1",
                StartTime = new TimeOnly(8, 5),
                Season = SeasonType.Autumn,
                Weather = WeatherType.Rain,
            };
            CollectionAssert.AreEqual(
                new[] { "-SingleplayerNewGame", "-ExploreActivity", "MSTS", "EUROPE1", "Innsbruck-Bludenz", "freight 1", "08:05", "Autumn", "Rain" },
                Selections.Arguments(explore));
        }

        [TestMethod]
        public void TimetableSaveUsesTimetableResumeAction()
        {
            string save = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".save");
            File.WriteAllBytes(save, Array.Empty<byte>());
            try
            {
                CollectionAssert.AreEqual(
                    new[] { "-SinglePlayerResumeTimetableGame", Path.GetFullPath(save) },
                    Selections.SavedGameArguments(save, "-SinglePlayerResumeTimetableGame").ToArray());
            }
            finally
            {
                File.Delete(save);
            }
        }

        [TestMethod]
        public void NothingChosenIsNotPlayable()
        {
            Assert.IsFalse(Selections.IsPlayable(null));
            Assert.IsFalse(Selections.IsPlayable(new ProfileSelectionsModel()));
        }

        [TestMethod]
        public void AnActivityWithoutItsIdIsNotPlayable()
        {
            Assert.IsFalse(Selections.IsPlayable(new ProfileSelectionsModel
            {
                GamePlayAction = GamePlayAction.SingleplayerNewGame,
                ActivityType = ActivityType.Activity,
                FolderName = "MSTS",
                RouteId = "USA2",
            }));
        }

        [TestMethod]
        public void ExploringNeedsAPathAndATrain()
        {
            ProfileSelectionsModel explore = new ProfileSelectionsModel
            {
                GamePlayAction = GamePlayAction.SingleplayerNewGame,
                ActivityType = ActivityType.ExploreActivity,
                FolderName = "MSTS",
                RouteId = "USA2",
                PathId = "main",
            };
            Assert.IsFalse(Selections.IsPlayable(explore));
            explore.WagonSetId = "coal";
            Assert.IsTrue(Selections.IsPlayable(explore));
        }
    }
}
