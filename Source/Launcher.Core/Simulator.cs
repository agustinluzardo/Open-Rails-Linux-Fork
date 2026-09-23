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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Info;

namespace Riel.Launcher
{
    /// <summary>Finds and starts the simulator.</summary>
    public static class Simulator
    {
        private const string ExecutableName = "ActivityRunner";

        /// <summary>
        /// Finds the simulator: next to the launcher, where both the build output and the
        /// installed package put it, or wherever RIEL_SIMULATOR says.
        /// </summary>
        public static string Locate()
        {
            string configured = Environment.GetEnvironmentVariable("RIEL_SIMULATOR");
            if (!string.IsNullOrEmpty(configured))
            {
                return File.Exists(configured)
                    ? configured
                    : throw new LauncherException($"RIEL_SIMULATOR points at '{configured}', which does not exist");
            }

            string beside = Path.Combine(AppContext.BaseDirectory, ExecutableName);
            if (File.Exists(beside))
                return beside;

            throw new LauncherException(
                $"{ExecutableName} was not found next to the launcher ({AppContext.BaseDirectory}). " +
                "Set RIEL_SIMULATOR to its path.");
        }

        /// <summary>
        /// Starts the simulator with <paramref name="arguments"/> - none, to start from the saved
        /// selections - and returns a handle to wait on.
        /// </summary>
        /// <param name="captureErrors">
        /// Keep what the simulator writes to standard error so it can be shown afterwards. A
        /// terminal already shows it; a desktop launcher is the only place it would ever appear.
        /// </param>
        public static SimulatorRun Start(IReadOnlyList<string> arguments, bool captureErrors)
        {
            return new SimulatorRun(Locate(), arguments ?? Array.Empty<string>(), captureErrors);
        }
    }

    /// <summary>One run of the simulator, from start to whatever it left behind.</summary>
    public sealed class SimulatorRun : IDisposable
    {
        /// <summary>How much of standard error to keep: the end is where the reason is.</summary>
        private const int KeptErrorLines = 80;

        private readonly Process process;
        private readonly DateTime startedUtc;
        private readonly Queue<string> errorLines = new Queue<string>();
        private readonly bool captureErrors;

        internal SimulatorRun(string executable, IReadOnlyList<string> arguments, bool captureErrors)
        {
            this.captureErrors = captureErrors;

            ProcessStartInfo startInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                RedirectStandardError = captureErrors,
                WorkingDirectory = Path.GetDirectoryName(executable),
            };
            foreach (string argument in arguments)
                startInfo.ArgumentList.Add(argument);
            if (captureErrors)
                startInfo.Environment[LaunchContext.LauncherReportsErrorsVariable] = "1";

            CommandLine = string.Join(' ', new[] { executable }.Concat(arguments.Select(Quote)));

