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
using System.IO;

using Avalonia;
using Avalonia.Styling;

using FreeTrainSimulator.Common.Info;

namespace Riel.Launcher.Gui
{
    /// <summary>
    /// Light or dark, remembered between runs.
    /// </summary>
    /// <remarks>
    /// Dark unless the user switched it off: a launcher for a game is mostly looked at in the
    /// evening, next to a game window that is itself dark. The choice is a one word file beside
    /// the rest of the configuration, so it survives reinstalls and is easy to reset.
    /// </remarks>
    internal static class Appearance
    {
        private static string SettingFile => Path.Combine(RuntimeInfo.UserDataFolder, "launcher-theme");

        public static bool Dark { get; private set; } = true;

        /// <summary>Applies the remembered choice; call once, before the first window opens.</summary>
        public static void Load()
        {
            try
            {
                if (File.Exists(SettingFile))
                    Dark = !string.Equals(File.ReadAllText(SettingFile).Trim(), "light", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Unreadable is the same as unset.
            }
            Apply();
        }

        /// <summary>Switches to <paramref name="dark"/> and remembers it.</summary>
        public static void Set(bool dark)
        {
            Dark = dark;
            Apply();
            try
            {
                Directory.CreateDirectory(RuntimeInfo.UserDataFolder);
                File.WriteAllText(SettingFile, dark ? "dark" : "light");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Not remembering is better than failing; the switch still took effect.
            }
        }

        private static void Apply()
        {
            if (Application.Current != null)
                Application.Current.RequestedThemeVariant = Dark ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }
}
