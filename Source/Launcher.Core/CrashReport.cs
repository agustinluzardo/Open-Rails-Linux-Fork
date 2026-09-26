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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

using FreeTrainSimulator.Common.Info;

namespace Riel.Launcher
{
    /// <summary>
    /// What .NET recorded when the simulator crashed in native code: the stack of the thread that
    /// crashed, from the library it died in back through the managed code that called it.
    /// </summary>
    /// <remarks>
    /// A crash inside a driver or a system library kills the process on the spot, before anything
    /// reaches the log. The runtime can still describe it: asked through its environment, it runs
    /// createdump as the process dies, and createdump writes this report - a few kilobytes of
    /// JSON rather than a core dump - with managed and native frames side by side. That is
    /// usually enough to name the part that failed, which the exit code alone never does.
    /// </remarks>
    public sealed class CrashReport
    {
        private const string FilePrefix = "ActivityRunner.";
        private const string FileSuffix = ".crashreport.json";
        private const int KeptReports = 5;

        private CrashReport(string file, string module, string caller, IReadOnlyList<string> frames)
        {
            File = file;
            Module = module;
            Caller = caller;
            Frames = frames;
        }

        /// <summary>Where crash reports are written.</summary>
        public static string Folder { get; } = Path.Combine(RuntimeInfo.LogFilesFolder, "Crashes");

        /// <summary>The report itself.</summary>
        public string File { get; }

        /// <summary>
        /// The library the crashing thread was running - libnvidia-glcore.so.550.54, say - or null
        /// when it was running managed code.
        /// </summary>
        public string Module { get; }

        /// <summary>The nearest managed method on the crashing thread's stack, or null if there is none.</summary>
        public string Caller { get; }

        /// <summary>The crashing thread's stack, innermost frame first, one line per frame.</summary>
        public IReadOnlyList<string> Frames { get; }

        /// <summary>
        /// Asks the runtime, through the environment the simulator starts with, to write a report
        /// if the process crashes. Anything already set up for dumps is left alone: someone
        /// collecting full dumps keeps getting them.
        /// </summary>
        internal static void Request(IDictionary<string, string> environment)
        {
            if (environment.ContainsKey("DOTNET_DbgEnableMiniDump") || environment.ContainsKey("COMPlus_DbgEnableMiniDump"))
                return;

            try
            {
                Directory.CreateDirectory(Folder);
                Prune();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return;
            }

            environment["DOTNET_DbgEnableMiniDump"] = "1";
            environment["DOTNET_EnableCrashReportOnly"] = "1";
            environment["DOTNET_DbgMiniDumpName"] = Path.Combine(Folder, FilePrefix + "%p");
        }

        /// <summary>The report process <paramref name="processId"/> left, or null if it left none.</summary>
        internal static CrashReport Find(int processId)
        {
            string file = Path.Combine(Folder, FilePrefix + processId.ToString(CultureInfo.InvariantCulture) + FileSuffix);
            try
            {
                return Parse(file, System.IO.File.ReadAllText(file));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>
        /// Reads a report in createdump's format. Null when it names no crashed thread, or is not
        /// a report at all - a half written one, say.
        /// </summary>
        internal static CrashReport Parse(string file, string json)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                if (!document.RootElement.TryGetProperty("payload", out JsonElement payload)
                    || !payload.TryGetProperty("threads", out JsonElement threads)
                    || threads.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                foreach (JsonElement thread in threads.EnumerateArray())
                {
                    if (Text(thread, "crashed") != "true" || !thread.TryGetProperty("stack_frames", out JsonElement stack) || stack.ValueKind != JsonValueKind.Array)
                        continue;

                    List<string> frames = new List<string>();
                    string module = null;
                    string caller = null;
                    bool first = true;
                    foreach (JsonElement frame in SkipDumpHandler(stack.EnumerateArray().ToList()))
                    {
                        bool managed = Text(frame, "is_managed") == "true";
                        if (first && !managed)
                            module = Text(frame, "native_module");
                        first = false;

                        string method = managed ? Text(frame, "method_name") : null;
                        if (method != null)
                            caller ??= method;

                        string line = Describe(frame, managed, method);
                        if (line != null)
                            frames.Add(line);
                    }
                    return new CrashReport(file, module, caller, frames);
                }
                return null;
            }
            // Malformed JSON, or JSON of the wrong shape: an array where an object belongs.
            catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// Drops the frames of the runtime's own signal handler, when the stack starts there.
        /// </summary>
        /// <remarks>
        /// For a fault, the report starts where the fault happened. When the thread was stopped some
        /// other way - by a signal sent with kill, or by the runtime aborting over an unhandled
        /// exception - it starts instead inside the handler, waiting for createdump: wait4 in libc,
        /// the runtime's frames, for a signal the kernel's trampoline in libc, and only after that
        /// the code the thread was really running.
        /// </remarks>
        private static IEnumerable<JsonElement> SkipDumpHandler(List<JsonElement> stack)
        {
            static bool IsRuntime(JsonElement frame) => Text(frame, "native_module")?.StartsWith("libcoreclr", StringComparison.Ordinal) == true;
            static bool IsLibc(JsonElement frame) => Text(frame, "native_module")?.StartsWith("libc.so", StringComparison.Ordinal) == true;

            if (stack.Count < 3 || !IsLibc(stack[0]) || !(Text(stack[0], "unmanaged_name") is "wait4" or "waitpid" or "__waitpid") || !IsRuntime(stack[1]))
                return stack;

            int index = 1;
            while (index < stack.Count && IsRuntime(stack[index]))
                index++;
            // The trampoline the kernel returns through, when the signal came from outside; an
            // abort the runtime raised itself goes straight on to the code that raised it.
            if (index < stack.Count && IsLibc(stack[index]))
                index++;
            return stack.Skip(index);
        }

        /// <summary>
        /// One frame as a line: "Namespace.Type.Method() [Assembly.dll]" for managed code,
        /// "libfoo.so!symbol+0x1c" or "libfoo.so+0x19b95c" for native code. Null for the runtime's
        /// own stubs, which have neither a name nor a module and say nothing.
        /// </summary>
        private static string Describe(JsonElement frame, bool managed, string method)
        {
            if (managed)
            {
                if (method == null)
                    return null;
                string assembly = Text(frame, "filename");
                return assembly == null ? method : $"{method} [{assembly}]";
            }

            string library = Text(frame, "native_module") ?? "?";
            string symbol = Text(frame, "unmanaged_name");
            return symbol != null
                ? $"{library}!{symbol}+{Text(frame, "native_offset") ?? "0x0"}"
                : $"{library}+{Text(frame, "native_image_offset") ?? "0x0"}";
        }

        private static string Text(JsonElement element, string property)
        {
            return element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        /// <summary>Keeps the newest few reports and deletes the rest.</summary>
        private static void Prune()
        {
            foreach (FileInfo old in new DirectoryInfo(Folder)
                .EnumerateFiles(FilePrefix + "*" + FileSuffix)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Skip(KeptReports - 1))
            {
                old.Delete();
            }
        }
    }
}
