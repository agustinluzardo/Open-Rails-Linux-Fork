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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Info;

namespace Riel.Launcher
{
    /// <summary>
    /// Parts of the simulator a run can leave out, to get past a crash in a driver and to find
    /// out which driver it was.
    /// </summary>
    [Flags]
    public enum SafeMode
    {
        None = 0,
        /// <summary>No audio device is opened at all.</summary>
        NoSound = 1,
        /// <summary>A window, no antialiasing, no dynamic shadows, no hardware instancing.</summary>
        BasicGraphics = 2,
    }

    /// <summary>The part of the system a native crash most likely came from.</summary>
    public enum CrashSuspect
    {
        Unknown,
        /// <summary>SDL or the window system.</summary>
        Window,
        /// <summary>OpenGL and the graphics driver.</summary>
        Graphics,
        /// <summary>OpenAL and the sound server.</summary>
        Sound,
    }

    /// <summary>Finds and starts the simulator.</summary>
    public static class Simulator
    {
        private const string ExecutableName = "RunActivity";

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

            string packaged = Path.Combine(AppContext.BaseDirectory, "engine", ExecutableName);
            if (File.Exists(packaged))
                return packaged;

            string beside = Path.Combine(AppContext.BaseDirectory, ExecutableName);
            if (File.Exists(beside))
                return beside;

            throw new LauncherException(
                $"{ExecutableName} was not found in the packaged engine directory or next to the launcher ({AppContext.BaseDirectory}). " +
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
        /// <param name="safeMode">What to leave out of this run; nothing, normally.</param>
        public static SimulatorRun Start(IReadOnlyList<string> arguments, bool captureErrors, SafeMode safeMode = SafeMode.None)
        {
            return new SimulatorRun(Locate(), arguments ?? Array.Empty<string>(), captureErrors, safeMode);
        }
    }

    /// <summary>One run of the simulator, from start to whatever it left behind.</summary>
    public sealed class SimulatorRun : IDisposable
    {
        /// <summary>How much of standard error to keep: the end is where the reason is.</summary>
        private const int KeptErrorLines = 80;

        /// <summary>What createdump says while it writes a crash report; the report itself is read instead.</summary>
        private const string CreateDumpPrefix = "[createdump]";

        private readonly Process process;
        private readonly DateTime startedUtc;
        private readonly Queue<string> errorLines = new Queue<string>();
        private readonly bool captureErrors;

        internal SimulatorRun(string executable, IReadOnlyList<string> arguments, bool captureErrors, SafeMode safeMode)
        {
            this.captureErrors = captureErrors;
            SafeMode = safeMode;

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
            if (safeMode.HasFlag(SafeMode.NoSound))
                startInfo.Environment[LaunchContext.NoSoundVariable] = "1";
            if (safeMode.HasFlag(SafeMode.BasicGraphics))
                startInfo.Environment[LaunchContext.BasicGraphicsVariable] = "1";
            CrashReport.Request(startInfo.Environment);

            CommandLine = string.Join(' ', new[] { executable }.Concat(arguments.Select(Quote)));

            process = new Process { StartInfo = startInfo };
            if (captureErrors)
            {
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data == null || e.Data.StartsWith(CreateDumpPrefix, StringComparison.Ordinal))
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
            ProcessId = process.Id;
        }

        /// <summary>The command, quoted so it can be pasted into a shell to reproduce the run.</summary>
        public string CommandLine { get; }

        /// <summary>What this run leaves out.</summary>
        public SafeMode SafeMode { get; }

        /// <summary>The simulator's process id, which its crash report and startup trail carry.</summary>
        public int ProcessId { get; }

        /// <summary>Earliest log time attributable to this run.</summary>
        public DateTime StartedUtc => startedUtc;

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
            return SimulatorOutcome.Read(process.ExitCode, startedUtc, standardError, ProcessId);
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

        internal SimulatorOutcome(int exitCode, string logFile, string fatalError, string standardError,
            IReadOnlyList<StartupStep> trail = null, CrashReport crash = null)
        {
            ExitCode = exitCode;
            LogFile = logFile;
            FatalError = fatalError;
            StandardError = standardError;
            (CauseType, Cause) = FindCause(fatalError, standardError);
            Trail = trail ?? Array.Empty<StartupStep>();
            Crash = crash;
            Stage = StageAtExit(Trail);
            Suspect = FindSuspect(crash, Stage);
        }

        public int ExitCode { get; }

        /// <summary>
        /// The signal that ended the simulator, when one did - 11 for a segmentation fault. A
        /// process killed by a signal reports 128 plus its number as the exit code.
        /// </summary>
        public int? Signal => ExitCode > 128 && ExitCode <= 128 + 64 ? ExitCode - 128 : null;

        /// <summary>The signal's name, such as SIGSEGV; null when no signal ended the run.</summary>
        public string SignalName => Signal switch
        {
            null => null,
            4 => "SIGILL",
            6 => "SIGABRT",
            7 => "SIGBUS",
            8 => "SIGFPE",
            9 => "SIGKILL",
            11 => "SIGSEGV",
            15 => "SIGTERM",
            int other => "signal " + other.ToString(CultureInfo.InvariantCulture),
        };

