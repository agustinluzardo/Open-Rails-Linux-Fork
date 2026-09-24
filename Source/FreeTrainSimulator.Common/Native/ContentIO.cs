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
    public static class ContentIO
    {
        private sealed class DirectoryIndex
        {
            internal DateTime Timestamp { get; init; }
            // Every name in the directory that matches a key without regard to case
            internal Dictionary<string, List<string>> Entries { get; init; }
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
        /// Opens a content file for reading as text, resolving its case and letting the reader
        /// detect the encoding - MSTS ships a mix of UTF-16 and plain ASCII files.
        /// </summary>
        public static StreamReader OpenText(string path)
        {
            return new StreamReader(ResolveOrOriginal(path), true);
        }

        /// <summary>Reads a content file, resolving its case.</summary>
        public static byte[] ReadAllBytes(string path)
        {
            return File.ReadAllBytes(ResolveOrOriginal(path));
        }

        /// <summary>Reads a content file, resolving its case.</summary>
        public static string ReadAllText(string path)
        {
            return File.ReadAllText(ResolveOrOriginal(path));
        }

        /// <summary>Reads a content file, resolving its case.</summary>
        public static string[] ReadAllLines(string path)
        {
            return File.ReadAllLines(ResolveOrOriginal(path));
        }

        /// <summary>
        /// How content is searched: the folder's case is resolved first, and the pattern is then
        /// matched without regard to case either.
        /// </summary>
        /// <remarks>
        /// Both halves matter. A route's consists live in TRAINS/CONSISTS on one installation and
        /// Trains/Consists on the next, and the files inside are .con on one and .CON on another,
        /// because MSTS was authored where neither distinction existed. Matching the pattern
        /// case sensitively would find the folder and then report it empty.
        ///
        /// Win32 matching is what the framework uses for the overloads without options, and these
        /// replace those call sites; the search patterns were written for it. Inaccessible entries
        /// are skipped rather than thrown over: content copied off a Windows disk routinely
        /// carries a file the current user cannot read, and losing one of forty thousand files is
        /// better than losing the scan. Nothing is skipped for being hidden - a dot prefixed file
        /// on a Linux file system is not a hidden file to MSTS.
        /// </remarks>
        private static EnumerationOptions SearchOptions(int depth) => new EnumerationOptions
        {
            MatchCasing = MatchCasing.CaseInsensitive,
            MatchType = MatchType.Win32,
            AttributesToSkip = FileAttributes.None,
            IgnoreInaccessible = true,
            RecurseSubdirectories = depth > 0,
            MaxRecursionDepth = depth > 0 ? depth : int.MaxValue,
        };

        private static readonly EnumerationOptions flatSearch = SearchOptions(0);

        /// <summary>
        /// Case insensitive replacement for <see cref="Directory.EnumerateFiles(string, string)"/>
        /// that yields nothing when the folder is absent.
        /// </summary>
        /// <remarks>
        /// A missing folder is not exceptional in content: an installation with no timetables has
        /// no timetable folder, and one route in twenty has no PATHS. Throwing there ended the
        /// scan of everything else, so the empty answer is the useful one. A folder that is
        /// genuinely required is reported by whoever needed it, by name.
        /// </remarks>
        /// <param name="depth">
        /// How many levels of subdirectory to descend into; 0, the default, searches only the
        /// folder itself.
        /// </param>
        public static IEnumerable<string> EnumerateFiles(string path, string searchPattern, int depth = 0)
        {
            string directory = ResolveDirectory(path);
            return directory == null
                ? Array.Empty<string>()
                : Directory.EnumerateFiles(directory, searchPattern, depth > 0 ? SearchOptions(depth) : flatSearch);
        }

        /// <summary>
        /// Case insensitive replacement for
        /// <see cref="Directory.EnumerateDirectories(string)"/> that yields nothing when the
        /// folder is absent.
        /// </summary>
        public static IEnumerable<string> EnumerateDirectories(string path, string searchPattern = "*")
        {
            string directory = ResolveDirectory(path);
            return directory == null
                ? Array.Empty<string>()
                : Directory.EnumerateDirectories(directory, searchPattern, flatSearch);
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

            if (segments.Length == 0)
                return Exists(current, kind) ? current : null;
            return ResolveSegment(current, segments, 0, kind);
        }

        /// <summary>
        /// Resolves <paramref name="segments"/> from <paramref name="index"/> on below
        /// <paramref name="current"/>, trying every entry whose name matches regardless of case.
        /// </summary>
        /// <remarks>
        /// Content unpacked with Linux tools into an install made under Wine routinely leaves
        /// SHAPES and Shapes, or GLOBAL and Global, side by side. On Windows those are one folder,
        /// so a file may sit under either of them; settling on the first match would lose the
        /// files kept under the other one - half a route's scenery, for instance.
        /// </remarks>
        private static string ResolveSegment(string current, string[] segments, int index, FileSystemEntry kind)
        {
            bool last = index == segments.Length - 1;
            string segment = segments[index];

            // An exactly matching segment needs no index; only the mismatching ones do.
            string candidate = Path.Combine(current, segment);
            string resolved = ResolveCandidate(candidate, segments, index, last, kind);
            if (resolved != null)
                return resolved;

            foreach (string actual in LookupIgnoreCase(current, segment))
            {
                if (string.Equals(actual, segment, StringComparison.Ordinal))
                    continue;
                resolved = ResolveCandidate(Path.Combine(current, actual), segments, index, last, kind);
                if (resolved != null)
                    return resolved;
            }
            return null;
        }

        private static string ResolveCandidate(string candidate, string[] segments, int index, bool last, FileSystemEntry kind)
        {
            if (last)
                return Exists(candidate, kind) ? candidate : null;
            return Directory.Exists(candidate) ? ResolveSegment(candidate, segments, index + 1, kind) : null;
        }

        private static IReadOnlyList<string> LookupIgnoreCase(string directory, string name)
        {
            DirectoryIndex index = GetIndex(directory);
            if (index == null)
                return Array.Empty<string>();
            return index.Entries.TryGetValue(name, out List<string> actual) ? actual : Array.Empty<string>();
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

            Dictionary<string, List<string>> entries = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    string name = Path.GetFileName(entry);
                    // Two entries differing only in case can coexist here, and both are kept: they
                    // would have been one folder on the file system the content was made for.
                    if (entries.TryGetValue(name, out List<string> names))
                        names.Add(name);
                    else
                        entries.Add(name, new List<string> { name });
                }
                foreach (List<string> names in entries.Values)
                    names.Sort(StringComparer.Ordinal);
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
                probe = Path.Combine(Path.GetTempPath(), $"riel-case-{Guid.NewGuid():N}.TMP");
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
