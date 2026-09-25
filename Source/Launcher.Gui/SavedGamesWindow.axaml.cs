// COPYRIGHT 2026 by the Riel project.
// This file is part of Riel and is distributed under the GNU General Public License v3 or later.

using System;
using System.Globalization;
using System.IO;
using System.Linq;

using Avalonia.Controls;
using Avalonia.Media.Imaging;

using Riel.Launcher;

namespace Riel.Launcher.Gui
{
    public sealed class SavedGameItem
    {
        public SavedGameItem(FileInfo file) => File = file;

        public FileInfo File { get; }
        public string FileName => File.Name;
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
        private Bitmap preview;

        public SavedGamesWindow()
        {
            InitializeComponent();
            SaveList.ItemsSource = Selections.SavedGames().Select(file => new SavedGameItem(file)).ToArray();
            SaveList.SelectionChanged += (_, _) => ShowSelection();
            SaveList.DoubleTapped += (_, _) => Accept();
            ContinueButton.Click += (_, _) => Accept();
            CancelButton.Click += (_, _) => Close(null);
            Closed += (_, _) => preview?.Dispose();
        }

        private void ShowSelection()
        {
            Screenshot.Source = null;
            preview?.Dispose();
            preview = null;
            SavedGameItem chosen = SaveList.SelectedItem as SavedGameItem;
            ContinueButton.IsEnabled = chosen != null;
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
            if (SaveList.SelectedItem is SavedGameItem chosen)
                Close(chosen.File.FullName);
        }
    }
}
