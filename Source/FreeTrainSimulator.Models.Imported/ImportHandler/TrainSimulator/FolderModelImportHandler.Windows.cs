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
using System.Collections.Immutable;
using System.Diagnostics;
using System.Security;

using FreeTrainSimulator.Models.Content;

using Microsoft.Win32;

namespace FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator
{
    internal sealed partial class FolderModelImportHandler
    {
        private const string ortsFoldersKey = "SOFTWARE\\OpenRails\\ORTS\\Folders";

        /// <summary>
        /// Reads the content folders an earlier Open Rails installation recorded in the registry.
        /// </summary>
        private static partial ImmutableArray<FolderModel> DiscoverLegacyFolders(ContentModel contentModel)
        {
            ArgumentNullException.ThrowIfNull(contentModel, nameof(contentModel));

            List<FolderModel> folderModels = new List<FolderModel>();

            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(ortsFoldersKey);
                if (key != null)
                {
                    foreach (string folder in key.GetValueNames())
                    {
                        string contentPath = key.GetValue(folder) as string;
                        if (string.IsNullOrWhiteSpace(contentPath))
                            continue;

                        folderModels.Add(new FolderModel(folder, contentPath, contentModel));
                    }
                }
            }
            catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or ObjectDisposedException)
            {
                Trace.TraceWarning($"Could not import existing content folders {ex.Message}.");
            }

            return folderModels.ToImmutableArray();
        }
    }
}
