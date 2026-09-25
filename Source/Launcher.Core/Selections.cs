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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;

namespace Riel.Launcher
{
    /// <summary>
    /// What to play, as the simulator understands it, and where it is remembered between runs.
    /// </summary>
    /// <remarks>
    /// The selections live in the current profile, the same record the Windows menu writes, and
    /// the simulator started with no arguments reads them from there. Saving them on every start
    /// is what makes "start again with what I played last" work - from the desktop icon, from
    /// "riel start", and when the graphical launcher reopens on the route it was left on.
    ///
    /// A run is still started with the selection spelled out as arguments rather than relying on
    /// the saved copy. The two describe the same thing; the arguments make the run independent of
    /// which profile happens to be current, and give a command line that reproduces it exactly.
    /// </remarks>
    public static class Selections
    {
        public static async Task<ProfileSelectionsModel> Load(CancellationToken cancellationToken)
        {
            ProfileModel profile = await ((ProfileModel)null).Current(cancellationToken).ConfigureAwait(false);
            return await profile.LoadSettingsModel<ProfileSelectionsModel>(cancellationToken).ConfigureAwait(false)
                ?? new ProfileSelectionsModel();
        }

        public static async Task Save(ProfileSelectionsModel selections, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(selections);

            ProfileModel profile = await ((ProfileModel)null).Current(cancellationToken).ConfigureAwait(false);
            await profile.UpdateSettingsModel(selections with { Id = profile.Name, Name = profile.Name }, cancellationToken).ConfigureAwait(false);
            await profile.UpdateCurrent(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Whether <paramref name="selections"/> names something that can be started.</summary>
        public static bool IsPlayable(ProfileSelectionsModel selections)
        {
            return selections != null
                && selections.GamePlayAction != GamePlayAction.None
                && !string.IsNullOrEmpty(selections.FolderName)
                && !string.IsNullOrEmpty(selections.RouteId)
                && selections.ActivityType switch
                {
                    ActivityType.Activity => !string.IsNullOrEmpty(selections.ActivityId),
                    ActivityType.Explorer or ActivityType.ExploreActivity => !string.IsNullOrEmpty(selections.PathId) && !string.IsNullOrEmpty(selections.WagonSetId),
                    ActivityType.TimeTable => !string.IsNullOrEmpty(selections.TimetableSet) &&
                        !string.IsNullOrEmpty(selections.TimetableName) && !string.IsNullOrEmpty(selections.TimetableTrain),
                    _ => false,
                };
        }

        public static ProfileSelectionsModel Activity(FolderModel folder, RouteModelHeader route, ActivityModelHeader activity)
        {
            ArgumentNullException.ThrowIfNull(folder);
            ArgumentNullException.ThrowIfNull(route);
            ArgumentNullException.ThrowIfNull(activity);

            return new ProfileSelectionsModel
            {
                GamePlayAction = GamePlayAction.SingleplayerNewGame,
                ActivityType = ActivityType.Activity,
                FolderName = folder.Name,
                RouteId = route.Id,
                ActivityId = activity.Id,
                StartTime = activity.StartTime,
                Season = activity.Season,
                Weather = activity.Weather,
            };
        }

        public static ProfileSelectionsModel Explore(FolderModel folder, RouteModelHeader route, PathModelHeader path, WagonSetModel consist,
            TimeOnly startTime, SeasonType season, WeatherType weather)
        {
            ArgumentNullException.ThrowIfNull(folder);
            ArgumentNullException.ThrowIfNull(route);
            ArgumentNullException.ThrowIfNull(path);
            ArgumentNullException.ThrowIfNull(consist);

            return new ProfileSelectionsModel
            {
                GamePlayAction = GamePlayAction.SingleplayerNewGame,
                // The launcher's Explore tab must use the simulator's real Explorer mode.
                // ExploreActivity runs the consist through the AI/autopilot initialization path,
                // which can reject otherwise-valid legacy MSTS paths or place long consists badly.
                ActivityType = ActivityType.Explorer,
                FolderName = folder.Name,
                RouteId = route.Id,
                PathId = path.Id,
                LocomotiveId = consist.Locomotive?.Reference,
                WagonSetId = consist.Id,
                StartTime = startTime,
                Season = season,
                Weather = weather,
            };
        }

        public static ProfileSelectionsModel Timetable(FolderModel folder, RouteModelHeader route, TimetableModel timetable,
            TimetableTrainModel train, DayOfWeek day, SeasonType season, WeatherType weather, WeatherModelHeader weatherFile)
        {
            ArgumentNullException.ThrowIfNull(folder);
            ArgumentNullException.ThrowIfNull(route);
            ArgumentNullException.ThrowIfNull(timetable);
            ArgumentNullException.ThrowIfNull(train);
            return new ProfileSelectionsModel
            {
                GamePlayAction = GamePlayAction.SinglePlayerTimetableGame,
                ActivityType = ActivityType.TimeTable,
                FolderName = folder.Name,
                RouteId = route.Id,
                TimetableSet = timetable.Id,
                TimetableName = train.Group,
                TimetableTrain = train.Id,
                TimetableDay = day,
                Season = season,
                Weather = weather,
                WeatherChanges = weatherFile?.Id,
            };
        }

        /// <summary>The simulator's command line for <paramref name="selections"/>.</summary>
        public static string[] Arguments(ProfileSelectionsModel selections)
        {
            ArgumentNullException.ThrowIfNull(selections);

            return selections.ActivityType switch
            {
                ActivityType.Activity => new[]
                {
                    "-SingleplayerNewGame",
                    "-Activity",
                    selections.FolderName,
                    selections.RouteId,
                    selections.ActivityId,
                },
                ActivityType.Explorer => new[]
                {
                    "-SingleplayerNewGame",
                    "-Explorer",
                    selections.FolderName,
                    selections.RouteId,
                    selections.PathId,
                    selections.WagonSetId,
                    selections.StartTime.ToString("HH\\:mm", CultureInfo.InvariantCulture),
                    selections.Season.ToString(),
                    selections.Weather.ToString(),
                },
                // Preserve explicit legacy ExploreActivity selections for backwards compatibility.
                ActivityType.ExploreActivity => new[]
                {
                    "-SingleplayerNewGame",
                    "-ExploreActivity",
                    selections.FolderName,
                    selections.RouteId,
                    selections.PathId,
                    selections.WagonSetId,
                    selections.StartTime.ToString("HH\\:mm", CultureInfo.InvariantCulture),
                    selections.Season.ToString(),
                    selections.Weather.ToString(),
                },
                ActivityType.TimeTable => new[]
                {
                    "-SinglePlayerTimetableGame", "-TimeTable",
                    selections.FolderName, selections.RouteId, selections.TimetableSet,
                    selections.TimetableName, selections.TimetableTrain,
                    selections.TimetableDay.ToString(), selections.Season.ToString(), selections.Weather.ToString(),
                }.Concat(string.IsNullOrEmpty(selections.WeatherChanges)
                    ? Array.Empty<string>() : new[] { selections.WeatherChanges }).ToArray(),
                _ => throw new LauncherException($"cannot start a {selections.ActivityType} selection"),
            };
        }

        /// <summary>Saved games in the same folder the simulator writes to, newest first.</summary>
        public static IReadOnlyList<FileInfo> SavedGames()
        {
            try
            {
                DirectoryInfo saves = new DirectoryInfo(RuntimeInfo.UserDataFolder);
                return saves.Exists
                    ? saves.EnumerateFiles("*" + FileNameExtensions.SaveFile)
                        .OrderByDescending(file => file.LastWriteTimeUtc)
                        .ToArray()
                    : Array.Empty<FileInfo>();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return Array.Empty<FileInfo>();
            }
        }

        /// <summary>The newest saved game, or null when there is none to continue.</summary>
        public static string LatestSave() => SavedGames().FirstOrDefault()?.FullName;

        /// <summary>Whether the save picker should be available, including for restoring deleted saves.</summary>
        public static bool HasSavesOrDeletedSaves()
        {
            if (LatestSave() != null)
                return true;
            try
            {
                return Directory.Exists(RuntimeInfo.DeletedSaveFolder) &&
                    Directory.EnumerateFiles(RuntimeInfo.DeletedSaveFolder).Any();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>The simulator's command line for continuing the chosen saved game.</summary>
        public static IReadOnlyList<string> ResumeArguments(string save)
        {
            if (string.IsNullOrWhiteSpace(save) || !File.Exists(save) ||
                !string.Equals(Path.GetExtension(save), FileNameExtensions.SaveFile, StringComparison.OrdinalIgnoreCase))
                throw new LauncherException("the selected saved game is no longer available");
            return new[] { "-SingleplayerResume", Path.GetFullPath(save) };
        }

        public static IReadOnlyList<string> SavedGameArguments(string save, string action)
        {
            if (action != "-SingleplayerResume" &&
                action != "-SinglePlayerResumeTimetableGame" &&
                action != "-SingleplayerReplay" &&
                action != "-SingleplayerReplayFromSave")
                throw new LauncherException("unsupported saved game action");
            string fullPath = ResumeArguments(save)[1];
            bool replayAction = action is "-SingleplayerReplay" or "-SingleplayerReplayFromSave";
            if (replayAction && !File.Exists(Path.ChangeExtension(fullPath, ".replay")))
                throw new LauncherException("the selected saved game has no replay log");
            return new[] { action, fullPath };
        }

        /// <summary>The simulator's command line for continuing the newest save.</summary>
        public static IReadOnlyList<string> ResumeArguments()
        {
            string save = LatestSave() ?? throw new LauncherException("there is no saved game to continue");
            return ResumeArguments(save);
        }
    }
}
