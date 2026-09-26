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

namespace FreeTrainSimulator.Common.Info
{
    /// <summary>
    /// Where the simulator keeps a user's files, following the XDG base directory specification.
    /// </summary>
    /// <remarks>
    /// The specification separates four kinds of file, and desktops and backup tools rely on that
    /// separation - a backup that skips caches, a distribution upgrade that keeps configuration.
    /// Putting everything in one folder, as the Windows build does, would work but would misfile
    /// several gigabytes of route cache as configuration.
    ///
    /// The directory name is the lowercase, hyphenated form: a folder with spaces and capitals in
    /// ~/.config is out of place next to everything else there.
    /// </remarks>
    internal static class UserFolders
    {
        private const string FolderName = "riel";

        /// <summary>Settings and profiles: XDG_CONFIG_HOME, by default ~/.config.</summary>
        internal static string Configuration { get; } = Resolve("XDG_CONFIG_HOME", ".config");

        /// <summary>Saves and other files the user would miss: XDG_DATA_HOME, by default ~/.local/share.</summary>
        internal static string Data { get; } = Resolve("XDG_DATA_HOME", Path.Combine(".local", "share"));

        /// <summary>Logs: XDG_STATE_HOME, by default ~/.local/state.</summary>
        internal static string State { get; } = Resolve("XDG_STATE_HOME", Path.Combine(".local", "state"));

        /// <summary>Regenerable content indexes: XDG_CACHE_HOME, by default ~/.cache.</summary>
        internal static string Cache { get; } = Resolve("XDG_CACHE_HOME", ".cache");

        private static string Resolve(string variable, string fallback)
        {
            string configured = Environment.GetEnvironmentVariable(variable);

            // The specification says a relative value is invalid and must be ignored.
            string root = !string.IsNullOrEmpty(configured) && Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), fallback);

            return Path.Combine(root, FolderName);
        }
    }
}
