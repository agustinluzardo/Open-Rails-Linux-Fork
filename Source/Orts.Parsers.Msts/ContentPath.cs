// COPYRIGHT 2026 by the Riel project.
//
// GPL-3.0-or-later. Native Linux compatibility for Windows-authored MSTS content.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace Orts.Parsers.Msts
{
    /// <summary>
    /// Resolves Windows-authored MSTS content paths on case-sensitive file systems.
    /// </summary>
    public static class ContentPath
    {
        private sealed class DirectoryIndex
        {
            internal DateTime Timestamp { get; init; }
            internal Dictionary<string, List<string>> Entries { get; init; }
        }

        private enum EntryKind { File, Directory, Any }

        private static readonly ConcurrentDictionary<string, DirectoryIndex> DirectoryCache =
            new ConcurrentDictionary<string, DirectoryIndex>(StringComparer.Ordinal);

        private static readonly ConcurrentDictionary<string, string>[] ResolvedCache =
        {
            new ConcurrentDictionary<string, string>(StringComparer.Ordinal),
            new ConcurrentDictionary<string, string>(StringComparer.Ordinal),
            new ConcurrentDictionary<string, string>(StringComparer.Ordinal),
        };

        private static readonly char[] Separators = new[] { '\\', '/' };

        public static bool CaseSensitiveFileSystem { get; } = DetectCaseSensitivity();

        public static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path) || Path.DirectorySeparatorChar == '\\')
                return path;
            return path.Replace('\\', Path.DirectorySeparatorChar);
        }

        public static string ResolveFile(string path) => Resolve(path, EntryKind.File);
        public static string ResolveDirectory(string path) => Resolve(path, EntryKind.Directory);
        public static string ResolveAny(string path) => Resolve(path, EntryKind.Any);
        public static string ResolveOrOriginal(string path) => ResolveAny(path) ?? Normalize(path);
        public static bool FileExists(string path) => ResolveFile(path) != null;
        public static bool DirectoryExists(string path) => ResolveDirectory(path) != null;

        public static FileStream OpenRead(string path)
        {
            string resolved = ResolveFile(path);
            if (resolved == null)
                throw new FileNotFoundException($"Content file not found: {path}", path);
            return File.OpenRead(resolved);
        }

        private static bool Exists(string path, EntryKind kind)
        {
            return kind switch
            {
                EntryKind.File => File.Exists(path),
                EntryKind.Directory => Directory.Exists(path),
                _ => File.Exists(path) || Directory.Exists(path),
            };
        }

        private static string Resolve(string path, EntryKind kind)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            string normalized = Normalize(path);
            if (Exists(normalized, kind))
                return normalized;

            if (!CaseSensitiveFileSystem)
                return null;

            var cache = ResolvedCache[(int)kind];
            if (cache.TryGetValue(normalized, out string cached))
            {
                if (Exists(cached, kind))
                    return cached;
                cache.TryRemove(normalized, out _);
            }

            string resolved = ResolveSegments(normalized, kind);
            if (resolved != null)
                cache[normalized] = resolved;
            return resolved;
        }

        private static string ResolveSegments(string normalized, EntryKind kind)
        {
            string full;
            try
            {
                full = Path.GetFullPath(normalized);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                return null;
            }

            string root = Path.GetPathRoot(full);
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                return null;

            string[] segments = full.Substring(root.Length).Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            string current = Path.TrimEndingDirectorySeparator(root);
            if (current.Length == 0)
                current = root;

            if (segments.Length == 0)
                return Exists(current, kind) ? current : null;

            return ResolveSegment(current, segments, 0, kind);
        }

        private static string ResolveSegment(string current, string[] segments, int index, EntryKind kind)
        {
            bool last = index == segments.Length - 1;
            string segment = segments[index];

            string exact = Path.Combine(current, segment);
            string resolved = ResolveCandidate(exact, segments, index, last, kind);
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

        private static string ResolveCandidate(string candidate, string[] segments, int index, bool last, EntryKind kind)
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
            return index.Entries.TryGetValue(name, out List<string> names) ? names : Array.Empty<string>();
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

            if (DirectoryCache.TryGetValue(directory, out DirectoryIndex cached) && cached.Timestamp == timestamp)
                return cached;

            var entries = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    string name = Path.GetFileName(entry);
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
            DirectoryCache[directory] = index;
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
