// COPYRIGHT 2026 by the Riel project.
// This file is part of Riel, a fork of Open Rails, under the GPL version 3 or later.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

using FreeTrainSimulator.Models.Shim;

namespace Riel.Launcher.Gui
{
    /// <summary>
    /// A small, windowless launcher process owns the simulator and reopens the graphical
    /// launcher when it exits. The Avalonia process and its resources leave memory while driving.
    /// </summary>
    internal static class LaunchSession
    {
        internal sealed class Request
        {
            public string[] Arguments { get; set; }
            public string Title { get; set; }
            public string RouteId { get; set; }
            public string FolderName { get; set; }
            public SafeMode SafeMode { get; set; }
        }

        internal sealed class Report
        {
            public Request Request { get; set; }
            public int ExitCode { get; set; }
            public DateTime StartedUtc { get; set; }
            public int ProcessId { get; set; }
            public string StandardError { get; set; }
            public string CommandLine { get; set; }
            public string StartError { get; set; }
        }

        private static string LauncherPath => Path.Combine(AppContext.BaseDirectory,
            OperatingSystem.IsWindows() ? "riel-gui.exe" : "riel-gui");

        internal static void Start(IReadOnlyList<string> arguments, string title, string routeId, string folderName, SafeMode safeMode)
        {
            var request = new Request
            {
                Arguments = new List<string>(arguments).ToArray(), Title = title,
                RouteId = routeId, FolderName = folderName, SafeMode = safeMode
            };
            var start = new ProcessStartInfo(LauncherPath)
            {
                UseShellExecute = false,
                WorkingDirectory = AppContext.BaseDirectory,
            };
            start.ArgumentList.Add("--supervise");
            start.ArgumentList.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
            using Process supervisor = Process.Start(start)
                ?? throw new LauncherException("Could not start the launcher supervisor.");
        }

        internal static int Supervise(string encodedRequest)
        {
            Request request = JsonSerializer.Deserialize<Request>(Convert.FromBase64String(encodedRequest))
                ?? throw new InvalidDataException("The launch request is empty.");
            Report report = null;
            try
            {
                using SimulatorRun run = Simulator.Start(request.Arguments, captureErrors: true, request.SafeMode);
                SimulatorOutcome outcome = run.Wait();
                if (outcome.Failed)
                    report = new Report
                    {
                        Request = request, ExitCode = outcome.ExitCode, StartedUtc = run.StartedUtc,
                        ProcessId = run.ProcessId, StandardError = outcome.StandardError,
                        CommandLine = run.CommandLine
                    };
            }
            catch (Exception error)
            {
                report = new Report { Request = request, StartError = error.ToString() };
            }

            string reportPath = null;
            try
            {
                if (report != null)
                {
                    reportPath = Path.GetTempFileName();
                    File.WriteAllText(reportPath, JsonSerializer.Serialize(report));
                }
                var restart = new ProcessStartInfo(LauncherPath)
                {
                    UseShellExecute = false,
                    WorkingDirectory = AppContext.BaseDirectory,
                };
                if (reportPath != null)
                {
                    restart.ArgumentList.Add("--run-report");
                    restart.ArgumentList.Add(reportPath);
                }
                using Process launcher = Process.Start(restart)
                    ?? throw new LauncherException("Could not reopen the launcher.");
                return 0;
            }
            catch
            {
                if (reportPath != null)
                    File.Delete(reportPath);
                throw;
            }
        }

        internal static Report ReadReport(string path)
        {
            try
            {
                return JsonSerializer.Deserialize<Report>(File.ReadAllText(path))
                    ?? throw new InvalidDataException("The simulator report is empty.");
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
