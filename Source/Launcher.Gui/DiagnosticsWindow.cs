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
using System.Text;
using System.Threading;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;

using static Riel.Launcher.Gui.Translation;

namespace Riel.Launcher.Gui
{
    /// <summary>
    /// "riel doctor" in a window: what this computer has and lacks for running the simulator.
    /// </summary>
    public sealed class DiagnosticsWindow : Window
    {
        private readonly StackPanel list = new StackPanel { Spacing = 10 };
        private readonly TextBlock verdict = new TextBlock { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold };
        private IReadOnlyList<CheckResult> results = new List<CheckResult>();

        public DiagnosticsWindow()
        {
            Title = T("Check this computer");
            Width = 640;
            Height = 480;
            MinWidth = 480;
            MinHeight = 320;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;

            Button copy = new Button { Content = T("Copy") };
            Button close = new Button { Content = T("Close"), IsDefault = true, IsCancel = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            copy.Click += async (_, _) =>
            {
                if (Clipboard != null)
                    await Clipboard.SetTextAsync(Report());
            };
            close.Click += (_, _) => Close();

            TextBlock heading = new TextBlock { Text = T("What the simulator needs, and whether it is here") };
            heading.Classes.Add("heading");

            DockPanel root = new DockPanel { Margin = new Thickness(24, 20), LastChildFill = true };
            StackPanel top = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 0, 14) };
            top.Children.Add(heading);
            top.Children.Add(verdict);
            DockPanel.SetDock(top, Dock.Top);

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            buttons.Children.Add(copy);
            buttons.Children.Add(close);
            DockPanel.SetDock(buttons, Dock.Bottom);

            root.Children.Add(top);
            root.Children.Add(buttons);
            root.Children.Add(new ScrollViewer { Content = list });
            Content = root;

            verdict.Text = T("Checking…");
            Opened += async (_, _) =>
            {
                results = await Diagnostics.Run(CancellationToken.None);
                Show(results);
            };
        }

        private void Show(IReadOnlyList<CheckResult> checks)
        {
            list.Children.Clear();
            foreach (CheckResult check in checks)
            {
                (string mark, IBrush colour) = check.State switch
                {
                    CheckState.Ok => ("✓", Brushes.SeaGreen),
                    CheckState.Failed => ("✗", Brushes.IndianRed),
                    _ => ("–", Brushes.Gray),
                };

                Grid row = new Grid { ColumnDefinitions = new ColumnDefinitions("28,110,*") };
                TextBlock markBlock = new TextBlock { Text = mark, Foreground = colour, FontWeight = FontWeight.Bold, FontSize = 16 };
                TextBlock name = new TextBlock { Text = check.Name, FontWeight = FontWeight.SemiBold };
                SelectableTextBlock detail = new SelectableTextBlock { Text = check.Detail, TextWrapping = TextWrapping.Wrap };
                Grid.SetColumn(name, 1);
                Grid.SetColumn(detail, 2);
                row.Children.Add(markBlock);
                row.Children.Add(name);
                row.Children.Add(detail);
                list.Children.Add(row);
            }

            verdict.Text = Diagnostics.Healthy(checks)
                ? T("Everything the simulator needs is in place.")
                : T("Something the simulator needs is missing; the lines marked ✗ say what.");
        }

        private string Report()
        {
            StringBuilder report = new StringBuilder();
            foreach (CheckResult check in results)
                report.AppendLine($"{check.State,-7} {check.Name,-12} {check.Detail}");
            return report.ToString();
        }
    }
}
