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
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Interactivity;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Native;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.ImportHandler;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;

using Riel.Launcher;

using static Riel.Launcher.Gui.Translation;

namespace Riel.Launcher.Gui
{
    /// <summary>
    /// Pick a route, then an activity or a path and a train, and play.
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int ActivityTab = 0;
        private const int ExploreTab = 1;
        private const int TimetableTab = 2;

        private readonly CancellationTokenSource closing = new CancellationTokenSource();

        private ContentModel content;
        private List<RouteItem> routes = new List<RouteItem>();
        private List<ConsistItem> consists = new List<ConsistItem>();
        private FolderModel consistsFolder;
        private string detectedInstallation;

        private IReadOnlyList<SkippedFile> problems = Array.Empty<SkippedFile>();

        private bool running;
        private bool hasSave;
        private bool suppressRouteChanged;

        /// <summary>
        /// Counts route changes, so the answer to an earlier one that arrives late - its lists read
        /// from disk after the user had already moved on - is recognised and dropped.
        /// </summary>
        private int routeGeneration;

        public MainWindow()
        {
            InitializeComponent();

            SeasonBox.ItemsSource = Enum.GetValues<SeasonType>().Select(season => new Choice<SeasonType>(season, Names.Season(season))).ToList();
            WeatherBox.ItemsSource = Enum.GetValues<WeatherType>().Select(weather => new Choice<WeatherType>(weather, Names.Weather(weather))).ToList();
            SelectChoice(SeasonBox, SeasonType.Summer);
            SelectChoice(WeatherBox, WeatherType.Clear);
            TimetableSeasonBox.ItemsSource = Enum.GetValues<SeasonType>().Select(season => new Choice<SeasonType>(season, Names.Season(season))).ToList();
            TimetableWeatherBox.ItemsSource = Enum.GetValues<WeatherType>().Select(weather => new Choice<WeatherType>(weather, Names.Weather(weather))).ToList();
            TimetableDayBox.ItemsSource = Enum.GetValues<DayOfWeek>().Select(day => new Choice<DayOfWeek>(day, T(day.ToString()))).ToList();
            SelectChoice(TimetableSeasonBox, SeasonType.Summer);
            SelectChoice(TimetableWeatherBox, WeatherType.Clear);
            SelectChoice(TimetableDayBox, DayOfWeek.Monday);

            RouteFilter.TextChanged += (_, _) => ApplyRouteFilter();
            RouteList.SelectionChanged += (_, _) =>
            {
                if (!suppressRouteChanged)
                    Guarded(RouteChanged);
            };
            ActivityList.SelectionChanged += (_, _) => ActivityChanged();
            ActivityList.DoubleTapped += (_, _) => Guarded(Play);
            ConsistFilter.TextChanged += (_, _) => ApplyConsistFilter();
            ConsistList.SelectionChanged += (_, _) => UpdateButtons();
            ConsistList.DoubleTapped += (_, _) => Guarded(Play);
            PathBox.SelectionChanged += (_, _) => UpdateButtons();
            ModeTabs.SelectionChanged += (_, _) => UpdateButtons();
            TimetableBox.SelectionChanged += (_, _) => TimetableChanged();
            TimetableTrainList.SelectionChanged += (_, _) => UpdateButtons();
            TimetableTrainList.DoubleTapped += (_, _) => Guarded(Play);
            TimetableDayBox.SelectionChanged += (_, _) => UpdateButtons();
            TimetableSeasonBox.SelectionChanged += (_, _) => UpdateButtons();
            TimetableWeatherBox.SelectionChanged += (_, _) => UpdateButtons();
            TimetableWeatherFileBox.SelectionChanged += (_, _) => UpdateButtons();

            PlayButton.Click += (_, _) => Guarded(Play);
            ResumeButton.Click += (_, _) => Guarded(Resume);
            ContentButton.Click += (_, _) => Guarded(() => ManageContent(browseFirst: false));
            GetContentButton.Click += (_, _) => SystemInfo.OpenBrowser("https://www.openrails.org/download/content/");
            ManualButton.Click += (_, _) => SystemInfo.OpenBrowser("https://www.openrails.org/learn/documents/");
            TestButton.Click += (_, _) => Guarded(ShowTesting);
            AddFirstFolderButton.Click += (_, _) => Guarded(() => ManageContent(browseFirst: true));
            UseDetectedButton.Click += (_, _) => Guarded(AddDetected);
            DoctorButton.Click += (_, _) => Guarded(() => new DiagnosticsWindow().ShowDialog(this));
            SettingsButton.Click += (_, _) => Guarded(ShowSettings);
            ProblemsButton.Click += (_, _) => Guarded(() => Dialogs.Problems(this, problems));

            Opened += (_, _) => Guarded(async () =>
            {
                await Reload(restoreSelections: true);
                if (Program.StartupReportPath != null)
                    await ShowCompletedRun(LaunchSession.ReadReport(Program.StartupReportPath));
            });
            Closed += (_, _) => closing.Cancel();

            DarkSwitch.IsChecked = Appearance.Dark;
            DarkSwitch.IsCheckedChanged += (_, _) => Appearance.Set(DarkSwitch.IsChecked == true);

            UpdateButtons();
        }

