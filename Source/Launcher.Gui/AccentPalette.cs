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

using Avalonia.Media;

namespace Riel.Launcher.Gui
{
    internal sealed record AccentOption(string Id, string Name, Color PreviewColor)
    {
        internal AccentPalette DarkPalette { get; init; }
        internal AccentPalette LightPalette { get; init; }
    }

    /// <summary>Related action and selection colors for one appearance variant.</summary>
    internal sealed record AccentPalette(
        Color Accent, Color Hover, Color Pressed, Color Foreground,
        Color Disabled, Color DisabledForeground, Color Selected, Color SelectedForeground)
    {
        internal static AccentOption Amber { get; } = new("amber", "Amber", Color.Parse("#F2B75A"))
        {
            DarkPalette = FromHex("#F2B75A", "#F7C575", "#DEA347", "#261C0C",
                "#433B2C", "#D6C3A1", "#3B3327", "#F8E4C3"),
            LightPalette = FromHex("#A65B0A", "#8B4A07", "#743C04", "#FFFFFF",
                "#E8DDCD", "#6D573A", "#FFF0D9", "#57390D"),
        };

        internal static AccentOption CreateOption(string id, string name, string darkAccent, string lightAccent)
        {
            var dark = Color.Parse(darkAccent);
            var light = Color.Parse(lightAccent);
            var darkBackground = Color.Parse("#14171B");
            return new AccentOption(id, name, dark)
            {
                // Pastel actions on dark surfaces; stronger actions on light surfaces.
                // State shades keep the same text readable rather than reusing one hue everywhere.
                DarkPalette = new AccentPalette(dark,
                    Mix(dark, Colors.White, 0.14), Mix(dark, darkBackground, 0.10), darkBackground,
                    Mix(darkBackground, dark, 0.25), Mix(dark, Colors.White, 0.40),
                    Mix(darkBackground, dark, 0.16), Mix(dark, Colors.White, 0.38)),
                LightPalette = new AccentPalette(light,
                    Mix(light, Colors.Black, 0.12), Mix(light, Colors.Black, 0.24), Colors.White,
                    Mix(light, Colors.White, 0.85), Mix(light, Colors.Black, 0.12),
                    Mix(light, Colors.White, 0.90), Mix(light, Colors.Black, 0.20)),
            };
        }

        internal IEnumerable<KeyValuePair<object, object>> Resources()
        {
            yield return Brush("RielAccent", Accent);
            yield return Brush("RielAccentHover", Hover);
            yield return Brush("RielAccentPressed", Pressed);
            yield return Brush("RielAccentForeground", Foreground);
            yield return Brush("RielAccentDisabled", Disabled);
            yield return Brush("RielAccentDisabledForeground", DisabledForeground);
            yield return Brush("RielSelected", Selected);
            yield return Brush("RielSelectedForeground", SelectedForeground);
            yield return Brush("MenuFlyoutSubItemChevronSubMenuOpened", Accent);
        }

        private static KeyValuePair<object, object> Brush(string name, Color color) =>
            new(name, new SolidColorBrush(color));

        private static AccentPalette FromHex(string accent, string hover, string pressed, string foreground,
            string disabled, string disabledForeground, string selected, string selectedForeground) =>
            new(Color.Parse(accent), Color.Parse(hover), Color.Parse(pressed), Color.Parse(foreground),
                Color.Parse(disabled), Color.Parse(disabledForeground), Color.Parse(selected), Color.Parse(selectedForeground));

        private static Color Mix(Color start, Color end, double amount) => Color.FromRgb(
            (byte)Math.Round(start.R + (end.R - start.R) * amount),
            (byte)Math.Round(start.G + (end.G - start.G) * amount),
            (byte)Math.Round(start.B + (end.B - start.B) * amount));
    }
}
