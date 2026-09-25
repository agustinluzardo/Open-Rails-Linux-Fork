// COPYRIGHT 2026 by the Riel project.
// This file is part of Riel and is distributed under the GNU General Public License v3 or later.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Media.Imaging;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Imported.State;

using Riel.Launcher;

using static Riel.Launcher.Gui.Translation;

namespace Riel.Launcher.Gui
{
    public sealed class SavedGameItem : INotifyPropertyChanged
    {
        public SavedGameItem(FileInfo file) => File = file;

        public event PropertyChangedEventHandler PropertyChanged;
        public FileInfo File { get; }
        public string FileName => File.Name;
        private string summary = T("Reading save details…");
        public string Summary
        {
            get => summary;
            set
            {
                summary = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
            }
        }
        public string Details { get; set; } = T("Reading save details…");
        public bool CanResume { get; set; } = true;
        public bool DetailsLoaded { get; set; }
        public string Name
        {
            get
            {
                // Open Rails names saves after the activity or route followed by a timestamp.
                // Keep the original file name visible too, so similarly named runs stay distinct.
                string stem = Path.GetFileNameWithoutExtension(File.Name);
                int timestamp = stem.LastIndexOf(" 20", StringComparison.Ordinal);
                return timestamp > 0 ? stem.Substring(0, timestamp) : stem;
            }
        }
        public string SavedAt => File.LastWriteTime.ToString("f", CultureInfo.CurrentCulture);
    }

    public partial class SavedGamesWindow : Window
    {
        private readonly IReadOnlyList<RouteItem> routes;
        private readonly List<SavedGameItem> saves;
        private readonly CancellationTokenSource closing = new CancellationTokenSource();
        private Bitmap preview;

        public SavedGamesWindow(IReadOnlyList<RouteItem> routes)
        {
            this.routes = routes;
            InitializeComponent();
            saves = Selections.SavedGames().Select(file => new SavedGameItem(file)).ToList();
            SaveList.ItemsSource = saves;
            SaveList.SelectionChanged += (_, _) => ShowSelection();
            SaveList.DoubleTapped += (_, _) => Accept();
            ContinueButton.Click += (_, _) => Accept();
            DeleteButton.Click += async (_, _) => await DeleteSelected();
            RestoreButton.Click += async (_, _) => await RestoreDeleted();
            CancelButton.Click += (_, _) => Close(null);
            Opened += async (_, _) => await LoadDetails();
            Closed += (_, _) =>
            {
                closing.Cancel();
                Screenshot.Source = null;
                preview?.Dispose();
            };
            RefreshRestoreButton();
        }

        private async Task LoadDetails()
        {
            // Reading a complete simulator snapshot is expensive. Read sequentially off the UI
            // thread; the list, preview, and buttons remain usable while details arrive.
            foreach (SavedGameItem item in saves.ToArray())
            {
                if (closing.IsCancellationRequested)
                    return;
                if (item.DetailsLoaded || !item.File.Exists)
                    continue;
                try
                {
                    GameSaveState state = await Task.Run(async () =>
                        await GameSaveState.FromFile<GameSaveState>(item.File.FullName, closing.Token), closing.Token);
                    if (closing.IsCancellationRequested)
                        return;

                    string folder = state.ProfileSelections?.FolderName;
                    string routeId = state.Route ?? state.ProfileSelections?.RouteId;
                    RouteItem route = routes.FirstOrDefault(candidate =>
                        string.Equals(candidate.Route.Id, routeId, StringComparison.OrdinalIgnoreCase) &&
                        (folder == null || string.Equals(candidate.Folder.Name, folder, StringComparison.OrdinalIgnoreCase)));
                    string routeName = route?.Name ?? routeId ?? T("Unknown route");
                    string train = state.ProfileSelections?.WagonSetId ?? state.ProfileSelections?.TimetableTrain;
                    if (string.IsNullOrWhiteSpace(train))
                        train = state.SimulatorSaveState?.Trains?.FirstOrDefault()?.TrainName;
                    if (string.IsNullOrWhiteSpace(train))
                        train = state.SimulatorSaveState?.Trains?.FirstOrDefault()?.TrainCars?.FirstOrDefault()?.OriginalConsist;
                    if (string.IsNullOrWhiteSpace(train))
                        train = T("Unknown train");
                    string path = state.Path ?? state.ProfileSelections?.PathId ?? state.ProfileSelections?.ActivityId;
                    string when = TimeSpan.FromSeconds(state.GameTime).ToString(@"hh\:mm\:ss", CultureInfo.CurrentCulture);
                    item.Summary = $"{routeName} · {train} · {when}";
                    item.Details = $"{T("Route")}: {routeName}\n{T("Train")}: {train}\n" +
                        (string.IsNullOrWhiteSpace(path) ? string.Empty : $"{T("Path")}: {path}\n") +
                        $"{T("Game time")}: {when}\n{T("Save version")}: {state.GameVersion}";
                    item.CanResume = state.Valid != false;
                    if (state.Valid == false)
                        item.Details += "\n" + T("This save is incomplete and cannot be continued.");
                }
                catch (OperationCanceledException) when (closing.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    item.Summary = T("Save details unavailable");
                    item.Details = F("Could not read this save: {0}", ex.Message);
                    item.CanResume = false;
                }
                item.DetailsLoaded = true;
                if (SaveList.SelectedItem == item)
                    ShowSelection();
            }
        }

