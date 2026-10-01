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
using System.Security;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

using Riel.Common.Info;

namespace Riel.Launcher.Gui
{
    /// <summary>
    /// Theme and accent color, remembered between runs.
    /// </summary>
    /// <remarks>
    /// Dark unless the user switched it off: a launcher for a game is mostly looked at in the
    /// evening, next to a game window that is itself dark. The choice is a one word file beside
    /// the rest of the configuration, so it survives reinstalls and is easy to reset.
    /// </remarks>
    internal static class Appearance
    {
        public static IReadOnlyList<AccentOption> AccentOptions { get; } = Array.AsReadOnly(new[]
        {
            AccentPalette.Amber,
            AccentPalette.CreateOption("blue", "Blue", "#82B1FF", "#285FAD"),
            AccentPalette.CreateOption("teal", "Teal", "#60C9BC", "#166F68"),
            AccentPalette.CreateOption("green", "Green", "#91C66F", "#3B6D25"),
            AccentPalette.CreateOption("violet", "Violet", "#BD9BF6", "#6946AA"),
            AccentPalette.CreateOption("rose", "Rose", "#F28FA6", "#A33658"),
            AccentPalette.CreateOption("orange", "Orange", "#F5A56A", "#9C4616"),
        });

        public static bool Dark { get; private set; } = true;
        public static string AccentId { get; private set; } = "amber";

        /// <summary>Applies the remembered choice; call once, before the first window opens.</summary>
        public static void Load() => Load(RuntimeInfo.UserDataFolder);

        internal static void Load(string folder)
        {
            Dark = !string.Equals(ReadSetting(folder, "launcher-theme"), "light", StringComparison.OrdinalIgnoreCase);
            AccentId = FindAccent(ReadSetting(folder, "launcher-accent")).Id;
            Apply();
        }

        /// <summary>Switches to <paramref name="dark"/> and remembers it.</summary>
        public static void Set(bool dark) => Set(dark, RuntimeInfo.UserDataFolder);

        internal static void Set(bool dark, string folder)
        {
            Dark = dark;
            Apply();
            WriteSetting(folder, "launcher-theme", dark ? "dark" : "light");
        }

        /// <summary>Applies an accent temporarily, so Settings can preview and cancel a change.</summary>
        public static void PreviewAccent(string id)
        {
            AccentId = FindAccent(id).Id;
            Apply();
        }

        /// <summary>Applies and remembers one of the supported accent colors.</summary>
        public static void SetAccent(string id) => SetAccent(id, RuntimeInfo.UserDataFolder);

        internal static void SetAccent(string id, string folder)
        {
            PreviewAccent(id);
            WriteSetting(folder, "launcher-accent", AccentId);
        }

        private static AccentOption FindAccent(string id) => AccentOptions.FirstOrDefault(option =>
            string.Equals(option.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? AccentOptions[0];

        private static string ReadSetting(string folder, string name)
        {
            try
            {
                var file = Path.Combine(folder, name);
                return File.Exists(file) ? File.ReadAllText(file).Trim() : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException)
            {
                // Unreadable is the same as unset.
                return null;
            }
        }

        private static void WriteSetting(string folder, string name, string value)
        {
            try
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, name), value);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException)
            {
                // The choice still applies when this computer cannot save its configuration.
            }
        }

        private static void Apply()
        {
            var app = Application.Current;
            if (app == null)
                return;

            var accent = FindAccent(AccentId);
            ApplyResources(app, ThemeVariant.Dark, accent.DarkPalette);
            ApplyResources(app, ThemeVariant.Light, accent.LightPalette);
            foreach (var fluent in app.Styles.OfType<FluentTheme>())
            {
                ApplyFluentPalette(fluent, ThemeVariant.Dark, accent.DarkPalette);
                ApplyFluentPalette(fluent, ThemeVariant.Light, accent.LightPalette);
            }
            app.RequestedThemeVariant = Dark ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        private static void ApplyResources(Application app, ThemeVariant theme, AccentPalette palette)
        {
            if (app.Resources.ThemeDictionaries.TryGetValue(theme, out var resources)
                && resources is ResourceDictionary dictionary)
                dictionary.SetItems(palette.Resources());
        }

        private static void ApplyFluentPalette(FluentTheme fluent, ThemeVariant theme, AccentPalette palette)
        {
            if (fluent.Palettes.TryGetValue(theme, out var colors))
                colors.Accent = palette.Accent;
            else
                fluent.Palettes[theme] = new ColorPaletteResources { Accent = palette.Accent };
        }
    }
}
