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
using FreeTrainSimulator.Common.Native;
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
                FolderPath = folder.ContentPath,
                RouteSourceName = Source(route.Tags, "MstsSourceRoute", route.Id),
                ActivitySourceName = Source(activity.Tags, "MstsSourceActivity", activity.Id),
                ActivityId = activity.Id,
                StartTime = activity.StartTime,
                Season = activity.Season,
                Weather = activity.Weather,
            };
        }

        public static ProfileSelectionsModel Explore(FolderModel folder, RouteModelHeader route, PathModelHeader path, WagonSetModel consist,
            TimeOnly startTime, SeasonType season, WeatherType weather, bool activityMode = false)
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
                ActivityType = activityMode ? ActivityType.ExploreActivity : ActivityType.Explorer,
                FolderName = folder.Name,
                RouteId = route.Id,
                FolderPath = folder.ContentPath,
                RouteSourceName = Source(route.Tags, "MstsSourceRoute", route.Id),
                PathSourceName = Source(path.Tags, "MstsSourcePath", path.Id),
                WagonSetSourceName = Source(consist.Tags, "MstsSourceConsist", consist.Id),
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
                FolderPath = folder.ContentPath,
                RouteSourceName = Source(route.Tags, "MstsSourceRoute", route.Id),
                TimetableSourceFile = Source(timetable.Tags, "OrSourceRoute", timetable.Id),
                WeatherSourceFile = weatherFile == null ? null : Source(weatherFile.Tags, "ORSourceWeather", weatherFile.Id),
                TimetableSet = timetable.Id,
                TimetableName = train.Group,
                TimetableTrain = train.Id,
                TimetableDay = day,
                Season = season,
                Weather = weather,
                WeatherChanges = weatherFile?.Id,
            };
        }

        /// <summary>The Open Rails runner command line for <paramref name="selections"/>.</summary>
        public static string[] Arguments(ProfileSelectionsModel selections)
        {
            ArgumentNullException.ThrowIfNull(selections);

            string routeFolder = RouteFolder(selections);
            return selections.ActivityType switch
            {
                ActivityType.Activity => new[]
                {
                    "-start",
                    "-activity",
                    ResolveFile(Path.Combine(routeFolder, "Activities", EnsureExtension(selections.ActivitySourceName ?? selections.ActivityId, ".act"))),
                },
                ActivityType.Explorer => new[]
                {
                    "-start",
                    "-explorer",
                    ResolveFile(Path.Combine(routeFolder, "Paths", EnsureExtension(selections.PathSourceName ?? selections.PathId, ".pat"))),
                    ResolveFile(Path.Combine(selections.FolderPath, "Trains", "Consists", EnsureExtension(selections.WagonSetSourceName ?? selections.WagonSetId, ".con"))),
                    selections.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                    ((int)selections.Season).ToString(CultureInfo.InvariantCulture),
                    ((int)selections.Weather).ToString(CultureInfo.InvariantCulture),
                },
                ActivityType.ExploreActivity => new[]
                {
                    "-start",
                    "-exploreactivity",
                    ResolveFile(Path.Combine(routeFolder, "Paths", EnsureExtension(selections.PathSourceName ?? selections.PathId, ".pat"))),
                    ResolveFile(Path.Combine(selections.FolderPath, "Trains", "Consists", EnsureExtension(selections.WagonSetSourceName ?? selections.WagonSetId, ".con"))),
                    selections.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                    ((int)selections.Season).ToString(CultureInfo.InvariantCulture),
                    ((int)selections.Weather).ToString(CultureInfo.InvariantCulture),
                },
                ActivityType.TimeTable => BuildTimetableArguments(selections, routeFolder),
                _ => throw new LauncherException($"cannot start a {selections.ActivityType} selection"),
            };
        }

        private static string[] BuildTimetableArguments(ProfileSelectionsModel selections, string routeFolder)
        {
            string timetable = ResolveFile(Path.Combine(routeFolder, "Activities", "OpenRails",
                selections.TimetableSourceFile ?? selections.TimetableSet));
            List<string> arguments = new List<string>
            {
                "-start",
                "-timetable",
                timetable,
                $"{selections.TimetableName}:{selections.TimetableTrain}",
                ((int)selections.TimetableDay).ToString(CultureInfo.InvariantCulture),
                ((int)selections.Season).ToString(CultureInfo.InvariantCulture),
                ((int)selections.Weather).ToString(CultureInfo.InvariantCulture),
            };

            if (!string.IsNullOrWhiteSpace(selections.WeatherSourceFile ?? selections.WeatherChanges))
                arguments.Add(ResolveFile(Path.Combine(routeFolder, "WeatherFiles",
                    selections.WeatherSourceFile ?? EnsureExtension(selections.WeatherChanges, ".weather-or"))));

            return arguments.ToArray();
        }

        private static string RouteFolder(ProfileSelectionsModel selections)
        {
            if (string.IsNullOrWhiteSpace(selections.FolderPath))
                throw new LauncherException("this saved selection predates the Open Rails migration; select the route again once in the launcher");

            string route = Path.Combine(selections.FolderPath, "Routes", selections.RouteSourceName ?? selections.RouteId);
            return ContentIO.ResolveDirectory(route) ?? ContentIO.Normalize(route);
        }

        private static string ResolveFile(string path)
        {
            string resolved = ContentIO.ResolveFile(path);
            return resolved ?? ContentIO.Normalize(path);
        }

        private static string EnsureExtension(string value, string extension)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;
            return value.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? value : value + extension;
        }

        private static string Source(System.Collections.Immutable.ImmutableDictionary<string, string> tags, string key, string fallback)
        {
            return tags != null && tags.TryGetValue(key, out string source) && !string.IsNullOrWhiteSpace(source)
                ? source
                : fallback;
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
            return new[] { "-resume", Path.GetFullPath(save) };
        }

        public static IReadOnlyList<string> SavedGameArguments(string save, string action)
        {
            string runnerAction = action switch
            {
                "-SingleplayerResume" or "-SinglePlayerResumeTimetableGame" or "-resume" => "-resume",
                "-SingleplayerReplay" or "-replay" => "-replay",
                "-SingleplayerReplayFromSave" or "-replay_from_save" => "-replay_from_save",
                _ => throw new LauncherException("unsupported saved game action"),
            };
            string fullPath = ResumeArguments(save)[1];
            bool replayAction = runnerAction is "-replay" or "-replay_from_save";
            if (replayAction && !File.Exists(Path.ChangeExtension(fullPath, ".replay")))
                throw new LauncherException("the selected saved game has no replay log");
            return new[] { runnerAction, fullPath };
        }

        /// <summary>The simulator's command line for continuing the newest save.</summary>
        public static IReadOnlyList<string> ResumeArguments()
        {
            string save = LatestSave() ?? throw new LauncherException("there is no saved game to continue");
            return ResumeArguments(save);
        }
    }
}
