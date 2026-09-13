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
using System.Diagnostics;
using System.IO;

namespace FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator
{
    /// <summary>
    /// Reads the Open Rails content folders out of a Wine prefix.
    /// </summary>
    /// <remarks>
    /// Someone moving to this build has most likely been running Open Rails under Wine, where it
    /// recorded its content folders in HKEY_CURRENT_USER. Wine keeps that hive as a plain text
    /// user.reg, so the folders can be carried over instead of asking the user to add them again.
    ///
    /// The parser only understands what this one key needs: the section header for the Open Rails
    /// folders and the quoted string values under it. Paths are Windows ones, and are translated
    /// back through the prefix's drive_c.
    /// </remarks>
    internal static class WineRegistry
    {
        private const string foldersSection = @"[Software\\OpenRails\\ORTS\\Folders]";

        internal static IEnumerable<(string Name, string Path)> ReadOpenRailsFolders()
        {
            foreach (string prefix in Prefixes())
            {
                string hive = Path.Combine(prefix, "user.reg");
                if (!File.Exists(hive))
                    continue;

                foreach ((string name, string value) in ReadSection(hive, foldersSection))
                {
                    string translated = TranslatePath(prefix, value);
                    if (translated != null)
                        yield return (name, translated);
                }
            }
        }

        private static IEnumerable<string> Prefixes()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            string configured = Environment.GetEnvironmentVariable("WINEPREFIX");
            if (!string.IsNullOrEmpty(configured))
                yield return configured;

            string standard = Path.Combine(home, ".wine");
            if (Directory.Exists(standard))
                yield return standard;
        }

        private static IEnumerable<(string Name, string Value)> ReadSection(string hive, string section)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(hive);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Trace.TraceWarning($"Could not read {hive}: {ex.Message}");
                yield break;
            }

            bool inside = false;
            foreach (string line in lines)
            {
                if (line.StartsWith('['))
                {
                    // Section headers carry a trailing timestamp, so compare the prefix only.
                    inside = line.StartsWith(section, StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!inside || !line.StartsWith('"'))
                    continue;

                // "Name"="C:\\path\\to\\content"
                int nameEnd = line.IndexOf('"', 1);
                int valueStart = line.IndexOf('"', nameEnd + 1);
                int valueEnd = line.LastIndexOf('"');
                if (nameEnd < 1 || valueStart < 0 || valueEnd <= valueStart)
                    continue;

                string name = line.Substring(1, nameEnd - 1);
                string value = line.Substring(valueStart + 1, valueEnd - valueStart - 1).Replace("\\\\", "\\", StringComparison.Ordinal);
                yield return (name, value);
            }
        }

        /// <summary>
        /// Turns a Windows path from inside the prefix into one this system can open, or returns
        /// <c>null</c> when it points at a drive the prefix does not map.
        /// </summary>
        private static string TranslatePath(string prefix, string windowsPath)
        {
            if (string.IsNullOrWhiteSpace(windowsPath) || windowsPath.Length < 3 || windowsPath[1] != ':')
                return null;

            char drive = char.ToLowerInvariant(windowsPath[0]);
            string remainder = windowsPath.Substring(3).Replace('\\', Path.DirectorySeparatorChar);

            // C: is the prefix itself; the others are symlinks Wine keeps in dosdevices.
            string root = drive == 'c'
                ? Path.Combine(prefix, "drive_c")
                : Path.Combine(prefix, "dosdevices", $"{drive}:");

            string full = Path.Combine(root, remainder);
            return Directory.Exists(full) ? full : null;
        }
    }
}
