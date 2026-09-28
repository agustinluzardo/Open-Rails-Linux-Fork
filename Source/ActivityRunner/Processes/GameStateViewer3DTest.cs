using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Diagnostics;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Logging;

using Orts.Simulation;

namespace Orts.ActivityRunner.Processes
{
    internal sealed class GameStateViewer3DTest : GameState
    {
        public bool Passed { get; set; }
        public double LoadTime { get; set; }

        public GameStateViewer3DTest()
        {
        }

        internal override Task Load()
        {
            Game.PopState();
            return base.Load();
        }

        protected override void Dispose(bool disposing)
        {
            ExportTestSummary(Passed, LoadTime);
            Environment.ExitCode = Passed ? 0 : 1;
            base.Dispose(disposing);
        }

        private static void ExportTestSummary(bool passed, double loadTime)
        {
            // Append to CSV file in format suitable for Excel
            string summaryFileName = Path.Combine(RuntimeInfo.UserDataFolder, "TestingSummary.csv");
            LoggingTraceListener traceListener = Trace.Listeners.OfType<LoggingTraceListener>().FirstOrDefault();
            // Could fail if already opened by Excel
            try
            {
                using (StreamWriter writer = File.AppendText(summaryFileName))
                {
                    // Route, Activity, Passed, Errors, Warnings, Infos, Load Time, Frame Rate
                    int errors = (traceListener?.EventCount(TraceEventType.Critical) ?? 0) +
                        (traceListener?.EventCount(TraceEventType.Error) ?? 0);
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4},{5},{6:F1},{7:F1}",
                        Simulator.Instance.RouteModel?.Name?.Replace(',', ';'),
                        Simulator.Instance.ActivityModel?.Name?.Replace(',', ';'),
                        passed ? "Yes" : "No", errors,
                        traceListener?.EventCount(TraceEventType.Warning) ?? 0,
                        traceListener?.EventCount(TraceEventType.Information) ?? 0,
                        loadTime, MetricCollector.Instance.Metrics[SlidingMetric.FrameRate].SmoothedValue));
                }
            }
            catch (IOException) { }// Ignore any errors
            catch (ArgumentNullException) { }// Ignore any errors
        }
    }
}
