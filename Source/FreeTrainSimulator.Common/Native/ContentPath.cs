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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace FreeTrainSimulator.Common.Native
{
    /// <summary>
    /// Resolves MSTS content paths on case sensitive file systems.
    /// </summary>
    /// <remarks>
    /// MSTS content was authored on Windows, so a route freely writes
    /// <c>..\..\GLOBAL\SHAPES\track1.s</c> for a file stored as
    /// <c>../../Global/Shapes/Track1.s</c>, and different files of the same route disagree with
    /// each other about the casing. On NTFS nobody notices; on ext4 or btrfs every one of those
    /// references fails, which is why an otherwise healthy route loads with missing shapes,
    /// textures and sounds - or does not load at all.
    ///
    /// Every content path therefore goes through <see cref="ResolveFile"/> or
    /// <see cref="ResolveDirectory"/>, which walk the path one segment at a time and match each
    /// segment case insensitively against a cached listing of its parent directory. Directory
    /// listings are cached per directory and revalidated against the directory's last write time,
    /// so loading a route costs one listing per directory rather than one per reference.
    ///
    /// The fast path is a plain existence check, so a correctly cased path - and every path on a
    /// case insensitive file system - costs one syscall and never builds an index.
    /// </remarks>
    public static class ContentPath
    {
        private sealed class DirectoryIndex
        {
            internal DateTime Timestamp { get; init; }
            internal Dictionary<string, string> Entries { get; init; }
        }

        private static readonly ConcurrentDictionary<string, DirectoryIndex> directoryCache =
            new ConcurrentDictionary<string, DirectoryIndex>(StringComparer.Ordinal);

        private static readonly char[] separators = new[] { '\\', '/' };

        /// <summary>
        /// True when path lookups need the case insensitive walk. Detected once by probing the
        /// temporary directory, so a case insensitive mount (an NTFS or exFAT drive holding the
        /// MSTS install, for instance) is not misjudged from the operating system alone.
        /// </summary>
        public static bool CaseSensitiveFileSystem { get; } = DetectCaseSensitivity();

        /// <summary>
        /// Converts a path written with Windows separators into one this platform understands.
        /// </summary>
        public static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path) || Path.DirectorySeparatorChar == '\\')
                return path;
            return path.Replace('\\', Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Returns the path of an existing file, correcting the case of each segment, or
        /// <c>null</c> when no such file exists.
        /// </summary>
        public static string ResolveFile(string path)
        {
            return Resolve(path, FileSystemEntry.File);
        }

        /// <summary>
        /// Returns the path of an existing directory, correcting the case of each segment, or
        /// <c>null</c> when no such directory exists.
        /// </summary>
        public static string ResolveDirectory(string path)
        {
            return Resolve(path, FileSystemEntry.Directory);
        }

        /// <summary>
        /// Returns the path of an existing file or directory, correcting the case of each
        /// segment, or <c>null</c> when nothing exists at that path.
        /// </summary>
        public static string ResolveAny(string path)
        {
            return Resolve(path, FileSystemEntry.Any);
        }

        /// <summary>
        /// Resolves a content path for reading, falling back to the normalized path when the
        /// target does not exist so callers keep reporting the name the content asked for.
        /// </summary>
        public static string ResolveOrOriginal(string path)
        {
            return ResolveAny(path) ?? Normalize(path);
        }

        /// <summary>
        /// Case insensitive replacement for <see cref="File.Exists(string)"/>.
        /// </summary>
        public static bool FileExists(string path)
        {
            return ResolveFile(path) != null;
        }

        /// <summary>
        /// Case insensitive replacement for <see cref="Directory.Exists(string)"/>.
        /// </summary>
        public static bool DirectoryExists(string path)
        {
            return ResolveDirectory(path) != null;
        }

        /// <summary>
        /// Opens a content file for reading, resolving its case first.
        /// </summary>
        public static FileStream OpenRead(string path)
        {
            string resolved = ResolveFile(path);
            if (resolved == null)
                throw new FileNotFoundException($"Content file not found: {path}", path);
            return File.OpenRead(resolved);
        }

        /// <summary>
        /// Drops cached directory listings. Pass a directory to drop just that one.
        /// </summary>
        public static void InvalidateCache(string directory = null)
        {
            if (directory == null)
                directoryCache.Clear();
            else
                directoryCache.TryRemove(Path.TrimEndingDirectorySeparator(Normalize(directory)), out _);
        }

        private enum FileSystemEntry
        {
            File,
            Directory,
            Any,
        }

        private static bool Exists(string path, FileSystemEntry kind)
        {
            return kind switch
            {
                FileSystemEntry.File => File.Exists(path),
                FileSystemEntry.Directory => Directory.Exists(path),
                _ => File.Exists(path) || Directory.Exists(path),
            };
        }

        private static string Resolve(string path, FileSystemEntry kind)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            string normalized = Normalize(path);

            // Correctly cased paths, and every path on a case insensitive file system, end here.
            if (Exists(normalized, kind))
                return normalized;

            if (!CaseSensitiveFileSystem)
                return null;

            return ResolveSegments(normalized, kind);
        }

        private static string ResolveSegments(string normalized, FileSystemEntry kind)
        {
            string full;
            try
            {
                // GetFullPath collapses '.' and '..' so a reference that climbs out of a route
                // folder is matched against the directory it really lands in.
                full = Path.GetFullPath(normalized);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                return null;
            }

            string root = Path.GetPathRoot(full);
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                return null;

            string[] segments = full.Substring(root.Length).Split(separators, StringSplitOptions.RemoveEmptyEntries);
            string current = Path.TrimEndingDirectorySeparator(root);
            if (current.Length == 0)
                current = root;

            for (int i = 0; i < segments.Length; i++)
            {
                bool last = i == segments.Length - 1;
                string candidate = Path.Combine(current, segments[i]);

                // An exactly matching segment needs no index; only the mismatching ones do.
                if (last ? Exists(candidate, kind) : Directory.Exists(candidate))
                {
                    current = candidate;
                    continue;
                }

                string actual = LookupIgnoreCase(current, segments[i]);
                if (actual == null)
                    return null;

                current = Path.Combine(current, actual);
                if (!last && !Directory.Exists(current))
                    return null;
            }

            return Exists(current, kind) ? current : null;
        }

        private static string LookupIgnoreCase(string directory, string name)
        {
            DirectoryIndex index = GetIndex(directory);
            if (index == null)
                return null;
            return index.Entries.TryGetValue(name, out string actual) ? actual : null;
        }

        private static DirectoryIndex GetIndex(string directory)
        {
            DateTime timestamp;
            try
            {
                DirectoryInfo info = new DirectoryInfo(directory);
                if (!info.Exists)
                    return null;
                timestamp = info.LastWriteTimeUtc;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }

            if (directoryCache.TryGetValue(directory, out DirectoryIndex cached) && cached.Timestamp == timestamp)
                return cached;

            Dictionary<string, string> entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    string name = Path.GetFileName(entry);
                    // Two entries differing only in case can coexist here; the first one wins,
                    // which matches how a case insensitive file system would have behaved.
                    if (!entries.ContainsKey(name))
                        entries.Add(name, name);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }

            DirectoryIndex index = new DirectoryIndex { Timestamp = timestamp, Entries = entries };
            directoryCache[directory] = index;
            return index;
        }

        private static bool DetectCaseSensitivity()
        {
            if (OperatingSystem.IsWindows())
                return false;

            string probe = null;
            try
            {
                probe = Path.Combine(Path.GetTempPath(), $"fts-case-{Guid.NewGuid():N}.TMP");
                File.WriteAllText(probe, string.Empty);
                return !File.Exists(probe.Replace(".TMP", ".tmp", StringComparison.Ordinal));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                // Assume the stricter behaviour when the probe cannot run.
                return true;
            }
            finally
            {
                try
                {
                    if (probe != null)
                        File.Delete(probe);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
