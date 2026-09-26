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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Platform.Storage;

using FreeTrainSimulator.Common.Native;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;

using Orts.Formats.Msts;

using static Riel.Launcher.Gui.Translation;

namespace Riel.Launcher.Gui
{
    /// <summary>A content folder as the list shows it.</summary>
    public sealed record FolderItem(string Name, string Path);

    /// <summary>
    /// Adds, removes and rescans content folders.
    /// </summary>
    public partial class ContentWindow : Window
    {
        private readonly bool browseFirst;
        private readonly CancellationTokenSource closing = new CancellationTokenSource();

        /// <summary>The name last filled in for the user, so a name they typed is not replaced.</summary>
        private string suggestedName;

        public ContentWindow() : this(false)
        {
        }

        public ContentWindow(bool browseFirst)
        {
            this.browseFirst = browseFirst;
            InitializeComponent();

            BrowseButton.IsVisible = StorageProvider.CanPickFolder;
            BrowseButton.Click += (_, _) => Guarded(Browse);
            AddButton.Click += (_, _) => Guarded(Add);
            RemoveButton.Click += (_, _) => Guarded(Remove);
            RescanButton.Click += (_, _) => Guarded(Rescan);
            CloseButton.Click += (_, _) => Close();
            PathBox.TextChanged += (_, _) => PathChanged();
            FolderList.SelectionChanged += (_, _) => UpdateButtons();

            Opened += (_, _) => Guarded(async () =>
            {
                await Load(null);
                if (this.browseFirst && StorageProvider.CanPickFolder)
                    await Browse();
            });
            Closed += (_, _) => closing.Cancel();
            UpdateButtons();
        }

        /// <summary>Whether anything was added, removed or rescanned, so the caller should reload.</summary>
        public bool Changed { get; private set; }

        private bool working;

        private async Task Load(ContentModel content)
        {
            content ??= await Work(T("Reading content…"), progress => ContentStore.Load(progress, closing.Token));
            List<FolderItem> folders = content.ContentFolders
                .Select(folder => new FolderItem(folder.Name, folder.ContentPath))
                .OrderBy(folder => folder.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            FolderList.ItemsSource = folders;
            NoFolders.IsVisible = folders.Count == 0;
            UpdateButtons();
        }

        private async Task Browse()
        {
            IReadOnlyList<IStorageFolder> picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = T("Choose the Microsoft Train Simulator folder"),
                AllowMultiple = false,
            });
            string path = picked.Count > 0 ? picked[0].TryGetLocalPath() : null;
            if (!string.IsNullOrEmpty(path))
                PathBox.Text = path;
        }

        private void PathChanged()
        {
            string path = ExpandHome(PathBox.Text?.Trim());
            if (string.IsNullOrEmpty(NameBox.Text) || NameBox.Text == suggestedName)
            {
                suggestedName = string.IsNullOrEmpty(path) ? null : ContentStore.SuggestName(path);
                NameBox.Text = suggestedName;
            }
            UpdateButtons();
        }

