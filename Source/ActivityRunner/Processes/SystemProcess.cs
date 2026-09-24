using System;
using System.Collections.Generic;
using System.Diagnostics;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Diagnostics;

using Microsoft.Xna.Framework;

namespace Orts.ActivityRunner.Processes
{
    internal sealed class SystemProcess : ProcessBase
    {
        internal const double UpdateInterval = 0.25;

        private double nextUpdate;
        private readonly MetricCollector metric = MetricCollector.Instance;

        public List<DetailInfoBase> Updateables { get; } = new List<DetailInfoBase>();

        public SystemProcess(GameHost gameHost) : base(gameHost, "System")
        {
            gameHost.SystemInfo[DiagnosticInfo.System] = new SystemInfo(gameHost);
            gameHost.SystemInfo[DiagnosticInfo.Clr] = new ClrEventListener();
            gameHost.SystemInfo[DiagnosticInfo.ProcessMetric] = new PerformanceDetails();
            gameHost.SystemInfo[DiagnosticInfo.GpuMetric] = new GraphicMetrics();

            Profiler.ProfilingData[ProcessType.System] = profiler;

            Updateables.Add(gameHost.SystemInfo[DiagnosticInfo.System] as DetailInfoBase);
            Updateables.Add(gameHost.SystemInfo[DiagnosticInfo.ProcessMetric] as DetailInfoBase);
            Updateables.Add(gameHost.SystemInfo[DiagnosticInfo.GpuMetric] as DetailInfoBase);
        }

        protected override void Update(GameTime gameTime)
        {
            metric.Update(gameTime);
            if (gameHost.State is GameStateViewer3D)
            {
                // The first interval starts with the drive, so loading does not count towards it.
                double now = gameTime.TotalGameTime.TotalSeconds;
                if (double.IsNaN(nextPerformanceLog))
                    StartPerformanceInterval(now);
                else if (now > nextPerformanceLog)
                {
                    LogPerformance();
                    StartPerformanceInterval(now);
                }
            }
            if (gameTime.TotalGameTime.TotalSeconds > nextUpdate)
            {

                foreach (Profiler profiler in Profiler.ProfilingData)
                {
                    profiler?.Mark();
                }

                (gameHost.SystemInfo[DiagnosticInfo.Clr] as ClrEventListener).Update(gameTime);

                for (int i=0; i < Updateables.Count; i++)
                    Updateables[i].Update(gameTime);
                nextUpdate = gameTime.TotalGameTime.TotalSeconds + UpdateInterval;
            }
        }

        /// <summary>
        /// Every <see cref="PerformanceLogInterval"/> seconds of driving, writes to the log how fast
        /// frames came and which thread was busy.
        /// </summary>
        /// <remarks>
        /// The same figures are on the HUD, but a report of poor performance arrives as a log file,
        /// and without them the log cannot tell whether drawing, updating the world or loading it is
        /// what holds a frame up. "busy" is how much of the time each thread spent in its own work
        /// rather than waiting for another; the GC figures are for the interval.
        /// </remarks>
        private void LogPerformance()
        {
            SmoothedDataWithPercentiles frameRate = (SmoothedDataWithPercentiles)metric.Metrics[SlidingMetric.FrameRate];
            SmoothedDataWithPercentiles frameTime = (SmoothedDataWithPercentiles)metric.Metrics[SlidingMetric.FrameTime];
            int collections0 = GC.CollectionCount(0), collections1 = GC.CollectionCount(1), collections2 = GC.CollectionCount(2);
            TimeSpan paused = GC.GetTotalPauseDuration();

            string Busy(ProcessType process) => Profiler.ProfilingData[process] is Profiler profiler
                ? $"{profiler.Wall.SmoothedValue:N0}%/{profiler.CPU.SmoothedValue:N0}%"
                : "-";
            int primitives = 0, shadowPrimitives = 0;
            foreach (int count in gameHost.RenderProcess.PrimitivePerFrame ?? [])
                primitives += count;
            foreach (int count in gameHost.RenderProcess.ShadowPrimitivePerFrame ?? [])
                shadowPrimitives += count;
            Trace.TraceInformation(
                $"Performance: {frameRate.SmoothedP50:N0} fps; frame time P50/P95/P99 {frameTime.SmoothedP50 * 1000:F1}/{frameTime.SmoothedP95 * 1000:F1}/{frameTime.SmoothedP99 * 1000:F1} ms; " +
                $"{primitives:N0} primitives + {shadowPrimitives:N0} in shadow maps; " +
                $"busy/CPU render {Busy(ProcessType.Render)}, updater {Busy(ProcessType.Updater)}, loader {Busy(ProcessType.Loader)}, sound {Busy(ProcessType.Sound)}; " +
                $"GC {collections0 - lastCollections0}/{collections1 - lastCollections1}/{collections2 - lastCollections2} (gen 0/1/2), paused {(paused - lastPaused).TotalMilliseconds:N0} ms; " +
                $"{GC.GetTotalMemory(false) / 1024 / 1024} MB managed");
        }

