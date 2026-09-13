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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Native;
using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.Shim;
using FreeTrainSimulator.Models.Shim;

namespace Riel.Launcher
{
    /// <summary>
    /// Reads and edits the content configuration the simulator shares with the Windows menu.
    /// </summary>
    internal static class ContentStore
    {
        /// <summary>
        /// Loads the content model, scanning the folders the first time or after an upgrade.
        /// </summary>
        internal static async Task<ContentModel> Load(CancellationToken cancellationToken)
        {
            ContentModel content = await ((ContentModel)null).Get(cancellationToken).ConfigureAwait(false);

            if (content.RefreshRequired())
            {
                Console.Error.WriteLine("Content has changed since it was last scanned; rescanning.");
                content = await Scan(content, cancellationToken).ConfigureAwait(false);
            }
            return content;
        }

        /// <summary>Rescans every configured folder.</summary>
        internal static async Task Refresh(CancellationToken cancellationToken)
        {
            ContentModel content = await ((ContentModel)null).Get(cancellationToken).ConfigureAwait(false);
            _ = await Scan(content, cancellationToken).ConfigureAwait(false);
        }

        internal static async Task AddFolder(string name, string path, CancellationToken cancellationToken)
        {
            string resolved = ContentIO.ResolveDirectory(Path.GetFullPath(path))
                ?? throw new LauncherException($"'{path}' is not a directory");

            ContentModel content = await ((ContentModel)null).Get(cancellationToken).ConfigureAwait(false);
            if (content.ContentFolders.Any(folder => string.Equals(folder.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new LauncherException($"a content folder named '{name}' already exists");

            List<(string, string)> folders = content.ContentFolders
                .Select(folder => (folder.Name, folder.ContentPath))
                .Append((name, resolved))
                .ToList();

            ContentModel updated = await ((ContentModel)null).Setup(folders, cancellationToken).ConfigureAwait(false);
            _ = await Scan(updated, cancellationToken).ConfigureAwait(false);
        }

        internal static async Task RemoveFolder(string name, CancellationToken cancellationToken)
        {
            ContentModel content = await ((ContentModel)null).Get(cancellationToken).ConfigureAwait(false);
            FolderModel target = MatchFolder(content, name);

            List<(string, string)> folders = content.ContentFolders
                .Where(folder => folder != target)
                .Select(folder => (folder.Name, folder.ContentPath))
                .ToList();

            ContentModel updated = await ((ContentModel)null).Setup(folders, cancellationToken).ConfigureAwait(false);
            _ = await Scan(updated, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Imports the routes, activities, paths and consists of every folder. This is the slow
        /// part of adding content - a large route takes a while - so it reports progress.
        /// </summary>
        private static async Task<ContentModel> Scan(ContentModel content, CancellationToken cancellationToken)
        {
            Progress<int> progress = new Progress<int>(percent =>
            {
                if (!Console.IsOutputRedirected)
                    Console.Error.Write($"\rScanning content... {percent,3}%");
            });

            ContentModel scanned = await content.Setup(progress, cancellationToken).ConfigureAwait(false);

            if (!Console.IsOutputRedirected)
                Console.Error.WriteLine("\rScanning content... done");
            return scanned;
        }

        // ----------------------------------------------------------------------------- matching

        internal static FolderModel MatchFolder(ContentModel content, string name)
        {
            ArgumentNullException.ThrowIfNull(content);
            return Match(content.ContentFolders, name, "content folder");
        }

        /// <summary>
        /// Finds a route by name across every folder, and returns the folder holding it.
        /// </summary>
        internal static async Task<(FolderModel Folder, RouteModelHeader Route)> MatchRoute(string name, CancellationToken cancellationToken)
        {
            ContentModel content = await Load(cancellationToken).ConfigureAwait(false);

            List<(FolderModel Folder, RouteModelHeader Route)> candidates = new List<(FolderModel, RouteModelHeader)>();
            foreach (FolderModel folder in content.ContentFolders)
            {
                foreach (RouteModelHeader route in await folder.GetRoutes(cancellationToken).ConfigureAwait(false))
                    candidates.Add((folder, route));
            }

            if (candidates.Count == 0)
                throw new LauncherException("no routes found; add a content folder with 'riel content add'");

            List<(FolderModel Folder, RouteModelHeader Route)> matches = Narrow(candidates, candidate => candidate.Route, name);
            return matches.Count switch
            {
                1 => matches[0],
                0 => throw new LauncherException($"no route matches '{name}'"),
                _ => throw new LauncherException($"'{name}' matches several routes: {string.Join(", ", matches.Select(m => m.Route.Name))}"),
            };
        }

        /// <summary>
        /// Finds one model by name: an exact match wins, otherwise a unique prefix, otherwise a
        /// unique substring. Comparisons ignore case.
        /// </summary>
        internal static T Match<T>(ImmutableArray<T> models, string name, string what) where T : ModelBase
        {
            List<T> matches = Narrow(models.ToList(), model => model, name);
            return matches.Count switch
            {
                1 => matches[0],
                0 => throw new LauncherException($"no {what} matches '{name}'"),
                _ => throw new LauncherException($"'{name}' matches several {what}s: {string.Join(", ", matches.Select(m => m.Name))}"),
            };
        }

        private static List<TItem> Narrow<TItem>(List<TItem> items, Func<TItem, ModelBase> selector, string name)
        {
            List<TItem> exact = items.Where(item =>
                string.Equals(selector(item).Name, name, StringComparison.CurrentCultureIgnoreCase) ||
                string.Equals(selector(item).Id, name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count > 0)
                return exact;

            List<TItem> prefix = items.Where(item =>
                selector(item).Name?.StartsWith(name, StringComparison.CurrentCultureIgnoreCase) == true).ToList();
            if (prefix.Count > 0)
                return prefix;

            return items.Where(item =>
                selector(item).Name?.Contains(name, StringComparison.CurrentCultureIgnoreCase) == true).ToList();
        }
    }
}
