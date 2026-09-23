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
using System.Text.RegularExpressions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Riel.Launcher;

namespace Tests.FreeTrainSimulator.Launcher
{
    /// <summary>
    /// The launcher's translations: the reader, and the catalogs that ship.
    /// </summary>
    [TestClass]
    public class PoCatalogTests
    {
        [TestMethod]
        public void ReadsPairs()
        {
            Dictionary<string, string> catalog = PoCatalog.Parse("msgid \"Play\"\nmsgstr \"Jugar\"\n");
            Assert.AreEqual("Jugar", catalog["Play"]);
        }

        [TestMethod]
        public void JoinsContinuationLines()
        {
            Dictionary<string, string> catalog = PoCatalog.Parse("msgid \"\"\n\"Long \"\n\"text\"\nmsgstr \"\"\n\"Texto \"\n\"largo\"\n");
            Assert.AreEqual("Texto largo", catalog["Long text"]);
        }

        [TestMethod]
        public void UnderstandsTheEscapes()
        {
            Dictionary<string, string> catalog = PoCatalog.Parse("msgid \"a \\\"b\\\"\\n\"\nmsgstr \"x \\\"y\\\"\\n\"\n");
            Assert.AreEqual("x \"y\"\n", catalog["a \"b\"\n"]);
        }

        /// <summary>GetText does not use a fuzzy entry; neither does the launcher.</summary>
        [TestMethod]
        public void SkipsFuzzyEntries()
        {
            Dictionary<string, string> catalog = PoCatalog.Parse("#, fuzzy\nmsgid \"Play\"\nmsgstr \"Jugar\"\n\nmsgid \"Close\"\nmsgstr \"Cerrar\"\n");
            Assert.IsFalse(catalog.ContainsKey("Play"));
            Assert.AreEqual("Cerrar", catalog["Close"]);
        }

        [TestMethod]
        public void SkipsTheHeaderAndUntranslatedEntries()
        {
            Dictionary<string, string> catalog = PoCatalog.Parse("msgid \"\"\nmsgstr \"Language: es\\n\"\n\nmsgid \"Play\"\nmsgstr \"\"\n");
            Assert.AreEqual(0, catalog.Count);
        }

        // --------------------------------------------------------------------------- shipped

        private static string LocaleFolder()
        {
            for (DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "Source", "Locales", "Launcher.Gui");
                if (Directory.Exists(candidate))
                    return candidate;
            }
            Assert.Inconclusive("The source tree is not above the test binaries.");
            return null;
        }

        /// <summary>
        /// Every string in the template has a Spanish translation, and each translation keeps the
        /// template's placeholders: a lost {0} is not a cosmetic slip, it throws when the text is
        /// formatted and takes that screen down with it.
        /// </summary>
        [TestMethod]
        public void SpanishTranslatesEveryStringAndKeepsItsPlaceholders()
        {
            string folder = LocaleFolder();
            Dictionary<string, string> template = ParseIds(File.ReadAllText(Path.Combine(folder, "Launcher.Gui.pot")));
            Dictionary<string, string> spanish = PoCatalog.Parse(File.ReadAllText(Path.Combine(folder, "es.po")));

            List<string> missing = template.Keys.Where(id => !spanish.ContainsKey(id)).ToList();
            Assert.AreEqual(0, missing.Count, "untranslated: " + string.Join(" | ", missing));

            foreach ((string id, string text) in spanish)
            {
                CollectionAssert.AreEquivalent(Placeholders(id), Placeholders(text), $"placeholders differ in \"{text}\"");
                _ = string.Format(System.Globalization.CultureInfo.InvariantCulture, text, 1, 2, 3);
            }
        }

        private static string[] Placeholders(string text) =>
            Regex.Matches(text, @"\{\d+\}").Select(match => match.Value).Distinct().OrderBy(value => value).ToArray();

        /// <summary>The template's msgstr are all empty, which the reader would drop; read the ids.</summary>
        private static Dictionary<string, string> ParseIds(string pot)
        {
            string filled = Regex.Replace(pot, @"^msgstr """"$", "msgstr \"x\"", RegexOptions.Multiline);
            return PoCatalog.Parse(filled);
        }
    }
}