        /// <summary>
        /// Whether the simulator crashed in native code: killed by one of the signals a fault
        /// raises, with no .NET exception to explain it. An unhandled exception also ends in
        /// SIGABRT, but it prints itself first and then it is the exception that says why.
        /// </summary>
        public bool CrashedNatively => Signal is 4 or 6 or 7 or 8 or 11 && Cause == null;

        /// <summary>How far the simulator got while starting, oldest step first; empty if it recorded none.</summary>
        public IReadOnlyList<StartupStep> Trail { get; }

        /// <summary>
        /// What the simulator was doing when it stopped, going by its startup trail; null when it
        /// left none. For the steps that run side by side, the one that had not finished wins.
        /// </summary>
        public StartupStage? Stage { get; }

        /// <summary>The runtime's report of a native crash, when it wrote one.</summary>
        public CrashReport Crash { get; }

        /// <summary>The part of the system a native crash most likely came from.</summary>
        public CrashSuspect Suspect { get; }

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

        internal static SimulatorOutcome Read(int exitCode, DateTime startedUtc, string standardError, int processId)
        {
            string logFile = FindLog(startedUtc);
            string fatal = logFile == null ? null : FindFatalError(logFile);
            if (exitCode == 0 && fatal == null)
                return new SimulatorOutcome(exitCode, logFile, fatal, standardError);

            return new SimulatorOutcome(exitCode, logFile, fatal, standardError,
                StartupTrail.Read(processId), CrashReport.Find(processId));
        }

        /// <summary>Reconstructs a completed run after the graphical launcher restarts.</summary>
        public static SimulatorOutcome ReadCompletedRun(int exitCode, DateTime startedUtc, string standardError, int processId)
            => Read(exitCode, startedUtc, standardError, processId);

        internal static StartupStage? StageAtExit(IReadOnlyList<StartupStep> trail)
        {
            if (trail.Count == 0)
                return null;

            // Three threads report here once the device exists: the game thread showing the
            // window, the sound thread and the loader. Whichever of them was still in the middle of
            // something is the one to blame, the window first since it holds up everything else.
            bool Reached(StartupStage stage) => trail.Any(step => step.Stage == stage);
            if (Reached(StartupStage.Running))
                return StartupStage.Running;
            if (Reached(StartupStage.GraphicsDeviceReady) && !Reached(StartupStage.FirstFrame))
                return StartupStage.GraphicsDeviceReady;
            if (Reached(StartupStage.StartingSound) && !Reached(StartupStage.SoundReady))
                return StartupStage.StartingSound;
            if (Reached(StartupStage.Loading))
                return StartupStage.Loading;
            return trail
                .Select(step => step.Stage)
                .Where(stage => stage != StartupStage.StartingSound && stage != StartupStage.SoundReady)
                .DefaultIfEmpty(StartupStage.Started)
                .Max();
        }

        private static readonly string[] soundLibraries = { "openal", "pipewire", "libspa", "pulse", "asound", "jack" };
        private static readonly string[] graphicsLibraries =
        {
            "nvidia", "libgl", "libegl", "glx", "_dri", "dri_", "gallium", "radeon", "amdgpu", "iris", "i965",
            "crocus", "nouveau", "swrast", "llvm", "mesa", "zink", "vulkan", "libdrm",
        };
        private static readonly string[] windowLibraries = { "libsdl", "libx11", "libxcb", "libxrandr", "libxi.", "wayland", "libdecor" };

        /// <summary>
        /// Names the likely culprit of a native crash: from the library it happened in when that
        /// says, then from the managed code that called it, then from the step it happened in.
        /// </summary>
        internal static CrashSuspect FindSuspect(CrashReport crash, StartupStage? stage)
        {
            string module = crash?.Module?.ToLowerInvariant();
            if (module != null)
            {
                if (soundLibraries.Any(module.Contains))
                    return CrashSuspect.Sound;
                if (graphicsLibraries.Any(module.Contains))
                    return CrashSuspect.Graphics;
                if (windowLibraries.Any(module.Contains))
                    return CrashSuspect.Window;
            }

            string caller = crash?.Caller;
            if (caller != null)
            {
                if (caller.Contains("OpenAL", StringComparison.Ordinal) || caller.Contains(".Sound", StringComparison.Ordinal) || caller.Contains(".Audio.", StringComparison.Ordinal))
                    return CrashSuspect.Sound;
                if (caller.StartsWith("Microsoft.Xna.Framework.Graphics.", StringComparison.Ordinal) || caller.StartsWith("MonoGame.OpenGL.", StringComparison.Ordinal))
                    return CrashSuspect.Graphics;
                if (caller.Contains(".Sdl", StringComparison.Ordinal))
                    return CrashSuspect.Window;
            }

            return stage switch
            {
                StartupStage.OpeningWindow => CrashSuspect.Window,
                StartupStage.CreatingGraphicsDevice or StartupStage.GraphicsDeviceReady or StartupStage.FirstFrame => CrashSuspect.Graphics,
                StartupStage.StartingSound => CrashSuspect.Sound,
                _ => CrashSuspect.Unknown,
            };
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
