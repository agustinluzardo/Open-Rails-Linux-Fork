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

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using FreeTrainSimulator.Models.Imported.ImportHandler;

using static Riel.Launcher.Gui.Translation;

namespace Riel.Launcher.Gui
{
    /// <summary>The message and question boxes Avalonia leaves to the application.</summary>
    internal static class Dialogs
    {
        /// <summary>Tells the user something, with the technical detail folded away if given.</summary>
        public static Task Message(Window owner, string title, string message, string details = null)
        {
            Window dialog = Build(title, message, details, out StackPanel buttons);
            Button ok = new Button { Content = T("OK"), IsDefault = true, IsCancel = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            ok.Click += (_, _) => dialog.Close();
            buttons.Children.Add(ok);
            return dialog.ShowDialog(owner);
        }

        /// <summary>Asks a yes or no question; true for yes.</summary>
        public static async Task<bool> Confirm(Window owner, string title, string message, string yes, string no)
        {
            Window dialog = Build(title, message, null, out StackPanel buttons);
            bool answer = false;
            Button cancel = new Button { Content = no, IsCancel = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            Button accept = new Button { Content = yes, IsDefault = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            accept.Classes.Add("accent");
            cancel.Click += (_, _) => dialog.Close();
            accept.Click += (_, _) =>
            {
                answer = true;
                dialog.Close();
            };
            buttons.Children.Add(cancel);
            buttons.Children.Add(accept);
            await dialog.ShowDialog(owner);
            return answer;
        }

        /// <summary>Lists the content a scan could not read, and why.</summary>
        public static Task Problems(Window owner, IReadOnlyList<SkippedFile> problems)
        {
            System.Text.StringBuilder details = new System.Text.StringBuilder();
            foreach (SkippedFile problem in problems.OrderBy(problem => problem.Kind).ThenBy(problem => problem.Path))
            {
                details.AppendLine($"{Kind(problem.Kind)}: {problem.Path}");
                details.AppendLine($"    {problem.Reason}");
            }

            string first = problems.Count > 0 ? $"{Kind(problems[0].Kind)} {System.IO.Path.GetFileName(problems[0].Path)}: {problems[0].Reason}" : string.Empty;
            return Message(owner,
                T("Some content could not be read"),
                F("Everything else loaded. These will not work until the files are fixed or replaced; the first one: {0}", first),
                details.ToString().TrimEnd());
        }

        private static string Kind(string kind) => kind switch
        {
            "route" => T("route"),
            "path" => T("path"),
            "consist" => T("train"),
            "rolling stock" => T("rolling stock"),
            "activity" => T("activity"),
            "timetable" => T("timetable"),
            _ => kind,
        };

        private static Window Build(string title, string message, string details, out StackPanel buttons)
        {
            buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };

            StackPanel body = new StackPanel { Spacing = 12, Margin = new Thickness(24, 20) };
            TextBlock heading = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap };
            heading.Classes.Add("heading");
            body.Children.Add(heading);
            body.Children.Add(new SelectableTextBlock { Text = message, TextWrapping = TextWrapping.Wrap });

            if (!string.IsNullOrEmpty(details))
            {
                TextBox detailBox = new TextBox
                {
                    Text = details,
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.NoWrap,
                    AcceptsReturn = true,
                    MaxHeight = 220,
                };
                detailBox.Classes.Add("mono");
                body.Children.Add(new Expander { Header = T("Details"), Content = detailBox, HorizontalAlignment = HorizontalAlignment.Stretch });
            }
            body.Children.Add(buttons);

            return new Window
            {
                Title = title,
                Width = 520,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
                Content = body,
            };
        }
    }
}
