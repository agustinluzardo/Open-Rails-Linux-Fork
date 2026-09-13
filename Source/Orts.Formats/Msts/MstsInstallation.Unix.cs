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

using FreeTrainSimulator.Common.Native;

namespace Orts.Formats.Msts
{
    /// <summary>
    /// Finds a Microsoft Train Simulator installation without the Windows registry.
    /// </summary>
    /// <remarks>
    /// On Windows the installer records the path under
    /// <c>HKLM\SOFTWARE\Microsoft\Microsoft Games\Train Simulator\1.0</c>. There is nothing
    /// equivalent here, and an installation reaches a Linux machine in one of a few ways: copied
    /// from a Windows machine, installed into a Wine or Proton prefix, or unpacked by hand.
    ///
    /// The search therefore looks, in order, at an explicit override, then at the usual places -
    /// the XDG data directory, the home directory, and the Program Files folder of every Wine
    /// prefix it can find, including Steam's Proton prefixes. A folder counts as an installation
    /// when it holds both ROUTES and GLOBAL, which is what the rest of the code needs from it.
    /// </remarks>
    public static class MstsInstallation
    {
        /// <summary>
        /// Environment variable naming the installation explicitly. Set this when the content
        /// lives somewhere the search below would not think to look.
        /// </summary>
        public const string OverrideVariable = "RIEL_MSTS_PATH";

        private const string RoutesFolder = "ROUTES";
        private const string GlobalFolder = "GLOBAL";

        /// <summary>
        /// The installation to use, or the most likely location when none was found, so the
        /// caller can report a path the user recognises.
        /// </summary>
        public static string Locate(string defaultLocation)
        {
            foreach (string candidate in Candidates())
            {
                string resolved = Validate(candidate);
                if (resolved != null)
                {
                    Trace.TraceInformation($"Microsoft Train Simulator content found at '{resolved}'.");
                    return resolved;
                }
            }

            Trace.TraceInformation(
                $"No Microsoft Train Simulator installation found. Looked in ~/.local/share, the home directory and any Wine prefix; " +
                $"set {OverrideVariable} to point at one, or add the folder as content in the launcher.");
            return defaultLocation;
        }

        /// <summary>
        /// Returns <paramref name="path"/> with its case corrected when it holds an installation,
        /// otherwise <c>null</c>.
        /// </summary>
        public static string Validate(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            string root = ContentIO.ResolveDirectory(path);
            if (root == null)
                return null;

            return ContentIO.DirectoryExists(Path.Combine(root, RoutesFolder))
                && ContentIO.DirectoryExists(Path.Combine(root, GlobalFolder))
                ? root
                : null;
        }

        private static IEnumerable<string> Candidates()
        {
            string over = Environment.GetEnvironmentVariable(OverrideVariable);
            if (!string.IsNullOrEmpty(over))
                yield return over;

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string data = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            foreach (string name in new[] { "MSTS", "Train Simulator", "TrainSimulator", "msts" })
            {
                yield return Path.Combine(data, name);
                yield return Path.Combine(home, name);
                yield return Path.Combine(home, "Games", name);
            }

            foreach (string prefix in WinePrefixes())
            {
                foreach (string programFiles in new[] { "Program Files (x86)", "Program Files" })
                {
                    yield return Path.Combine(prefix, "drive_c", programFiles, "Microsoft Games", "Train Simulator");
                }
            }
        }

        private static IEnumerable<string> WinePrefixes()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            string configured = Environment.GetEnvironmentVariable("WINEPREFIX");
            if (!string.IsNullOrEmpty(configured))
                yield return configured;

            yield return Path.Combine(home, ".wine");

            // Steam keeps one Proton prefix per game under the library that holds it. Only the
            // default library is walked here; a game on another drive is reached through the
            // override variable or by adding its folder in the launcher.
            foreach (string steam in new[]
            {
                Path.Combine(home, ".steam", "steam", "steamapps", "compatdata"),
                Path.Combine(home, ".local", "share", "Steam", "steamapps", "compatdata"),
                Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam", "steamapps", "compatdata"),
            })
            {
                if (!Directory.Exists(steam))
                    continue;

                string[] prefixes;
                try
                {
                    prefixes = Directory.GetDirectories(steam);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    continue;
                }
                foreach (string prefix in prefixes)
                    yield return Path.Combine(prefix, "pfx");
            }
        }
    }
}