        private void ShowSelection()
        {
            Screenshot.Source = null;
            preview?.Dispose();
            preview = null;
            SavedGameItem chosen = SaveList.SelectedItem as SavedGameItem;
            ContinueButton.IsEnabled = chosen?.CanResume == true;
            DeleteButton.IsEnabled = chosen != null;
            SelectedDetails.Text = chosen?.Details ?? string.Empty;
            SelectedFile.Text = chosen?.File.FullName ?? string.Empty;

            string screenshot = chosen == null ? null : Path.ChangeExtension(chosen.File.FullName, ".png");
            if (screenshot != null)
            {
                try
                {
                    if (File.Exists(screenshot))
                        Screenshot.Source = preview = new Bitmap(screenshot);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
                {
                    // A missing or unreadable screenshot must not prevent resuming the save.
                }
            }
            Screenshot.IsVisible = preview != null;
            NoScreenshot.IsVisible = preview == null;
        }

        private void Accept()
        {
            if (SaveList.SelectedItem is SavedGameItem chosen && chosen.CanResume)
                Close(chosen.File.FullName);
        }

        private async Task DeleteSelected()
        {
            if (SaveList.SelectedItem is not SavedGameItem chosen)
                return;
            if (!await Dialogs.Confirm(this, T("Delete saved game?"),
                F("Move {0} and its screenshot and replay to Deleted Saves? You can restore them later.", chosen.File.Name),
                T("Delete save"), T("Cancel")))
                return;

            try
            {
                string target = RuntimeInfo.DeletedSaveFolder;
                Directory.CreateDirectory(target);
                string source = chosen.File.FullName;
                Screenshot.Source = null;
                preview?.Dispose();
                preview = null;
                string[] companions = { ".png", ".replay", ".txt", ".evaluation.txt" };
                string[] files = companions.Select(extension => Path.ChangeExtension(source, extension))
                    .Where(File.Exists).Append(source).ToArray();
                if (files.Any(file => File.Exists(Path.Combine(target, Path.GetFileName(file)))))
                    throw new IOException(T("A deleted save with the same name already exists. Restore it first."));
                // Move the save last: a failed companion move cannot hide a playable save.
                foreach (string file in files)
                    File.Move(file, Path.Combine(target, Path.GetFileName(file)));
                RefreshSaves();
                RefreshRestoreButton();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                await Dialogs.Message(this, T("Could not delete saved game"), ex.Message);
            }
        }

        private async Task RestoreDeleted()
        {
            try
            {
                if (!Directory.Exists(RuntimeInfo.DeletedSaveFolder))
                    return;
                int restored = 0;
                foreach (string file in Directory.EnumerateFiles(RuntimeInfo.DeletedSaveFolder))
                {
                    string destination = Path.Combine(RuntimeInfo.UserDataFolder, Path.GetFileName(file));
                    if (File.Exists(destination))
                        continue;
                    File.Move(file, destination);
                    restored++;
                }
                RefreshSaves();
                RefreshRestoreButton();
                await LoadDetails();
                await Dialogs.Message(this, T("Saves restored"), F("Restored {0} files.", restored));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                await Dialogs.Message(this, T("Could not restore saved games"), ex.Message);
            }
        }

        private void RefreshSaves()
        {
            SavedGameItem[] updated = Selections.SavedGames().Select(file =>
                saves.FirstOrDefault(item => item.File.FullName == file.FullName) ?? new SavedGameItem(file)).ToArray();
            saves.Clear();
            saves.AddRange(updated);
            SaveList.ItemsSource = updated;
        }

        private void RefreshRestoreButton() => RestoreButton.IsEnabled =
            Directory.Exists(RuntimeInfo.DeletedSaveFolder) &&
            Directory.EnumerateFiles(RuntimeInfo.DeletedSaveFolder).Any();
    }
}
