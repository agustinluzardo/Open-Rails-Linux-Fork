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

namespace FreeTrainSimulator.Common.Info
{
    /// <summary>
    /// The steps of starting the simulator, in the order they are reached.
    /// </summary>
    /// <remarks>
    /// Sound and loading start on their own threads while the game thread shows the window, so
    /// <see cref="FirstFrame"/>, <see cref="StartingSound"/>, <see cref="SoundReady"/> and
    /// <see cref="Loading"/> can come in any order after <see cref="GraphicsDeviceReady"/>.
    /// </remarks>
    public enum StartupStage
    {
        /// <summary>The process is running and has read its settings.</summary>
        Started,
        /// <summary>SDL is starting and the game window is being created.</summary>
        OpeningWindow,
        /// <summary>The OpenGL context is being created, antialiasing included.</summary>
        CreatingGraphicsDevice,
        /// <summary>The graphics device exists and the screen mode has been applied.</summary>
        GraphicsDeviceReady,
        /// <summary>The window is showing and the first frame has been presented.</summary>
        FirstFrame,
        /// <summary>The audio device is being opened.</summary>
        StartingSound,
        /// <summary>The audio device is open.</summary>
        SoundReady,
        /// <summary>The loading screen is up and the route is loading.</summary>
        Loading,
        /// <summary>Loading finished and the simulation is running.</summary>
        Running,
    }

    /// <summary>
    /// A record of how far the simulator got while starting, kept so that a crash in native code -
    /// which ends the process on the spot, before anything reaches the log - still leaves behind
    /// the step it happened in.
    /// </summary>
    /// <remarks>
    /// Each step is appended and the file closed straight away, so what was written survives the
    /// process dying at any point afterwards. Once the simulation runs there is nothing more to
    /// add and the regular log takes over.
    /// </remarks>
    public static class StartupTrail
    {
        public const string FileName = "Startup.log";

        private const string ProcessPrefix = "pid ";

        private static readonly object gate = new object();
        private static readonly Stopwatch clock = new Stopwatch();
        private static string trailFile;

        /// <summary>The trail of the most recent run.</summary>
        public static string FilePath => Path.Combine(RuntimeInfo.LogFilesFolder, FileName);

        /// <summary>Starts a new trail for this process, replacing the previous run's.</summary>
        public static void Begin()
        {
            Begin(FilePath, Environment.ProcessId);
        }

        internal static void Begin(string file, int processId)
        {
            lock (gate)
            {
                trailFile = null;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    File.WriteAllText(file, ProcessPrefix + processId.ToString(CultureInfo.InvariantCulture) + "\n");
                    trailFile = file;
                    clock.Restart();
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    // Nowhere to write it: the run goes ahead without a trail.
                }
            }
            Mark(StartupStage.Started);
        }

        /// <summary>Records that <paramref name="stage"/> was reached.</summary>
        /// <param name="detail">What it found, where that is worth knowing: the graphics card, say.</param>
        public static void Mark(StartupStage stage, string detail = null)
        {
            lock (gate)
            {
                if (trailFile == null)
                    return;
                try
                {
                    string line = string.Create(CultureInfo.InvariantCulture, $"{clock.ElapsedMilliseconds}\t{stage}");
                    if (!string.IsNullOrWhiteSpace(detail))
                        line += "\t" + detail.ReplaceLineEndings(" ").Trim();
                    File.AppendAllText(trailFile, line + "\n");
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    trailFile = null;
                }
            }
        }

        /// <summary>
        /// The steps process <paramref name="processId"/> recorded, oldest first; empty when the
        /// trail belongs to another run or there is none.
        /// </summary>
        public static IReadOnlyList<StartupStep> Read(int processId)
        {
            return Read(FilePath, processId);
        }

        internal static IReadOnlyList<StartupStep> Read(string file, int processId)
        {
            List<StartupStep> steps = new List<StartupStep>();
            string[] lines;
            try
            {
                lines = File.ReadAllLines(file);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return steps;
            }

            if (lines.Length == 0 || lines[0] != ProcessPrefix + processId.ToString(CultureInfo.InvariantCulture))
                return steps;

            for (int i = 1; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split('\t', 3);
                if (parts.Length >= 2
                    && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out long elapsed)
                    && Enum.TryParse(parts[1], out StartupStage stage))
                {
                    steps.Add(new StartupStep(stage, TimeSpan.FromMilliseconds(elapsed), parts.Length > 2 ? parts[2] : null));
                }
            }
            return steps;
        }
    }

    /// <summary>One step of a startup trail.</summary>
    /// <param name="Elapsed">How long after the process started the step was reached.</param>
    public sealed record StartupStep(StartupStage Stage, TimeSpan Elapsed, string Detail);
}
