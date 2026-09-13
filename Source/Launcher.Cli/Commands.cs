// COPYRIGHT 2026 by the Open Rails Linux Fork project.
//
// This file is part of Open Rails.
//
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;

namespace FreeTrainSimulator.Launcher
{
    /// <summary>The launcher's commands.</summary>
    internal static class Commands
    {
        // ------------------------------------------------------------------------------ content

        internal static async Task<int> Content(List<string> arguments, CancellationToken cancellationToken)
        {
            string action = arguments.Count > 0 ? arguments[0].ToLowerInvariant() : "list";

            switch (action)
            {
                case "list":
                {
                    ContentModel content = await ContentStore.Load(cancellationToken).ConfigureAwait(false);
                    if (content.ContentFolders.Length == 0)
                    {
                        Console.WriteLine("No content folders configured. Add one with:");
                        Console.WriteLine("  fts content add \"My routes\" /path/to/train/simulator");
                        return 0;
                    }
                    foreach (FolderModel folder in content.ContentFolders.OrderBy(f => f.Name, StringComparer.CurrentCulture))
                        Console.WriteLine($"{folder.Name,-32} {folder.ContentPath}");
                    return 0;
                }

                case "add":
                {
                    if (arguments.Count < 3)
                        throw new LauncherException("usage: fts content add <name> <path>");
                    await ContentStore.AddFolder(arguments[1], arguments[2], cancellationToken).ConfigureAwait(false);
                    return 0;
                }

                case "remove":
                {
                    if (arguments.Count < 2)
                        throw new LauncherException("usage: fts content remove <name>");
                    await ContentStore.RemoveFolder(arguments[1], cancellationToken).ConfigureAwait(false);
                    return 0;
                }

                case "refresh":
                    await ContentStore.Refresh(cancellationToken).ConfigureAwait(false);
                    return 0;

                default:
                    throw new LauncherException($"unknown content action '{action}'");
            }
        }

        // ------------------------------------------------------------------------------ listing

        internal static async Task<int> Routes(List<string> arguments, CancellationToken cancellationToken)
        {
            ContentModel content = await ContentStore.Load(cancellationToken).ConfigureAwait(false);
            IEnumerable<FolderModel> folders = arguments.Count > 0
                ? new[] { ContentStore.MatchFolder(content, arguments[0]) }
                : content.ContentFolders;

            bool multiple = content.ContentFolders.Length > 1;
            foreach (FolderModel folder in folders)
            {
                ImmutableArray<RouteModelHeader> routes = await folder.GetRoutes(cancellationToken).ConfigureAwait(false);
                foreach (RouteModelHeader route in routes.OrderBy(r => r.Name, StringComparer.CurrentCulture))
                    Console.WriteLine(multiple ? $"{folder.Name,-20} {route.Name}" : route.Name);
            }
            return 0;
        }

        internal static async Task<int> Activities(List<string> arguments, CancellationToken cancellationToken)
        {
            if (arguments.Count < 1)
                throw new LauncherException("usage: fts activities <route>");

            (_, RouteModelHeader route) = await ContentStore.MatchRoute(arguments[0], cancellationToken).ConfigureAwait(false);
            ImmutableArray<ActivityModelHeader> activities = await route.GetActivities(cancellationToken).ConfigureAwait(false);

            foreach (ActivityModelHeader activity in activities.OrderBy(a => a.Name, StringComparer.CurrentCulture))
            {
                // The explorer entries the engine adds are not activities the user picked.
                if (activity.ActivityType != ActivityType.Activity)
                    continue;
                Console.WriteLine($"{activity.Name,-44} {activity.StartTime:HH\\:mm}  {activity.Season,-6} {activity.Weather,-5} {activity.Difficulty}");
            }
            return 0;
        }

        internal static async Task<int> Paths(List<string> arguments, CancellationToken cancellationToken)
        {
            if (arguments.Count < 1)
                throw new LauncherException("usage: fts paths <route>");

            (_, RouteModelHeader route) = await ContentStore.MatchRoute(arguments[0], cancellationToken).ConfigureAwait(false);
            ImmutableArray<PathModelHeader> paths = await route.GetPaths(cancellationToken).ConfigureAwait(false);

            foreach (PathModelHeader path in paths.OrderBy(p => p.Name, StringComparer.CurrentCulture))
                Console.WriteLine($"{path.Name,-44} {path.Start} -> {path.End}");
            return 0;
        }

