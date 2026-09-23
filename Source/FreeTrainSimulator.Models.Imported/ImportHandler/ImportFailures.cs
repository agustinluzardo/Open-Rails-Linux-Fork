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
using System.Diagnostics;
using System.Threading.Tasks;

using Orts.Formats.Msts.Parsers;

namespace FreeTrainSimulator.Models.Imported.ImportHandler
{
    /// <summary>A content file the scan passed over, and why.</summary>
    public sealed record SkippedFile(string Kind, string Path, string Reason);

    /// <summary>
    /// Keeps one unreadable file from ending the scan of everything else.
    /// </summary>
    /// <remarks>
    /// Third party MSTS content is full of files that are slightly wrong - a missing block, a
    /// stray bracket, a value in the wrong unit - which Microsoft's own reader tolerated. A scan
    /// that stops at the first of them lists no routes at all, which looks exactly like the
    /// content folder being wrong. So a file that cannot be read is skipped, the rest carries on,
    /// and what was skipped is kept so the launcher can say so by name.
    /// </remarks>
    public static class ImportFailures
    {
        private static readonly ConcurrentQueue<SkippedFile> skipped = new ConcurrentQueue<SkippedFile>();

        /// <summary>
        /// Awaits the import of one file, answering null when the file could not be read.
        /// Cancellation still cancels: that is the user stopping the scan, not a bad file.
        /// </summary>
        internal static async Task<T> Guard<T>(Task<T> import, string kind, string path) where T : class
        {
            ArgumentNullException.ThrowIfNull(import);
            try
            {
                return await import.ConfigureAwait(false);
            }
            catch (Exception ex) when (Skippable(ex))
            {
                Record(kind, path, ex);
                return null;
            }
        }

        /// <summary>Notes a file that was passed over; also used where a reader already copes.</summary>
        internal static void Record(string kind, string path, Exception reason)
        {
            string message = reason?.Message ?? "unreadable";
            Trace.TraceWarning($"Skipped the {kind} {path}, which could not be read: {message}");
            skipped.Enqueue(new SkippedFile(kind, path, message));
        }

        /// <summary>Everything skipped since the last call, which is then forgotten.</summary>
        public static IReadOnlyList<SkippedFile> Take()
        {
            List<SkippedFile> result = new List<SkippedFile>();
            while (skipped.TryDequeue(out SkippedFile file))
                result.Add(file);
            return result;
        }

        internal static bool Skippable(Exception ex)
        {
            return ex is not OperationCanceledException && (ex is STFException || ex is SystemException);
        }
    }
}