        private async Task Add()
        {
            string requested = ExpandHome(PathBox.Text?.Trim());
            if (string.IsNullOrEmpty(requested))
                return;

            string folder = ContentIO.ResolveDirectory(requested);
            if (folder == null)
            {
                await Dialogs.Message(this, T("That folder does not exist"), F("Nothing was found at {0}. Check the path, and that the disk it is on is mounted.", requested));
                return;
            }

            // The folder people pick is often one level off: Microsoft Games rather than the
            // Train Simulator inside it. When the installation is right below, use that.
            string installation = MstsInstallation.Validate(folder) ?? FindInstallationBelow(folder);
            if (installation == null)
            {
                bool addAnyway = await Dialogs.Confirm(this,
                    T("This does not look like an installation"),
                    F("{0} has no ROUTES and GLOBAL folders, so its routes would not load. Add it anyway?", folder),
                    T("Add anyway"), T("Cancel"));
                if (!addAnyway)
                    return;
                installation = folder;
            }
            else if (!string.Equals(installation, folder, StringComparison.Ordinal))
            {
                StatusText.Text = F("Using {0}, the installation inside the folder chosen.", installation);
            }

            string name = string.IsNullOrWhiteSpace(NameBox.Text) || NameBox.Text == suggestedName
                ? ContentStore.SuggestName(installation)
                : NameBox.Text.Trim();

            ContentModel content = await Work(F("Scanning {0}…", name), progress => ContentStore.AddFolder(name, installation, progress, closing.Token));
            Changed = true;
            PathBox.Text = string.Empty;
            NameBox.Text = suggestedName = null;
            await Load(content);

            FolderModel added = content.ContentFolders.FirstOrDefault(item => item.Name == name);
            int routes = added == null ? 0 : (await added.GetRoutes(closing.Token)).Length;
            StatusText.Text = routes > 0
                ? F("Added {0}: {1}.", name, Count(routes, "{0} route", "{0} routes"))
                : F("Added {0}, but no routes were found in it.", name);
            if (ContentStore.LastScanSkipped.Count > 0)
                await Dialogs.Problems(this, ContentStore.LastScanSkipped);
        }

        private async Task Remove()
        {
            if (FolderList.SelectedItem is not FolderItem folder)
                return;

            bool remove = await Dialogs.Confirm(this,
                F("Remove {0}?", folder.Name),
                T("Only the entry is removed. The folder and everything in it stay on disk."),
                T("Remove"), T("Cancel"));
            if (!remove)
                return;

            ContentModel content = await Work(T("Removing…"), progress => ContentStore.RemoveFolder(folder.Name, progress, closing.Token));
            Changed = true;
            await Load(content);
            StatusText.Text = F("Removed {0}.", folder.Name);
        }

        private async Task Rescan()
        {
            ContentModel content = await Work(T("Scanning every folder…"), progress => ContentStore.Refresh(progress, closing.Token));
            Changed = true;
            await Load(content);
            StatusText.Text = T("Scanned again.");
            if (ContentStore.LastScanSkipped.Count > 0)
                await Dialogs.Problems(this, ContentStore.LastScanSkipped);
        }

        /// <summary>
        /// Runs a content operation with the progress bar showing and the buttons held, so a
        /// scan of a large installation is visibly under way rather than a frozen window.
        /// </summary>
        private async Task<T> Work<T>(string status, Func<IProgress<int>, Task<T>> operation)
        {
            working = true;
            UpdateButtons();
            StatusText.Text = status;
            Progress.IsVisible = true;
            Progress.IsIndeterminate = true;
            try
            {
                return await operation(new Progress<int>(percent =>
                {
                    Progress.IsIndeterminate = false;
                    Progress.Value = percent;
                }));
            }
            finally
            {
                Progress.IsVisible = false;
                StatusText.Text = string.Empty;
                working = false;
                UpdateButtons();
            }
        }

        private void UpdateButtons()
        {
            AddButton.IsEnabled = !working && !string.IsNullOrWhiteSpace(PathBox.Text);
            BrowseButton.IsEnabled = !working;
            RemoveButton.IsEnabled = !working && FolderList.SelectedItem != null;
            RescanButton.IsEnabled = !working && FolderList.ItemCount > 0;
        }

        private static string FindInstallationBelow(string folder)
        {
            try
            {
                return ContentIO.EnumerateDirectories(folder)
                    .Select(MstsInstallation.Validate)
                    .FirstOrDefault(installation => installation != null);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static string ExpandHome(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            if (path == "~")
                return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (path.StartsWith("~/", StringComparison.Ordinal))
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
            // file:// URIs are what a file manager puts on the clipboard.
            if (path.StartsWith("file://", StringComparison.Ordinal))
                return Uri.UnescapeDataString(new Uri(path).LocalPath);
            return path;
        }

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
#pragma warning disable CA1031 // Top of a UI action; showing the failure beats losing it.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                await Dialogs.Message(this, T("Something went wrong"), ex.Message, ex.ToString());
            }
        }
    }
}
