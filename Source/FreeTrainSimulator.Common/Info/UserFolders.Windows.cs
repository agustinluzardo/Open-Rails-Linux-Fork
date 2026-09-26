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
    /// Where the simulator keeps a user's files.
    /// </summary>
    /// <remarks>
    /// Windows keeps configuration, data, state and caches together in the roaming profile, which
    /// is what the simulator has always done here; the four properties differ only in the
    /// subfolder they name, and exist so shared code need not know which convention applies.
    /// </remarks>
    internal static class UserFolders
    {
        private static readonly string root =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), RuntimeInfo.ProductName);

        internal static string Configuration => root;

        internal static string Data => root;

        internal static string State => root;

        internal static string Cache => Path.Combine(root, "Cache");
    }
}
