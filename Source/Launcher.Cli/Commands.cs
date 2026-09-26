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
using System.Collections.Immutable;
using System.Diagnostics;
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
                    ContentModel content = await ContentStore.Load(ScanProgress.Create(), cancellationToken).ConfigureAwait(false);
                    ScanProgress.Done();
                    if (content.ContentFolders.Length == 0)
                    {
                        Console.WriteLine("No content folders configured. Add one with:");
                        Console.WriteLine("  riel content add \"My routes\" /path/to/train/simulator");
                        return 0;
                    }
                    foreach (FolderModel folder in content.ContentFolders.OrderBy(f => f.Name, StringComparer.CurrentCulture))
                        Console.WriteLine($"{folder.Name,-32} {folder.ContentPath}");
                    return 0;
                }

                case "add":
                {
                    if (arguments.Count < 3)
                        throw new LauncherException("usage: riel content add <name> <path>");
                    _ = await ContentStore.AddFolder(arguments[1], arguments[2], ScanProgress.Create(), cancellationToken).ConfigureAwait(false);
                    ScanProgress.Done();
                    return 0;
                }

                case "remove":
                {
                    if (arguments.Count < 2)
                        throw new LauncherException("usage: riel content remove <name>");
                    _ = await ContentStore.RemoveFolder(arguments[1], ScanProgress.Create(), cancellationToken).ConfigureAwait(false);
                    ScanProgress.Done();
                    return 0;
                }

                case "refresh":
                    _ = await ContentStore.Refresh(ScanProgress.Create(), cancellationToken).ConfigureAwait(false);
                    ScanProgress.Done();
                    return 0;

                default:
                    throw new LauncherException($"unknown content action '{action}'");
            }
        }

        // ------------------------------------------------------------------------------ listing

        internal static async Task<int> Routes(List<string> arguments, CancellationToken cancellationToken)
        {
            ContentModel content = await LoadContent(cancellationToken).ConfigureAwait(false);
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
                throw new LauncherException("usage: riel activities <route>");

            (_, RouteModelHeader route) = await MatchRoute(arguments[0], cancellationToken).ConfigureAwait(false);
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
                throw new LauncherException("usage: riel paths <route>");

            (_, RouteModelHeader route) = await MatchRoute(arguments[0], cancellationToken).ConfigureAwait(false);
            ImmutableArray<PathModelHeader> paths = await route.GetPaths(cancellationToken).ConfigureAwait(false);

            foreach (PathModelHeader path in paths.OrderBy(p => p.Name, StringComparer.CurrentCulture))
                Console.WriteLine($"{path.Name,-44} {path.Start} -> {path.End}");
            return 0;
        }

        internal static async Task<int> Consists(List<string> arguments, CancellationToken cancellationToken)
        {
            ContentModel content = await LoadContent(cancellationToken).ConfigureAwait(false);
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
                throw new LauncherException("usage: riel play <route> <activity>");

            (FolderModel folder, RouteModelHeader route) = await MatchRoute(arguments[0], cancellationToken).ConfigureAwait(false);
            ImmutableArray<ActivityModelHeader> activities = await route.GetActivities(cancellationToken).ConfigureAwait(false);
            ActivityModelHeader activity = ContentStore.Match(activities, arguments[1], "activity");

            return await Run(Selections.Activity(folder, route, activity), cancellationToken).ConfigureAwait(false);
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
                throw new LauncherException("usage: riel explore <route> <path> <consist> [--time HH:MM] [--season summer] [--weather clear]");

            if (!TimeOnly.TryParse(time, CultureInfo.CurrentCulture, out TimeOnly startTime))
                throw new LauncherException($"'{time}' is not a time of day");
            if (!EnumExtension.GetValue(season, out SeasonType seasonType))
                throw new LauncherException($"'{season}' is not a season (spring, summer, autumn, winter)");
            if (!EnumExtension.GetValue(weather, out WeatherType weatherType))
                throw new LauncherException($"'{weather}' is not a weather type (clear, snow, rain)");

            (FolderModel folder, RouteModelHeader route) = await MatchRoute(positional[0], cancellationToken).ConfigureAwait(false);

            ImmutableArray<PathModelHeader> paths = await route.GetPaths(cancellationToken).ConfigureAwait(false);
            PathModelHeader path = ContentStore.Match(paths, positional[1], "path");

            ImmutableArray<WagonSetModel> wagonSets = await folder.GetWagonSets(cancellationToken).ConfigureAwait(false);
            WagonSetModel wagonSet = ContentStore.Match(wagonSets, positional[2], "consist");

            return await Run(Selections.Explore(folder, route, path, wagonSet, startTime, seasonType, weatherType), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Starts whatever was played last. The selection is saved by every start, from here or
        /// from the graphical launcher, so this has something to go on once anything has been
        /// played at all.
        /// </summary>
        internal static async Task<int> Start(CancellationToken cancellationToken)
        {
            ProfileSelectionsModel selections = await Selections.Load(cancellationToken).ConfigureAwait(false);
            if (!Selections.IsPlayable(selections))
            {
                throw new LauncherException(
                    "nothing has been played yet, so there is nothing to start again. Open the launcher with 'riel gui', " +
                    "or pick something with 'riel play' or 'riel explore'.");
            }
            return Report(Simulator.Start(Selections.Arguments(selections), captureErrors: false));
        }

        internal static int Resume()
        {
            return Report(Simulator.Start(Selections.ResumeArguments(), captureErrors: false));
        }

        internal static int RunRaw(List<string> arguments)
        {
            // "--" is the conventional end-of-options marker; drop it if the shell passed it on.
            if (arguments.Count > 0 && arguments[0] == "--")
                arguments.RemoveAt(0);
            return Report(Simulator.Start(arguments, captureErrors: false));
        }

        /// <summary>Opens the graphical launcher, which is installed beside this command.</summary>
        internal static int Gui(List<string> arguments)
        {
            string gui = Path.Combine(AppContext.BaseDirectory, "riel-gui");
            if (!File.Exists(gui))
                throw new LauncherException($"the graphical launcher is not installed ({gui} is missing)");

            ProcessStartInfo startInfo = new ProcessStartInfo(gui) { UseShellExecute = false };
            foreach (string argument in arguments)
                startInfo.ArgumentList.Add(argument);
            using Process process = Process.Start(startInfo) ?? throw new LauncherException($"could not start {gui}");
            process.WaitForExit();
            return process.ExitCode;
        }

        private static async Task<int> Run(ProfileSelectionsModel selections, CancellationToken cancellationToken)
        {
            await Selections.Save(selections, cancellationToken).ConfigureAwait(false);
            return Report(Simulator.Start(Selections.Arguments(selections), captureErrors: false));
        }

        /// <summary>
        /// Waits for the simulator and, when it failed, says so and where to look. Standard error
        /// already reached the terminal; what the terminal never showed is the log.
        /// </summary>
        private static int Report(SimulatorRun run)
        {
            using (run)
            {
                Console.Error.WriteLine($"Starting {run.CommandLine}");
                SimulatorOutcome outcome = run.Wait();

                if (!outcome.Failed)
                    return 0;

                Console.Error.WriteLine();
                if (outcome.CrashedNatively)
                    ReportCrash(outcome);
                else
                {
                    Console.Error.WriteLine(outcome.ExitCode != 0
                        ? $"riel: the simulator stopped with exit code {outcome.ExitCode}."
                        : "riel: the simulator stopped with an error.");
                }
                if (outcome.FatalError != null)
                {
                    Console.Error.WriteLine();
                    Console.Error.WriteLine(outcome.FatalError);
                }
                Console.Error.WriteLine();
                Console.Error.WriteLine(outcome.LogFile != null
                    ? $"The full log is {outcome.LogFile}"
                    : $"No log was written; the simulator stopped before it got that far. Logs live in {RuntimeInfo.LogFilesFolder}");
                return outcome.ExitCode != 0 ? outcome.ExitCode : 1;
            }
        }

        /// <summary>
        /// Says where a native crash happened and what to try: the terminal saw nothing, since a
        /// crash in a driver ends the process without a word.
        /// </summary>
        private static void ReportCrash(SimulatorOutcome outcome)
        {
            string stage = outcome.Stage switch
            {
                StartupStage.Started => " while starting",
                StartupStage.OpeningWindow => " while opening its window",
                StartupStage.CreatingGraphicsDevice => " while setting up the graphics card",
                StartupStage.GraphicsDeviceReady => " while showing its window for the first time",
                StartupStage.FirstFrame => " just after showing its window",
                StartupStage.StartingSound => " while opening the sound device",
                StartupStage.SoundReady or StartupStage.Loading => " while loading the route",
                StartupStage.Running => " while running",
                _ => string.Empty,
            };
            Console.Error.WriteLine($"riel: the simulator crashed ({outcome.SignalName}){stage}.");

            if (outcome.Crash != null)
            {
                Console.Error.WriteLine($"It crashed in {outcome.Crash.Module ?? "managed code"}{(outcome.Crash.Caller == null ? "" : ", called from " + outcome.Crash.Caller)}.");
                Console.Error.WriteLine();
                foreach (string frame in outcome.Crash.Frames.Take(12))
                    Console.Error.WriteLine("  " + frame);
                Console.Error.WriteLine();
                Console.Error.WriteLine($"Crash report: {outcome.Crash.File}");
            }

            foreach (StartupStep step in outcome.Trail.Where(step => step.Detail != null))
                Console.Error.WriteLine($"{step.Stage}: {step.Detail}");

            Console.Error.WriteLine();
            Console.Error.WriteLine("A crash like this happens in a driver or a system library, not in the route. These leave");
            Console.Error.WriteLine("out the likeliest parts, one each, for this run only:");
            Console.Error.WriteLine($"  {LaunchContext.NoSoundVariable}=1 riel start");
            Console.Error.WriteLine($"  {LaunchContext.BasicGraphicsVariable}=1 riel start");
        }

        // --------------------------------------------------------------------------- diagnostics

        internal static async Task<int> Doctor(CancellationToken cancellationToken)
        {
            IReadOnlyList<CheckResult> results = await Diagnostics.Run(cancellationToken).ConfigureAwait(false);
            foreach (CheckResult result in results)
            {
                string mark = result.State switch
                {
                    CheckState.Ok => "ok  ",
                    CheckState.Failed => "FAIL",
                    _ => "--  ",
                };
                Console.WriteLine($"  {mark}  {result.Name,-12} {result.Detail}");
            }

            bool healthy = Diagnostics.Healthy(results);
            Console.WriteLine();
            Console.WriteLine(healthy
                ? "Everything needed to run is in place."
                : "Some checks failed; see the notes above.");
            return healthy ? 0 : 1;
        }

        internal static int Version()
        {
            Console.WriteLine($"{RuntimeInfo.ProductName} {VersionInfo.Version}");
            Console.WriteLine($"runtime  {Environment.Version} on {System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}");
            return 0;
        }

        // ------------------------------------------------------------------------------ helpers

        private static async Task<ContentModel> LoadContent(CancellationToken cancellationToken)
        {
            ContentModel content = await ContentStore.Load(ScanProgress.Create(), cancellationToken).ConfigureAwait(false);
            ScanProgress.Done();
            return content;
        }

        private static async Task<(FolderModel Folder, RouteModelHeader Route)> MatchRoute(string name, CancellationToken cancellationToken)
        {
            (FolderModel, RouteModelHeader) match = await ContentStore.MatchRoute(name, ScanProgress.Create(), cancellationToken).ConfigureAwait(false);
            ScanProgress.Done();
            return match;
        }

        private static string Next(List<string> arguments, ref int index, string option)
        {
            if (index + 1 >= arguments.Count)
                throw new LauncherException($"{option} needs a value");
            return arguments[++index];
        }
    }

    /// <summary>
    /// Scanning a large installation takes a while; this shows it is happening, on standard
    /// error so a listing piped elsewhere stays clean, and only when there is a terminal to
    /// redraw a line on.
    /// </summary>
    internal static class ScanProgress
    {
        private static bool shown;
        private static int scansBefore;

        internal static IProgress<int> Create()
        {
            shown = false;
            scansBefore = ContentStore.ScanCount;
            return new Progress<int>(percent =>
            {
                if (Console.IsErrorRedirected)
                    return;
                shown = true;
                Console.Error.Write($"\rScanning content... {percent,3}%");
            });
        }

        internal static void Done()
        {
            if (shown && !Console.IsErrorRedirected)
                Console.Error.WriteLine("\rScanning content... done");

            // Only after an actual scan: a load that read the cache skipped nothing new.
            if (ContentStore.ScanCount != scansBefore && ContentStore.LastScanSkipped.Count > 0)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("Some content could not be read. Everything else loaded; these will not work until fixed:");
                foreach (var file in ContentStore.LastScanSkipped.OrderBy(file => file.Kind).ThenBy(file => file.Path))
                    Console.Error.WriteLine($"  {file.Kind,-13} {file.Path}{Environment.NewLine}  {"",-13} {file.Reason}");
                Console.Error.WriteLine();
            }
            shown = false;
        }
    }
}
