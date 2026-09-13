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
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;

using FreeTrainSimulator.Common.Native;
using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator
{
    internal sealed partial class FolderModelImportHandler
    {
        /// <summary>
        /// The initialization file an Open Rails installation running under Wine leaves behind.
        /// </summary>
        private const string ortsFoldersFile = "OpenRails/Folders.ini";

        private const string ortsFoldersSection = "Folders";

        /// <summary>
        /// Finds the content folders an earlier Open Rails installation configured.
        /// </summary>
        /// <remarks>
        /// The Windows build reads these from the registry, which does not exist here. Two things
        /// stand in for it: an Open Rails folder list written under the XDG configuration
        /// directory, and, for the common case of having run Open Rails under Wine, the same
        /// registry hive inside a Wine prefix - Wine stores it as a plain text user.reg, so the
        /// folders a user already set up carry over rather than having to be added again.
        /// </remarks>
        private static partial ImmutableArray<FolderModel> DiscoverLegacyFolders(ContentModel contentModel)
        {
            ArgumentNullException.ThrowIfNull(contentModel, nameof(contentModel));

            List<FolderModel> folderModels = new List<FolderModel>();

            try
            {
                string configuration = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ortsFoldersFile);

                foreach (string entry in IniFile.GetSection(configuration, ortsFoldersSection))
                {
                    int separator = entry.IndexOf('=', StringComparison.Ordinal);
                    if (separator <= 0)
                        continue;

                    string name = entry.Substring(0, separator);
                    string path = entry.Substring(separator + 1);
                    if (!string.IsNullOrWhiteSpace(path))
                        folderModels.Add(new FolderModel(name, path, contentModel));
                }

                foreach ((string name, string path) in WineRegistry.ReadOpenRailsFolders())
                    folderModels.Add(new FolderModel(name, path, contentModel));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Trace.TraceWarning($"Could not import existing content folders {ex.Message}.");
            }

            return folderModels.ToImmutableArray();
        }
    }
}
