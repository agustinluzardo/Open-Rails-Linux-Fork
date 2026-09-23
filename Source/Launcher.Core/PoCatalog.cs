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
using System.Text;

namespace Riel.Launcher
{
    /// <summary>
    /// Reads the GetText catalogs the launcher's translations are kept in.
    /// </summary>
    public static class PoCatalog
    {
        /// <summary>
        /// Reads msgid and msgstr pairs from a .po file: continuation lines, the usual escapes,
        /// and fuzzy entries skipped, which is what GetText itself does with them.
        /// </summary>
        public static Dictionary<string, string> Parse(string po)
        {
            Dictionary<string, string> entries = new Dictionary<string, string>(StringComparer.Ordinal);
            StringBuilder id = null, text = null, current = null;
            bool fuzzy = false;

            void Commit()
            {
                if (id != null && text != null && !fuzzy && id.Length > 0 && text.Length > 0)
                    entries[id.ToString()] = text.ToString();
                id = text = current = null;
                fuzzy = false;
            }

            foreach (string raw in po.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0)
                {
                    Commit();
                }
                else if (line.StartsWith("#,", StringComparison.Ordinal))
                {
                    if (id != null)
                        Commit();
                    fuzzy = line.Contains("fuzzy", StringComparison.Ordinal);
                }
                else if (line.StartsWith('#'))
                {
                    continue;
                }
                else if (line.StartsWith("msgid ", StringComparison.Ordinal))
                {
                    bool keepFuzzy = fuzzy;
                    if (id != null)
                        Commit();
                    fuzzy = keepFuzzy;
                    id = current = new StringBuilder(Unquote(line[6..]));
                }
                else if (line.StartsWith("msgstr ", StringComparison.Ordinal))
                {
                    text = current = new StringBuilder(Unquote(line[7..]));
                }
                else if (line.StartsWith('"') && current != null)
                {
                    current.Append(Unquote(line));
                }
            }
            Commit();
            return entries;
        }

        private static string Unquote(string quoted)
        {
            quoted = quoted.Trim();
            if (quoted.Length < 2 || quoted[0] != '"' || quoted[^1] != '"')
                return string.Empty;

            StringBuilder result = new StringBuilder(quoted.Length);
            for (int i = 1; i < quoted.Length - 1; i++)
            {
                char c = quoted[i];
                if (c == '\\' && i + 1 < quoted.Length - 1)
                {
                    char next = quoted[++i];
                    result.Append(next switch
                    {
                        'n' => '\n',
                        't' => '\t',
                        '"' => '"',
                        '\\' => '\\',
                        _ => next,
                    });
                }
                else
                {
                    result.Append(c);
                }
            }
            return result.ToString();
        }
    }
}