        private async Task ShowSettings()
        {
            ProfileModel profile = await ((ProfileModel)null).Current(closing.Token);
            SettingsWindow window = new SettingsWindow(profile);
            await window.ShowDialog(this);
        }

        private async Task ShowTesting()
        {
            if (content == null)
                return;

            ProfileModel profile = await ((ProfileModel)null).Current(closing.Token);
            ProfileUserSettingsModel settings = await profile.LoadSettingsModel<ProfileUserSettingsModel>(closing.Token)
                ?? new ProfileUserSettingsModel();
            await new TestingWindow(content, settings).ShowDialog(this);
        }

        // ----------------------------------------------------------------------------- content

        private async Task Reload(bool restoreSelections)
        {
            RouteItem previous = RouteList.SelectedItem as RouteItem;

            using (Busy(T("Reading content…")))
            {
                content = await ContentStore.Load(ScanProgress(), closing.Token);

                List<RouteItem> found = new List<RouteItem>();
                foreach (FolderModel folder in content.ContentFolders)
                {
                    foreach (RouteModelHeader route in await folder.GetRoutes(closing.Token))
                        found.Add(new RouteItem(folder, route));
                }
                routes = found.OrderBy(route => route.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                consistsFolder = null;
            }

            bool empty = content.ContentFolders.Length == 0;
            EmptyState.IsVisible = empty;
            MainArea.IsVisible = !empty;
            if (empty)
                ShowDetectedInstallation();

            hasSave = Selections.HasSavesOrDeletedSaves();
            ShowProblems();
            ApplyRouteFilter();

            if (restoreSelections)
                await Restore();
            else
                await SelectRoute((previous == null ? null : routes.FirstOrDefault(route => Same(route, previous))) ?? routes.FirstOrDefault());

            StatusText.Text = empty
                ? string.Empty
                : routes.Count == 0
                    ? T("No routes were found in the content folders. Check that each one is the folder holding ROUTES and GLOBAL.")
                    : F("{0} in {1}.",
                        Count(routes.Count, "{0} route", "{0} routes"),
                        Count(content.ContentFolders.Length, "{0} content folder", "{0} content folders"));
            UpdateButtons();
        }

        /// <summary>
        /// Shows, beside the status, what the last scan in this session could not read. A scan
        /// that read the cache instead has nothing new to say, so the last real one stands.
        /// </summary>
        private void ShowProblems()
        {
            if (ContentStore.ScanCount > 0)
                problems = ContentStore.LastScanSkipped;
            ProblemsButton.IsVisible = problems.Count > 0;
            ProblemsButton.Content = problems.Count == 1 ? T("1 problem") : F("{0} problems", problems.Count);
        }

        private void ShowDetectedInstallation()
        {
            try
            {
                string candidate = Orts.Formats.Msts.FolderStructure.MstsFolder;
                detectedInstallation = ContentIO.DirectoryExists(candidate) ? ContentIO.ResolveDirectory(candidate) : null;
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                detectedInstallation = null;
            }

            DetectedHint.IsVisible = UseDetectedButton.IsVisible = detectedInstallation != null;
            if (detectedInstallation != null)
                DetectedHint.Text = F("An installation was found at {0}", detectedInstallation);
        }

        private async Task AddDetected()
        {
            if (detectedInstallation == null)
                return;
            using (Busy(T("Adding the folder…")))
                _ = await ContentStore.AddFolder(ContentStore.SuggestName(detectedInstallation), detectedInstallation, ScanProgress(), closing.Token);
            await Reload(restoreSelections: false);
        }

        private async Task ManageContent(bool browseFirst)
        {
            ContentWindow window = new ContentWindow(browseFirst);
            await window.ShowDialog(this);
            if (window.Changed)
                await Reload(restoreSelections: false);
        }

        // ------------------------------------------------------------------------------ routes

        private void ApplyRouteFilter()
        {
            string filter = RouteFilter.Text?.Trim() ?? string.Empty;
            RouteItem selected = RouteList.SelectedItem as RouteItem;

            List<RouteItem> shown = filter.Length == 0
                ? routes
                : routes.Where(route =>
                    route.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                    route.FolderName.Contains(filter, StringComparison.CurrentCultureIgnoreCase)).ToList();

            suppressRouteChanged = true;
            try
            {
                RouteList.ItemsSource = shown;
                RouteList.SelectedItem = selected != null && shown.Contains(selected) ? selected : null;
            }
            finally
            {
                suppressRouteChanged = false;
            }

            // Filtering the selection away leaves nothing to play, and the panel must say so.
            if (selected != null && RouteList.SelectedItem == null)
                Guarded(RouteChanged);
        }

        private async Task SelectRoute(RouteItem route)
        {
            suppressRouteChanged = true;
            try
            {
                RouteList.SelectedItem = route;
            }
            finally
            {
                suppressRouteChanged = false;
            }
            if (route != null)
                RouteList.ScrollIntoView(route);
            await RouteChanged();
        }

        private async Task RouteChanged()
        {
            int generation = ++routeGeneration;
            RouteItem item = RouteList.SelectedItem as RouteItem;

            if (item == null)
            {
                RouteTitle.Text = routes.Count == 0 ? string.Empty : T("Choose a route");
                RouteDescription.Text = string.Empty;
                ActivityList.ItemsSource = null;
                PathBox.ItemsSource = null;
                TimetableBox.ItemsSource = null;
                TimetableTrainList.ItemsSource = null;
                ActivityDescription.Text = string.Empty;
                NoActivities.IsVisible = false;
                UpdateButtons();
                return;
            }

            RouteTitle.Text = item.Name;
            RouteDescription.Text = item.Route.Description?.Trim() ?? string.Empty;

            Task<ImmutableArray<ActivityModelHeader>> activitiesTask = item.Route.GetActivities(closing.Token);
            Task<ImmutableArray<PathModelHeader>> pathsTask = item.Route.GetPaths(closing.Token);
            Task<ImmutableArray<TimetableModel>> timetablesTask = item.Route.GetTimetables(closing.Token);
            Task<ImmutableArray<WeatherModelHeader>> weatherFilesTask = item.Route.GetWeatherFiles(closing.Token);
            ImmutableArray<ActivityModelHeader> activities = await activitiesTask;
            ImmutableArray<PathModelHeader> paths = await pathsTask;
            ImmutableArray<TimetableModel> timetables = await timetablesTask;
            ImmutableArray<WeatherModelHeader> weatherFiles = await weatherFilesTask;

            if (item.Folder != consistsFolder)
            {
                ImmutableArray<WagonSetModel> wagonSets = await item.Folder.GetWagonSets(closing.Token);
                if (generation != routeGeneration)
                    return;
                consists = wagonSets.Select(wagonSet => new ConsistItem(wagonSet))
                    .OrderBy(consist => consist.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                consistsFolder = item.Folder;
                ApplyConsistFilter();
            }

            if (generation != routeGeneration)
                return;

            // The engine lists its explore entries among the activities; they are not the
            // route's activities and the Explore tab is where they belong.
            List<ActivityItem> activityItems = activities
                .Where(activity => activity.ActivityType == ActivityType.Activity)
                .Select(activity => new ActivityItem(activity))
                .OrderBy(activity => activity.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            ActivityList.ItemsSource = activityItems;
            ActivityList.SelectedIndex = activityItems.Count > 0 ? 0 : -1;
            NoActivities.IsVisible = activityItems.Count == 0;

            // Only paths a player can drive belong in the list; a route that marks none still
            // gets them all rather than an empty box.
            List<PathModelHeader> playerPaths = paths.Where(path => path.PlayerPath).ToList();
            List<PathItem> pathItems = (playerPaths.Count > 0 ? playerPaths : paths.ToList())
                .Select(path => new PathItem(path))
                .OrderBy(path => path.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            PathBox.ItemsSource = pathItems;
            PathBox.SelectedIndex = pathItems.Count > 0 ? 0 : -1;

            TimetableBox.ItemsSource = timetables.Select(model => new TimetableItem(model))
                .OrderBy(model => model.ToString(), StringComparer.CurrentCultureIgnoreCase).ToList();
            TimetableBox.SelectedIndex = timetables.Length > 0 ? 0 : -1;
            TimetableWeatherFileBox.ItemsSource = new[] { new WeatherFileItem(null) }
                .Concat(weatherFiles.Select(model => new WeatherFileItem(model))).ToList();
            TimetableWeatherFileBox.SelectedIndex = 0;

            if (activityItems.Count == 0 && ModeTabs.SelectedIndex == ActivityTab)
                ModeTabs.SelectedIndex = ExploreTab;

            ActivityChanged();
        }

        private void TimetableChanged()
        {
            TimetableTrainList.ItemsSource = (TimetableBox.SelectedItem as TimetableItem)?.Model.TimetableTrains
                .Select(model => new TimetableTrainItem(model))
                .OrderBy(model => model.Model.Group).ThenBy(model => model.Model.StartTime).ToList();
            TimetableTrainList.SelectedIndex = TimetableTrainList.ItemsSource is IList<TimetableTrainItem> trains && trains.Count > 0 ? 0 : -1;
            UpdateButtons();
        }

        private void ActivityChanged()
        {
            ActivityDescription.Text = (ActivityList.SelectedItem as ActivityItem)?.Description ?? string.Empty;
            UpdateButtons();
        }

        private void ApplyConsistFilter()
        {
            string filter = ConsistFilter.Text?.Trim() ?? string.Empty;
            ConsistItem selected = ConsistList.SelectedItem as ConsistItem;

            List<ConsistItem> shown = filter.Length == 0
                ? consists
                : consists.Where(consist => consist.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)).ToList();
            ConsistList.ItemsSource = shown;
            ConsistList.SelectedItem = selected != null && shown.Contains(selected) ? selected : shown.FirstOrDefault();
            UpdateButtons();
        }

        // --------------------------------------------------------------------------- selections

        /// <summary>Reopens on what was played last, from here or from the riel command.</summary>
        private async Task Restore()
        {
            ProfileSelectionsModel saved = await Selections.Load(closing.Token);
            RouteItem route = saved == null ? null : routes.FirstOrDefault(item =>
                string.Equals(item.Folder.Name, saved.FolderName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Route.Id, saved.RouteId, StringComparison.OrdinalIgnoreCase));

            await SelectRoute(route ?? routes.FirstOrDefault());
            if (route == null)
                return;

            if (saved.ActivityType == ActivityType.TimeTable)
            {
                ModeTabs.SelectedIndex = TimetableTab;
                TimetableBox.SelectedItem = (TimetableBox.ItemsSource as IEnumerable<TimetableItem>)?
                    .FirstOrDefault(item => string.Equals(item.Model.Id, saved.TimetableSet, StringComparison.OrdinalIgnoreCase));
                TimetableTrainList.SelectedItem = (TimetableTrainList.ItemsSource as IEnumerable<TimetableTrainItem>)?
                    .FirstOrDefault(item => string.Equals(item.Model.Group, saved.TimetableName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(item.Model.Id, saved.TimetableTrain, StringComparison.OrdinalIgnoreCase));
                SelectChoice(TimetableDayBox, saved.TimetableDay);
                SelectChoice(TimetableSeasonBox, saved.Season);
                SelectChoice(TimetableWeatherBox, saved.Weather);
                TimetableWeatherFileBox.SelectedItem = (TimetableWeatherFileBox.ItemsSource as IEnumerable<WeatherFileItem>)?
                    .FirstOrDefault(item => item.Model?.Id == saved.WeatherChanges) ?? TimetableWeatherFileBox.SelectedItem;
            }
            else if (saved.ActivityType is ActivityType.Explorer or ActivityType.ExploreActivity)
            {
                ModeTabs.SelectedIndex = ExploreTab;
                PathBox.SelectedItem = (PathBox.ItemsSource as IEnumerable<PathItem>)?
                    .FirstOrDefault(path => string.Equals(path.Path.Id, saved.PathId, StringComparison.OrdinalIgnoreCase)) ?? PathBox.SelectedItem;
                ConsistItem consist = consists.FirstOrDefault(item => string.Equals(item.Consist.Id, saved.WagonSetId, StringComparison.OrdinalIgnoreCase));
                if (consist != null)
                {
                    ConsistList.SelectedItem = consist;
                    ConsistList.ScrollIntoView(consist);
                }
                TimeBox.Text = saved.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture);
                SelectChoice(SeasonBox, saved.Season);
                SelectChoice(WeatherBox, saved.Weather);
            }
            else
            {
                ModeTabs.SelectedIndex = ActivityTab;
                ActivityItem activity = (ActivityList.ItemsSource as IEnumerable<ActivityItem>)?
                    .FirstOrDefault(item => string.Equals(item.Activity.Id, saved.ActivityId, StringComparison.OrdinalIgnoreCase));
                if (activity != null)
                {
                    ActivityList.SelectedItem = activity;
                    ActivityList.ScrollIntoView(activity);
                }
            }
            UpdateButtons();
        }

        /// <summary>
        /// What the Play button would start, or null with the reason when something is missing.
        /// </summary>
        private (ProfileSelectionsModel Selections, string Title, string Missing) Selected()
        {
            if (RouteList.SelectedItem is not RouteItem route)
                return (null, null, T("Choose a route."));

            if (ModeTabs.SelectedIndex == ActivityTab)
            {
                if (ActivityList.SelectedItem is not ActivityItem activity)
                    return (null, null, T("Choose an activity, or explore the route instead."));
                return (Selections.Activity(route.Folder, route.Route, activity.Activity), $"{route.Name} — {activity.Name}", null);
            }

            if (ModeTabs.SelectedIndex == TimetableTab)
            {
                if (TimetableBox.SelectedItem is not TimetableItem timetable)
                    return (null, null, T("Choose a timetable set."));
                if (TimetableTrainList.SelectedItem is not TimetableTrainItem train)
                    return (null, null, T("Choose a timetable train."));
                DayOfWeek day = (TimetableDayBox.SelectedItem as Choice<DayOfWeek>)?.Value ?? DayOfWeek.Monday;
                SeasonType timetableSeason = (TimetableSeasonBox.SelectedItem as Choice<SeasonType>)?.Value ?? SeasonType.Summer;
                WeatherType timetableWeather = (TimetableWeatherBox.SelectedItem as Choice<WeatherType>)?.Value ?? WeatherType.Clear;
                WeatherModelHeader weatherFile = (TimetableWeatherFileBox.SelectedItem as WeatherFileItem)?.Model;
                return (Selections.Timetable(route.Folder, route.Route, timetable.Model, train.Model, day,
                    timetableSeason, timetableWeather, weatherFile), $"{route.Name} — {train.Name}", null);
            }

            if (PathBox.SelectedItem is not PathItem path)
                return (null, null, T("Choose a path to explore."));
            if (ConsistList.SelectedItem is not ConsistItem consist)
                return (null, null, T("Choose a train to explore with."));
            if (!TimeOnly.TryParse(TimeBox.Text?.Trim(), CultureInfo.InvariantCulture, out TimeOnly time))
                return (null, null, T("Write the time as hours and minutes, like 08:30."));

            SeasonType season = (SeasonBox.SelectedItem as Choice<SeasonType>)?.Value ?? SeasonType.Summer;
            WeatherType weather = (WeatherBox.SelectedItem as Choice<WeatherType>)?.Value ?? WeatherType.Clear;
            return (Selections.Explore(route.Folder, route.Route, path.Path, consist.Consist, time, season, weather),
                $"{route.Name} — {consist.Name}", null);
        }

        private void UpdateButtons()
        {
            (ProfileSelectionsModel selections, _, string missing) = Selected();
            PlayButton.IsEnabled = !running && selections != null;
            ResumeButton.IsEnabled = !running && hasSave;
            ToolTip.SetTip(PlayButton, missing);
        }

        // ------------------------------------------------------------------------------ playing

        private async Task Play()
        {
            if (running)
                return;

            (ProfileSelectionsModel selections, string title, string missing) = Selected();
            if (selections == null)
            {
                await Dialogs.Message(this, T("Nothing to play yet"), missing);
                return;
            }

            await Selections.Save(selections, closing.Token);
            await Run(Selections.Arguments(selections), title, RouteList.SelectedItem as RouteItem);
        }

        private async Task Resume()
        {
            if (running)
                return;
            SavedGameChoice choice = await new SavedGamesWindow(routes).ShowDialog<SavedGameChoice>(this);
            hasSave = Selections.HasSavesOrDeletedSaves();
            UpdateButtons();
            if (choice != null)
                await Run(Selections.SavedGameArguments(choice.File, choice.Action),
                    System.IO.Path.GetFileNameWithoutExtension(choice.File), null);
        }

        /// <summary>
        /// Starts the simulator and waits for it. This window stays as it is meanwhile, with
        /// Play disabled, so there is always something on screen saying what is happening - the
        /// failure this launcher exists to prevent is a click that seems to do nothing.
        /// </summary>
        /// <param name="safeMode">What to leave out: set when the error window's retry buttons run it again.</param>
        private async Task Run(IReadOnlyList<string> arguments, string title, RouteItem route, SafeMode safeMode = SafeMode.None)
        {
            ProfileModel profile = await ((ProfileModel)null).Current(closing.Token);
            ProfileUserSettingsModel settings = await profile.LoadSettingsModel<ProfileUserSettingsModel>(closing.Token);
            if (settings.CloseLauncherWhilePlaying)
            {
                LaunchSession.Start(arguments, title, route?.Route.Id, route?.Folder.Name, safeMode);
                Close();
                return;
            }

            running = true;
            UpdateButtons();
            StatusText.Text = F("Starting {0}…", title);
            string leftOut = safeMode switch
            {
                SafeMode.NoSound => T("without sound"),
                SafeMode.BasicGraphics => T("with basic graphics"),
                SafeMode.None => null,
                _ => T("without sound and with basic graphics"),
            };

            SimulatorOutcome outcome;
            string commandLine;
            try
            {
                using SimulatorRun run = Simulator.Start(arguments, captureErrors: true, safeMode);
                commandLine = run.CommandLine;
                StatusText.Text = leftOut == null
                    ? F("Running {0}. The simulator window may take a moment to appear.", title)
                    : F("Running {0} {1}. The simulator window may take a moment to appear.", title, leftOut);
                outcome = await run.WaitAsync(closing.Token);
            }
            finally
            {
                running = false;
                hasSave = Selections.HasSavesOrDeletedSaves();
                UpdateButtons();
            }

            if (outcome.Failed)
            {
                StatusText.Text = outcome.CrashedNatively ? T("The simulator crashed.") : T("The simulator stopped because of an error.");
                string routeProblem = route == null ? null : problems
                    .FirstOrDefault(problem => problem.Kind == "route" && (problem.Path == route.Name || problem.Path == route.Route.Id))?.Reason;
                SafeMode? retry = await new ErrorWindow(outcome, commandLine, routeProblem, safeMode).ShowDialog<SafeMode?>(this);
                if (retry != null)
                    await Run(arguments, title, route, retry.Value);
            }
            else
            {
                StatusText.Text = leftOut == null
                    ? F("Finished {0}.", title)
                    : F("Finished {0} {1}.", title, leftOut);
            }
        }

        private async Task ShowCompletedRun(LaunchSession.Report report)
        {
            if (report.StartError != null)
            {
                await Dialogs.Message(this, T("Could not start the simulator"), report.StartError);
                return;
            }

            SimulatorOutcome outcome = SimulatorOutcome.ReadCompletedRun(report.ExitCode,
                report.StartedUtc, report.StandardError, report.ProcessId);
            RouteItem route = routes.FirstOrDefault(item =>
                item.Route.Id == report.Request.RouteId && item.Folder.Name == report.Request.FolderName);
            string routeProblem = route == null ? null : problems
                .FirstOrDefault(problem => problem.Kind == "route" &&
                    (problem.Path == route.Name || problem.Path == route.Route.Id))?.Reason;
            SafeMode? retry = await new ErrorWindow(outcome, report.CommandLine,
                routeProblem, report.Request.SafeMode).ShowDialog<SafeMode?>(this);
            if (retry != null)
                await Run(report.Request.Arguments, report.Request.Title, route, retry.Value);
        }

        // ------------------------------------------------------------------------------ helpers

        private static bool Same(RouteItem a, RouteItem b)
        {
            return string.Equals(a.Folder.Name, b.Folder.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.Route.Id, b.Route.Id, StringComparison.OrdinalIgnoreCase);
        }

        private static void SelectChoice<T>(ComboBox box, T value)
        {
            box.SelectedItem = (box.ItemsSource as IEnumerable<Choice<T>>)?.FirstOrDefault(choice => Equals(choice.Value, value));
        }

        /// <summary>Covers the window with a progress bar until disposed.</summary>
        private IDisposable Busy(string text)
        {
            BusyText.Text = text;
            BusyProgress.IsIndeterminate = true;
            BusyOverlay.IsVisible = true;
            return new Release(() => BusyOverlay.IsVisible = false);
        }

        private IProgress<int> ScanProgress()
        {
            return new Progress<int>(percent =>
            {
                BusyProgress.IsIndeterminate = false;
                BusyProgress.Value = percent;
                BusyText.Text = F("Scanning content… {0}%", percent);
            });
        }

        /// <summary>
        /// Runs an event handler and shows what went wrong instead of letting it end the program.
        /// </summary>
        /// <remarks>
        /// Deliberately broad. This is the top of every user action, and a launcher that closes
        /// or does nothing when something fails is exactly what it replaces; whatever the cause,
        /// the user is better served by seeing it.
        /// </remarks>
        private async void Guarded(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (OperationCanceledException) when (closing.IsCancellationRequested)
            {
            }
            catch (LauncherException ex)
            {
                await Dialogs.Message(this, T("That did not work"), ex.Message);
            }
#pragma warning disable CA1031 // Top of a UI action: see remarks.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                await Dialogs.Message(this, T("Something went wrong"), ex.Message, ex.ToString());
            }
        }

        private sealed class Release : IDisposable
        {
            private Action action;

            public Release(Action action)
            {
                this.action = action;
            }

            public void Dispose()
            {
                action?.Invoke();
                action = null;
            }
        }
    }
}
