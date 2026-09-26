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

namespace Riel.Launcher.Gui
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--supervise")
                return LaunchSession.Supervise(args[1]);
            if (args.Length == 2 && args[0] == "--run-report")
            {
                StartupReportPath = args[1];
                args = Array.Empty<string>();
            }
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        internal static string StartupReportPath { get; private set; }

        /// <summary>Also the entry point Avalonia's designer looks for.</summary>
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
        }
    }
}
