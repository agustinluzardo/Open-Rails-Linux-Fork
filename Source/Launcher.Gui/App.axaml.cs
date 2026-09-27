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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Markup.Xaml;

namespace Riel.Launcher.Gui
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            Appearance.Load();
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = Program.DispatcherBaseUrl == null
                    ? new MainWindow()
                    : new DispatcherWindow(Program.DispatcherBaseUrl);
                // The dispatcher runs as a separate Xwayland window. Give it the same
                // native icon as the launcher so the compositor does not show the X icon.
                using var iconStream = AssetLoader.Open(new Uri("avares://riel-gui/Assets/riel.png"));
                desktop.MainWindow.Icon = new WindowIcon(iconStream);
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
