// COPYRIGHT 2026 by the Riel project.
// This file is part of Riel and is distributed under the GNU General Public License v3 or later.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;

using Riel.Common;
using Riel.Common.Info;
using Riel.Models.Content;
using Riel.Models.Settings;
using Riel.Models.Shim;

using Riel.Launcher;

using static Riel.Launcher.Gui.Translation;

namespace Riel.Launcher.Gui
{
    public sealed class TestingActivityItem : INotifyPropertyChanged
    {
        private string result;
        private string errors;
        private string load;
        private string fps;

        public TestingActivityItem(TestActivityModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            Result = model.Tested ? model.Passed ? T("Passed") : T("Failed") : string.Empty;
            Errors = model.Errors ?? string.Empty;
            Load = model.Load ?? string.Empty;
            FPS = model.FPS ?? string.Empty;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public TestActivityModel Model { get; }
        public string Route => Model.Route;
        public string Activity => Model.Activity;

        public string Result
        {
            get => result;
            set { result = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Result))); }
        }

        public string Errors
        {
            get => errors;
            set { errors = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Errors))); }
        }

        public string Load
        {
            get => load;
            set { load = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Load))); }
        }

        public string FPS
        {
            get => fps;
            set { fps = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FPS))); }
        }
    }

    public partial class TestingWindow : Window
    {
        private const string LogFileName = "TestingLog.txt";

        private readonly ContentModel contentModel;
        private readonly ProfileUserSettingsModel userSettings;
        private readonly CancellationTokenSource closing = new CancellationTokenSource();
        private CancellationTokenSource testRun;
        private List<TestingActivityItem> activities = new List<TestingActivityItem>();
        private readonly string summaryFilePath = Path.Combine(RuntimeInfo.UserDataFolder, "TestingSummary.csv");
        private readonly string logFilePath;
        private bool clearedLogs;
        private bool running;

        public TestingWindow(ContentModel contentModel, ProfileUserSettingsModel userSettings)
        {
            this.contentModel = contentModel ?? throw new ArgumentNullException(nameof(contentModel));
            this.userSettings = userSettings ?? throw new ArgumentNullException(nameof(userSettings));
            logFilePath = Path.Combine(userSettings.LogFilePath, LogFileName);

            InitializeComponent();

            Opened += async (_, _) => await LoadActivities();
            Closed += (_, _) =>
            {
                closing.Cancel();
                testRun?.Cancel();
                testRun?.Dispose();
                closing.Dispose();
            };

            ActivityList.SelectionChanged += (_, _) => UpdateButtons();
            TestSelectedButton.Click += async (_, _) =>
            {
                if (ActivityList.SelectedItem is TestingActivityItem item)
                    await RunTests(new[] { item });
            };
            TestAllButton.Click += async (_, _) => await RunTests(activities);
            CancelTestButton.Click += (_, _) => testRun?.Cancel();
            SummaryButton.Click += (_, _) => OpenFile(summaryFilePath);
            LogButton.Click += (_, _) => OpenFile(logFilePath);
            CloseButton.Click += (_, _) => Close();

            UpdateButtons();
        }

        private async Task LoadActivities()
        {
            running = true;
            UpdateButtons();
            Progress.IsVisible = true;
            Progress.IsIndeterminate = true;
            try
            {
                var loaded = await contentModel.LoadTestActivities(closing.Token);
                activities = loaded.Cast<TestActivityModel>()
                    .OrderBy(activity => activity.DefaultSort, StringComparer.CurrentCultureIgnoreCase)
                    .Select(activity => new TestingActivityItem(activity))
                    .ToList();
                ActivityList.ItemsSource = activities;
                ActivityList.SelectedIndex = activities.Count > 0 ? 0 : -1;
            }
            catch (OperationCanceledException) when (closing.IsCancellationRequested)
            {
            }
            catch (Exception error)
            {
                await Dialogs.Message(this, T("Test activities"), error.Message);
            }
            finally
            {
                Progress.IsIndeterminate = false;
                Progress.IsVisible = false;
                running = false;
                UpdateButtons();
            }
        }

        private async Task RunTests(IEnumerable<TestingActivityItem> source)
        {
            if (running)
                return;

            TestingActivityItem[] items = source?.ToArray() ?? Array.Empty<TestingActivityItem>();
            if (items.Length == 0)
                return;

            testRun?.Dispose();
            testRun = CancellationTokenSource.CreateLinkedTokenSource(closing.Token);
            CancellationToken token = testRun.Token;
            running = true;
            Progress.IsVisible = true;
            Progress.IsIndeterminate = false;
            Progress.Minimum = 0;
            Progress.Maximum = items.Length;
            Progress.Value = 0;
            UpdateButtons();

            try
            {
                int done = 0;
                foreach (TestingActivityItem item in items)
                {
                    token.ThrowIfCancellationRequested();
                    item.Result = T("Testing…");
                    item.Errors = item.Load = item.FPS = string.Empty;

                    TestActivityModel tested;
                    try
                    {
                        tested = await RunTestTask(item.Model, DefaultSettingsBox.IsChecked == true, token);
                    }
                    catch (Exception error) when (error is not OperationCanceledException)
                    {
                        item.Result = T("Failed");
                        item.Errors = error.Message;
                        break;
                    }
                    item.Result = tested.Passed ? T("Passed") : T("Failed");
                    item.Errors = tested.Errors ?? string.Empty;
                    item.Load = tested.Load ?? string.Empty;
                    item.FPS = tested.FPS ?? string.Empty;
                    Progress.Value = ++done;
                    ActivityList.ScrollIntoView(item);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                running = false;
                Progress.IsVisible = false;
                UpdateButtons();
            }
        }

        private async Task<TestActivityModel> RunTestTask(TestActivityModel activity, bool overrideSettings, CancellationToken cancellationToken)
        {
            ProfileModel testingProfile = new ProfileModel(ProfileModel.TestingProfile);

            ProfileUserSettingsModel testingSettings = (overrideSettings ? new ProfileUserSettingsModel() : userSettings) with
            {
                Id = ProfileModel.TestingProfile,
                Name = ProfileModel.TestingProfile,
                LogFileName = LogFileName,
                LogFilePath = userSettings.LogFilePath,
                LogLevel = TraceEventType.Verbose,
                ErrorDialogEnabled = false,
                PauseAtStart = false,
                Profiling = true,
                ProfilingTime = 10,
            };
            await testingProfile.UpdateSettingsModel(testingSettings, cancellationToken);

            ProfileSelectionsModel selections = new ProfileSelectionsModel
            {
                Id = ProfileModel.TestingProfile,
                Name = ProfileModel.TestingProfile,
                ActivityId = activity.Id,
                ActivityType = ActivityType.Activity,
                FolderName = activity.Folder,
                RouteId = activity.Parent.Id,
                GamePlayAction = GamePlayAction.TestActivity,
            };
            await testingProfile.UpdateSettingsModel(selections, cancellationToken);

            Directory.CreateDirectory(RuntimeInfo.UserDataFolder);
            Directory.CreateDirectory(userSettings.LogFilePath);

            if (!clearedLogs)
            {
                await File.WriteAllTextAsync(summaryFilePath,
                    "Route, Activity, Passed, Errors, Warnings, Infos, Load Time, FPS" + Environment.NewLine,
                    cancellationToken);
                try { File.Delete(logFilePath); }
                catch (IOException) { }
                clearedLogs = true;
            }

            long summaryPosition = new FileInfo(summaryFilePath).Length;

            string executable = Path.GetFullPath(Simulator.Locate());
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = Path.GetDirectoryName(executable),
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add($"/Profile={ProfileModel.TestingProfile}");

            (bool passed, string processError) = await RunProcess(startInfo, cancellationToken);
            string errors = string.Empty;
            string load = string.Empty;
            string fps = string.Empty;

            using (FileStream stream = new FileStream(summaryFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                stream.Seek(summaryPosition, SeekOrigin.Begin);
                using StreamReader reader = new StreamReader(stream);
                string line = await reader.ReadLineAsync(cancellationToken);
                if (!string.IsNullOrEmpty(line))
                {
                    string[] csv = line.Split(',');
                    if (csv.Length >= 8)
                    {
                        passed &= string.Equals(csv[2].Trim(), "Yes", StringComparison.OrdinalIgnoreCase);
                        errors = $"{csv[3]}/{csv[4]}/{csv[5]}";
                        if (float.TryParse(csv[6], NumberStyles.Float, CultureInfo.InvariantCulture, out float loadSeconds))
                            load = $"{loadSeconds:F1}s";
                        if (float.TryParse(csv[7], NumberStyles.Float, CultureInfo.InvariantCulture, out float frameRate))
                            fps = $"{frameRate:F1}";
                    }
                    else
                    {
                        passed = false;
                        errors = T("The test summary is incomplete. Check the test log.");
                    }
                }
                else
                {
                    passed = false;
                    errors = string.IsNullOrWhiteSpace(processError)
                        ? T("The simulator did not write a test summary. Check the test log.")
                        : processError;
                }
            }

            return activity with { Tested = true, Passed = passed, Errors = errors, Load = load, FPS = fps };
        }

        private static async Task<(bool Passed, string Error)> RunProcess(ProcessStartInfo startInfo, CancellationToken cancellationToken)
        {
            using Process process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            if (!process.Start())
                return (false, T("The simulator could not start."));

            // Drain stderr during the test so a full pipe cannot block the simulator from exiting.
            Task<string> errorTask = process.StandardError.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) { } // The process exited after the check.
                await process.WaitForExitAsync(CancellationToken.None);
                throw;
            }
            string error = (await errorTask).Trim();
            if (process.ExitCode != 0 && error.Length > 0)
                Trace.TraceWarning(error);
            return (process.ExitCode == 0, error.Length > 240 ? error.Substring(error.Length - 240) : error);
        }

        private static void OpenFile(string file)
        {
            if (File.Exists(file))
                SystemInfo.OpenFile(file);
        }

        private void UpdateButtons()
        {
            TestSelectedButton.IsEnabled = !running && ActivityList.SelectedItem != null;
            TestAllButton.IsEnabled = !running && activities.Count > 0;
            CancelTestButton.IsEnabled = running && testRun != null && !testRun.IsCancellationRequested;
            SummaryButton.IsEnabled = !running && File.Exists(summaryFilePath);
            LogButton.IsEnabled = !running && File.Exists(logFilePath);
            CloseButton.IsEnabled = !running;
        }
    }
}