        internal static async Task<int> Consists(List<string> arguments, CancellationToken cancellationToken)
        {
            ContentModel content = await ContentStore.Load(cancellationToken).ConfigureAwait(false);
            IEnumerable<FolderModel> folders = arguments.Count > 0
                ? new[] { ContentStore.MatchFolder(content, arguments[0]) }
                : content.ContentFolders;

            foreach (FolderModel folder in folders)
            {
                ImmutableArray<WagonSetModel> wagonSets = await folder.GetWagonSets(cancellationToken).ConfigureAwait(false);
                foreach (WagonSetModel wagonSet in wagonSets.OrderBy(w => w.Name, StringComparer.CurrentCulture))
                    Console.WriteLine(wagonSet.Name);
            }
            return 0;
        }

        // ------------------------------------------------------------------------------ playing

        internal static async Task<int> Play(List<string> arguments, CancellationToken cancellationToken)
        {
            if (arguments.Count < 2)
                throw new LauncherException("usage: fts play <route> <activity>");

            (FolderModel folder, RouteModelHeader route) = await ContentStore.MatchRoute(arguments[0], cancellationToken).ConfigureAwait(false);
            ImmutableArray<ActivityModelHeader> activities = await route.GetActivities(cancellationToken).ConfigureAwait(false);
            ActivityModelHeader activity = ContentStore.Match(activities, arguments[1], "activity");

            return Simulator.Start(new[]
            {
                "-SingleplayerNewGame",
                "-Activity",
                folder.Name,
                route.Id,
                activity.Id,
            });
        }

        internal static async Task<int> Explore(List<string> arguments, CancellationToken cancellationToken)
        {
            List<string> positional = new List<string>();
            string time = "12:00";
            string season = nameof(SeasonType.Summer);
            string weather = nameof(WeatherType.Clear);

            for (int i = 0; i < arguments.Count; i++)
            {
                switch (arguments[i])
                {
                    case "--time": time = Next(arguments, ref i, "--time"); break;
                    case "--season": season = Next(arguments, ref i, "--season"); break;
                    case "--weather": weather = Next(arguments, ref i, "--weather"); break;
                    default: positional.Add(arguments[i]); break;
                }
            }

            if (positional.Count < 3)
                throw new LauncherException("usage: fts explore <route> <path> <consist> [--time HH:MM] [--season summer] [--weather clear]");

            if (!TimeOnly.TryParse(time, CultureInfo.CurrentCulture, out TimeOnly startTime))
                throw new LauncherException($"'{time}' is not a time of day");
            if (!EnumExtension.GetValue(season, out SeasonType _))
                throw new LauncherException($"'{season}' is not a season (spring, summer, autumn, winter)");
            if (!EnumExtension.GetValue(weather, out WeatherType _))
                throw new LauncherException($"'{weather}' is not a weather type (clear, snow, rain)");

            (FolderModel folder, RouteModelHeader route) = await ContentStore.MatchRoute(positional[0], cancellationToken).ConfigureAwait(false);

            ImmutableArray<PathModelHeader> paths = await route.GetPaths(cancellationToken).ConfigureAwait(false);
            PathModelHeader path = ContentStore.Match(paths, positional[1], "path");

            ImmutableArray<WagonSetModel> wagonSets = await folder.GetWagonSets(cancellationToken).ConfigureAwait(false);
            WagonSetModel wagonSet = ContentStore.Match(wagonSets, positional[2], "consist");

            return Simulator.Start(new[]
            {
                "-SingleplayerNewGame",
                "-ExploreActivity",
                folder.Name,
                route.Id,
                path.Id,
                wagonSet.Id,
                startTime.ToString("HH\\:mm", CultureInfo.InvariantCulture),
                season,
                weather,
            });
        }

        internal static int Resume()
        {
            return Simulator.Start(new[] { "-SingleplayerResume" });
        }

        internal static int RunRaw(List<string> arguments)
        {
            // "--" is the conventional end-of-options marker; drop it if the shell passed it on.
            if (arguments.Count > 0 && arguments[0] == "--")
                arguments.RemoveAt(0);
            return Simulator.Start(arguments.ToArray());
        }

        // --------------------------------------------------------------------------- diagnostics

        internal static async Task<int> Doctor(CancellationToken cancellationToken)
        {
            return await Diagnostics.Run(cancellationToken).ConfigureAwait(false);
        }

        internal static int Version()
        {
            Console.WriteLine($"{RuntimeInfo.ProductName} {VersionInfo.Version}");
            Console.WriteLine($"runtime  {Environment.Version} on {System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}");
            return 0;
        }

        private static string Next(List<string> arguments, ref int index, string option)
        {
            if (index + 1 >= arguments.Count)
                throw new LauncherException($"{option} needs a value");
            return arguments[++index];
        }
    }
}
