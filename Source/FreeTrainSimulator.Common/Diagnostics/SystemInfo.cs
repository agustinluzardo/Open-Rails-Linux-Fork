using System;
using System.Globalization;

using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Info;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Common.Diagnostics
{
    /// <remarks>
    /// Updated on the system thread, which has no graphics context: nothing here may touch the
    /// graphics device or the window. The adapter comes from what the render thread recorded, and
    /// the resolution is filled in by the game thread itself (see GameHost.Update).
    /// </remarks>
    public sealed class SystemInfo : DetailInfoBase
    {
        private readonly int processorCount = Environment.ProcessorCount;
        private readonly MetricCollector metricCollector = MetricCollector.Instance;

        public SystemInfo(Game game) : base(true)
        {
            _ = game;
            this["System Details"] = null;
            this[".0"] = null;
            this["Version"] = VersionInfo.Version;
            this["System Time"] = null;
            this["Game Time"] = null;
            this["OS"] = $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription} {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}";
            this["Framework"] = $"{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription} {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}";
            // Filled in later: this runs while the game is still being constructed, and the
            // graphics device does not exist until the game runs. The keys are declared here so
            // the overlay keeps its order.
            this["Adapter"] = null;
            this["Resolution"] = null;
            this["CPU"] = null;
            this["Memory"] = null;
            this[".0"] = null;
            this["Frame rate"] = null;
        }

        public override void Update(GameTime gameTime)
        {
            if (UpdateNeeded)
            {
                this["Adapter"] = $"{Info.SystemInfo.GraphicAdapterName ?? "n/a"} ({Info.SystemInfo.GraphicAdapterMemoryInformation})";
                this["System Time"] = DateTime.Now.ToString(CultureInfo.CurrentCulture);
                this["Game Time"] = $"{FormatStrings.FormatTime(gameTime.TotalGameTime.TotalSeconds)}";// Simulator.Instance != null ? $"{FormatStrings.FormatTime(Simulator.Instance.ClockTime)}" : null;
                this["Frame rate"] = $"{metricCollector.Metrics[SlidingMetric.FrameRate].SmoothedValue:0}";
                this["CPU"] = $"{metricCollector.Metrics[SlidingMetric.ProcessorTime].SmoothedValue / processorCount:N0}% total / {metricCollector.Metrics[SlidingMetric.ProcessorTime].SmoothedValue:0}% of single core ({processorCount} logical cores)";
                this["Memory"] = $"{Environment.WorkingSet >> 20} MB";
                base.Update(gameTime);
            }
        }

    }
}