            process = new Process { StartInfo = startInfo };
            if (captureErrors)
            {
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data == null)
                        return;
                    lock (errorLines)
                    {
                        errorLines.Enqueue(e.Data);
                        while (errorLines.Count > KeptErrorLines)
                            errorLines.Dequeue();
                    }
                };
            }

            // Anything the log file records was written after this moment; the slack covers a
            // file system whose timestamps are coarser than the clock.
            startedUtc = DateTime.UtcNow.AddSeconds(-2);
            try
            {
                if (!process.Start())
                    throw new LauncherException($"could not start {executable}");
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                process.Dispose();
                throw new LauncherException($"could not start {executable}: {ex.Message}", ex);
            }
            if (captureErrors)
                process.BeginErrorReadLine();
        }

        /// <summary>The command, quoted so it can be pasted into a shell to reproduce the run.</summary>
        public string CommandLine { get; }

        /// <summary>Waits for the simulator to exit and works out how it went.</summary>
        public async Task<SimulatorOutcome> WaitAsync(CancellationToken cancellationToken = default)
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return Finish();
        }

        /// <summary>Waits for the simulator to exit and works out how it went.</summary>
        public SimulatorOutcome Wait()
        {
            process.WaitForExit();
            return Finish();
        }

        private SimulatorOutcome Finish()
        {
            // The parameterless wait is the one that also waits for the redirected stream to
            // drain, so the last lines - the ones that say why - are not lost.
            process.WaitForExit();

            string standardError = null;
            if (captureErrors)
            {
                lock (errorLines)
                    standardError = errorLines.Count > 0 ? string.Join(Environment.NewLine, errorLines) : null;
            }
            return SimulatorOutcome.Read(process.ExitCode, startedUtc, standardError);
        }

        public void Dispose()
        {
            process.Dispose();
        }

        private static string Quote(string argument)
        {
            return argument.Length > 0 && argument.All(c => char.IsLetterOrDigit(c) || "-_./:=+".Contains(c))
                ? argument
                : "'" + argument.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
        }
    }

    /// <summary>How a run ended, and where to look if it went wrong.</summary>
    public sealed class SimulatorOutcome
    {
        private const string FatalMarker = "FatalException";
        private const int ExcerptLines = 14;

        internal SimulatorOutcome(int exitCode, string logFile, string fatalError, string standardError)
        {
            ExitCode = exitCode;
            LogFile = logFile;
            FatalError = fatalError;
            StandardError = standardError;
            (CauseType, Cause) = FindCause(fatalError, standardError);
        }

        public int ExitCode { get; }

        /// <summary>The log this run wrote, or null if it never got as far as opening one.</summary>
        public string LogFile { get; }

        /// <summary>
        /// The fatal error the simulator logged, from the error line through the cause and the
        /// first frames of the stack; null when there was none.
        /// </summary>
        /// <remarks>
        /// Only a fatal error counts. The log also carries errors the simulator carried on past -
        /// a texture that would not load, a signal script with a typo - and treating those as a
        /// failed run would put an alarming dialog after every normal session.
        /// </remarks>
        public string FatalError { get; }

        /// <summary>The end of what the simulator wrote to standard error, when it was kept.</summary>
        public string StandardError { get; }

        /// <summary>
        /// The one sentence that says why: the message of the innermost exception, which is the
        /// actual failure - "Could not find file ..." - rather than the wrapper that reported it.
        /// Null when the simulator gave no reason at all.
        /// </summary>
        public string Cause { get; }

        /// <summary>The innermost exception's type, without its namespace; null with no cause.</summary>
        public string CauseType { get; }

        /// <summary>
        /// Whether the run failed. An unhandled exception exits non-zero and lands on standard
        /// error, before or outside the log; a failure the simulator caught lands in the log
        /// as a fatal error and exits normally. Either is a failure.
        /// </summary>
        public bool Failed => ExitCode != 0 || FatalError != null;

        internal static SimulatorOutcome Read(int exitCode, DateTime startedUtc, string standardError)
        {
            string logFile = FindLog(startedUtc);
            string fatal = logFile == null ? null : FindFatalError(logFile);
            return new SimulatorOutcome(exitCode, logFile, fatal, standardError);
        }

        /// <summary>
        /// The log written by this run: the newest in the log folder changed since it started.
        /// Each run replaces its log, so there is no older content to mistake for this one.
        /// </summary>
        private static string FindLog(DateTime startedUtc)
        {
            try
            {
                return new DirectoryInfo(RuntimeInfo.LogFilesFolder)
                    .EnumerateFiles("*.txt")
                    .Where(file => file.LastWriteTimeUtc >= startedUtc)
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .Select(file => file.FullName)
                    .FirstOrDefault();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>
        /// Exceptions that never explain anything to a user by themselves: wrappers, and the null
        /// reference that is only ever a symptom. The cause is the nearest exception that is not
        /// one of these.
        /// </summary>
        private static readonly string[] uninformative =
        {
            "FatalException",
            "AggregateException",
            "TargetInvocationException",
            "NullReferenceException",
        };

        /// <summary>
        /// Picks the exception that explains a failure out of one as .NET prints it - the chain
        /// of "Type: message" lines joined by "--->" - preferring the logged fatal error and
        /// falling back to an unhandled exception on standard error.
        /// </summary>
        /// <remarks>
        /// The innermost exception is usually the real failure: "Could not find file ..." inside
        /// the wrapper that reported it. Not always - a graphics device that could not be created
        /// ends in a null reference inside the driver binding, and the outer exception is the one
        /// that says what failed - so wrappers and null references are passed over when anything
        /// else in the chain says more.
        /// </remarks>
        internal static (string Type, string Message) FindCause(string fatalError, string standardError)
        {
            foreach (string text in new[] { fatalError, standardError })
            {
                if (string.IsNullOrEmpty(text))
                    continue;

                List<(string Type, string Message)> chain = new List<(string, string)>();
                foreach (string raw in text.Split('\n'))
                {
                    string line = raw.Trim();
                    string entry = line.StartsWith("--->", StringComparison.Ordinal) ? line[4..].Trim()
                        : line.StartsWith("Unhandled exception.", StringComparison.Ordinal) ? line["Unhandled exception.".Length..].Trim()
                        : line.StartsWith("Error: ", StringComparison.Ordinal) ? line["Error: ".Length..].Trim()
                        : line.StartsWith("Critical: ", StringComparison.Ordinal) ? line["Critical: ".Length..].Trim()
                        : null;
                    if (!string.IsNullOrEmpty(entry))
                        chain.Add(Split(entry));
                }
                if (chain.Count == 0)
                    continue;

                for (int i = chain.Count - 1; i >= 0; i--)
                {
                    if (chain[i].Type == null || Array.IndexOf(uninformative, chain[i].Type) < 0)
                        return chain[i];
                }
                return chain[^1];
            }
            return (null, null);
        }

        /// <summary>
        /// "System.IO.FileNotFoundException: Could not find file '...'" into its type, without the
        /// namespace, and its message. The type is the part before the first ": " when it looks
        /// like one - dotted, no spaces, ending in Exception - and otherwise there is none.
        /// </summary>
        private static (string Type, string Message) Split(string entry)
        {
            int colon = entry.IndexOf(": ", StringComparison.Ordinal);
            if (colon > 0)
            {
                string type = entry[..colon];
                if (type.IndexOf(' ', StringComparison.Ordinal) < 0 && type.EndsWith("Exception", StringComparison.Ordinal))
                    return (type[(type.LastIndexOf('.') + 1)..], entry[(colon + 2)..].Trim());
            }
            return (null, entry);
        }

        private static string FindFatalError(string logFile)
        {
            string[] lines;
            try
            {
                // Shared read: a simulator that is still shutting down may hold the file open.
                using FileStream stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using StreamReader reader = new StreamReader(stream);
                lines = reader.ReadToEnd().Split('\n');
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                bool fatal = (line.StartsWith("Error: ", StringComparison.Ordinal) && line.Contains(FatalMarker, StringComparison.Ordinal))
                    || line.StartsWith("Critical: ", StringComparison.Ordinal);
                if (fatal)
                    return string.Join('\n', lines.Skip(i).Take(ExcerptLines).Select(l => l.TrimEnd('\r'))).Trim();
            }
            return null;
        }
    }
}
