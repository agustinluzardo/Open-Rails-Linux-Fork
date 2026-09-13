// COPYRIGHT 2026 by the Open Rails Linux Fork project.
//
// This file is part of Open Rails.
//
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace FreeTrainSimulator.Common.Native
{
    /// <summary>
    /// Managed reader/writer for Windows style initialization files.
    /// </summary>
    /// <remarks>
    /// MSTS content and the train control system scripts store their parameters in .ini files
    /// which Open Rails reads through <c>GetPrivateProfileString</c>. That API only exists on
    /// Windows, so the Linux build routes the same calls here.
    ///
    /// The behaviour deliberately mirrors the Win32 one, including the quirks content relies on:
    /// section and key lookups are case insensitive, a value may be wrapped in double quotes
    /// which are stripped, everything after a <c>;</c> starting a line is a comment, and a key
    /// without an <c>=</c> yields an empty value.
    /// </remarks>
    public static class IniFile
    {
        private sealed class Section
        {
            internal readonly Dictionary<string, string> Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            internal readonly List<string> Order = new List<string>();

            internal void Set(string key, string value)
            {
                if (!Values.ContainsKey(key))
                    Order.Add(key);
                Values[key] = value;
            }
        }

        private sealed class Document
        {
            internal readonly Dictionary<string, Section> Sections = new Dictionary<string, Section>(StringComparer.OrdinalIgnoreCase);
            internal readonly List<string> Order = new List<string>();

            internal Section GetOrAdd(string name)
            {
                if (!Sections.TryGetValue(name, out Section section))
                {
                    section = new Section();
                    Sections.Add(name, section);
                    Order.Add(name);
                }
                return section;
            }
        }

        /// <summary>
        /// Reads a single value, returning <paramref name="defaultValue"/> when the file, the
        /// section or the key is missing.
        /// </summary>
        public static string GetValue(string fileName, string sectionName, string keyName, string defaultValue)
        {
            Document document = Read(fileName);
            if (document != null && sectionName != null && keyName != null &&
                document.Sections.TryGetValue(sectionName, out Section section) &&
                section.Values.TryGetValue(keyName, out string value))
                return value;
            return defaultValue;
        }

        /// <summary>
        /// Fills <paramref name="buffer"/> the way <c>GetPrivateProfileString</c> does and returns
        /// the number of characters written, excluding the terminating null.
        /// </summary>
        /// <remarks>
        /// A null <paramref name="keyName"/> asks for every key name in the section and a null
        /// <paramref name="sectionName"/> for every section name; both are returned as a list of
        /// null terminated strings closed by a second null, which is why the truncated result is
        /// reported as <paramref name="size"/> minus two rather than minus one.
        /// </remarks>
        public static int GetString(string sectionName, string keyName, string defaultValue, StringBuilder buffer, int size, string fileName)
        {
            ArgumentNullException.ThrowIfNull(buffer);

            if (size <= 0)
                return 0;

            buffer.Clear();

            if (sectionName != null && keyName != null)
            {
                string value = GetValue(fileName, sectionName, keyName, defaultValue) ?? string.Empty;
                if (value.Length > size - 1)
                    value = value.Substring(0, size - 1);
                buffer.Append(value);
                return value.Length;
            }

            Document document = Read(fileName);
            List<string> names = new List<string>();
            if (document != null)
            {
                if (sectionName == null)
                    names.AddRange(document.Order);
                else if (document.Sections.TryGetValue(sectionName, out Section section))
                    names.AddRange(section.Order);
            }

            // The list form is a run of null terminated names closed by an extra null, so the
            // usable room is two characters less than the buffer.
            int room = size - 2;
            int written = 0;
            foreach (string name in names)
            {
                if (written + name.Length + 1 > room)
                    return size - 2;
                buffer.Append(name).Append('\0');
                written += name.Length + 1;
            }
            return written;
        }

        /// <summary>
        /// Writes or removes a single value, creating the file and the section when needed.
        /// A null <paramref name="value"/> deletes the key, a null <paramref name="keyName"/>
        /// deletes the whole section.
        /// </summary>
        public static bool WriteValue(string fileName, string sectionName, string keyName, string value)
        {
            if (string.IsNullOrEmpty(fileName) || sectionName == null)
                return false;

            Document document = Read(fileName) ?? new Document();

            if (keyName == null)
            {
                document.Sections.Remove(sectionName);
                document.Order.RemoveAll(s => string.Equals(s, sectionName, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                Section section = document.GetOrAdd(sectionName);
                if (value == null)
                {
                    section.Values.Remove(keyName);
                    section.Order.RemoveAll(k => string.Equals(k, keyName, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    section.Set(keyName, value);
                }
            }

            try
            {
                string directory = Path.GetDirectoryName(fileName);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                StringBuilder output = new StringBuilder();
                foreach (string name in document.Order)
                {
                    Section section = document.Sections[name];
                    output.Append('[').Append(name).Append(']').Append('\n');
                    foreach (string key in section.Order)
                        output.Append(key).Append('=').Append(section.Values[key]).Append('\n');
                    output.Append('\n');
                }
                File.WriteAllText(fileName, output.ToString());
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                return false;
            }
        }

        private static Document Read(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            string resolved = ContentIO.ResolveFile(fileName);
            if (resolved == null)
                return null;

            string[] lines;
            try
            {
                lines = File.ReadAllLines(resolved);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                return null;
            }

            Document document = new Document();
            Section current = null;
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == ';' || line[0] == '#')
                    continue;

                if (line[0] == '[')
                {
                    int end = line.IndexOf(']', StringComparison.Ordinal);
                    if (end > 0)
                        current = document.GetOrAdd(line.Substring(1, end - 1).Trim());
                    continue;
                }

                // Values outside any section are unreachable through this API, matching Win32.
                if (current == null)
                    continue;

                int separator = line.IndexOf('=', StringComparison.Ordinal);
                string key = separator < 0 ? line : line.Substring(0, separator).TrimEnd();
                string value = separator < 0 ? string.Empty : line.Substring(separator + 1).Trim();

                // Win32 strips one layer of surrounding double quotes so values can keep spaces.
                if (value.Length > 1 && value[0] == '"' && value[value.Length - 1] == '"')
                    value = value.Substring(1, value.Length - 2);

                current.Set(key, value);
            }
            return document;
        }

        /// <summary>
        /// Reads a whole section as <c>key=value</c> entries, the shape
        /// <c>GetPrivateProfileSection</c> returns.
        /// </summary>
        public static IReadOnlyList<string> GetSection(string fileName, string sectionName)
        {
            List<string> result = new List<string>();
            Document document = Read(fileName);
            if (document != null && sectionName != null && document.Sections.TryGetValue(sectionName, out Section section))
            {
                foreach (string key in section.Order)
                    result.Add(string.Create(CultureInfo.InvariantCulture, $"{key}={section.Values[key]}"));
            }
            return result;
        }
    }
}
