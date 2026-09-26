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
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

using Avalonia.Markup.Xaml;

namespace Riel.Launcher.Gui
{
    /// <summary>
    /// The launcher's text in the user's language.
    /// </summary>
    /// <remarks>
    /// Strings are written in English and looked up in a GetText catalog, the format the rest of
    /// the project is translated in, so a translator works the same way here as anywhere else:
    /// copy Source/Locales/Launcher.Gui/es.po to the new language and fill in msgstr. The
    /// catalogs are compiled into the program, so there is nothing to install or to lose.
    ///
    /// The language comes from LANGUAGE, then from the locale .NET derives from LC_ALL, LC_MESSAGES
    /// and LANG. A missing translation falls back to the English it was written in.
    /// </remarks>
    internal static class Translation
    {
        private static readonly IReadOnlyDictionary<string, string> catalog = Load();

        /// <summary>The translation of <paramref name="english"/>.</summary>
        public static string T(string english)
        {
            return english != null && catalog.TryGetValue(english, out string translated) ? translated : english;
        }

        /// <summary>
        /// A count with its noun: "1 route", "3 routes". Both forms are translated separately,
        /// since languages differ in more than an added "s".
        /// </summary>
        public static string Count(int count, string one, string many)
        {
            return F(count == 1 ? one : many, count);
        }

        /// <summary>The translation of a format string, filled in.</summary>
        public static string F(string english, params object[] arguments)
        {
            return string.Format(CultureInfo.CurrentCulture, T(english), arguments);
        }

        private static IReadOnlyDictionary<string, string> Load()
        {
            foreach (string language in Languages())
            {
                using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Riel.Launcher.Gui.Locales.{language}.po");
                if (stream != null)
                {
                    using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                    return PoCatalog.Parse(reader.ReadToEnd());
                }
            }
            return new Dictionary<string, string>();
        }

        /// <summary>Candidate catalog names, most specific first: es_AR, es-AR, es.</summary>
        private static IEnumerable<string> Languages()
        {
            List<string> requested = new List<string>();

            // GNU's LANGUAGE is a colon separated preference list, and outranks the locale.
            string preference = Environment.GetEnvironmentVariable("LANGUAGE");
            if (!string.IsNullOrEmpty(preference))
                requested.AddRange(preference.Split(':', StringSplitOptions.RemoveEmptyEntries));
            requested.Add(CultureInfo.CurrentUICulture.Name);

            foreach (string entry in requested)
            {
                string name = entry.Split('.', '@')[0];
                if (name.Length == 0 || name == "C" || name == "POSIX")
                    continue;
                yield return name.Replace('-', '_');
                yield return name.Replace('_', '-');
                yield return name.Split('_', '-')[0];
            }
        }
    }

    /// <summary>
    /// Translates a string in markup: <c>Text="{l:T 'Play'}"</c>.
    /// </summary>
    public sealed class TExtension : MarkupExtension
    {
        public TExtension()
        {
        }

        public TExtension(string text)
        {
            Text = text;
        }

        public string Text { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return Translation.T(Text);
        }
    }
}