        private void StartPerformanceInterval(double now)
        {
            nextPerformanceLog = now + PerformanceLogInterval;
            (lastCollections0, lastCollections1, lastCollections2, lastPaused) =
                (GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2), GC.GetTotalPauseDuration());
        }

        private const double PerformanceLogInterval = 30;
        private double nextPerformanceLog = double.NaN;
        private int lastCollections0, lastCollections1, lastCollections2;
        private TimeSpan lastPaused;

        private sealed class PerformanceDetails : DetailInfoBase
        {
            private readonly int processorCount = Environment.ProcessorCount;

            public PerformanceDetails()
            {
                this["Process Metrics"] = null;
                this[".0"] = null;
            }

            public override void Update(GameTime gameTime)
            {
                if (UpdateNeeded)
                {
                    this["CPU"] = $"{MetricCollector.Instance.Metrics[SlidingMetric.ProcessorTime].SmoothedValue / processorCount:N0} % total / {MetricCollector.Instance.Metrics[SlidingMetric.ProcessorTime].SmoothedValue:0} % single core";
                    this["Render process"] = $"{Profiler.ProfilingData[ProcessType.Render].CPU.SmoothedValue:N0} %";
                    this["Update process"] = $"{Profiler.ProfilingData[ProcessType.Updater].CPU.SmoothedValue:N0} %";
                    this["Loader process"] = $"{Profiler.ProfilingData[ProcessType.Loader].CPU.SmoothedValue:N0} %";
                    this["Sound process"] = $"{Profiler.ProfilingData[ProcessType.Sound].CPU.SmoothedValue:N0} %";
                    this["Background process"] = $"{Profiler.ProfilingData[ProcessType.System].CPU.SmoothedValue:N0} %";
                    this["Memory use"] = $"{Environment.WorkingSet / 1024 / 1024} Mb";
                    this["Frame rate (actual/P50/P95/P99)"] = $"{(int)MetricCollector.Instance.Metrics[SlidingMetric.FrameRate].Value} fps / {((int)(MetricCollector.Instance.Metrics[SlidingMetric.FrameRate] as SmoothedDataWithPercentiles).SmoothedP50)} fps / {(int)(MetricCollector.Instance.Metrics[SlidingMetric.FrameRate] as SmoothedDataWithPercentiles).SmoothedP95} fps / {(int)(MetricCollector.Instance.Metrics[SlidingMetric.FrameRate] as SmoothedDataWithPercentiles).SmoothedP99} fps";
                    this["Frame time (actual/P50/P95/P99)"] = $"{MetricCollector.Instance.Metrics[SlidingMetric.FrameTime].Value * 1000:F1} ms / {(MetricCollector.Instance.Metrics[SlidingMetric.FrameTime] as SmoothedDataWithPercentiles).SmoothedP50 * 1000:F1} ms / {(MetricCollector.Instance.Metrics[SlidingMetric.FrameTime] as SmoothedDataWithPercentiles).SmoothedP95 * 1000:F1} ms / {(MetricCollector.Instance.Metrics[SlidingMetric.FrameTime] as SmoothedDataWithPercentiles).SmoothedP99 * 1000:F1} ms";
                    base.Update(gameTime);
                }
            }
        }

    }
}
