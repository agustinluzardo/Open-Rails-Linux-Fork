// COPYRIGHT 2026 by the Riel project.
// This file is part of Riel, a fork of Open Rails, under the GPL version 3 or later.

using System;

namespace Orts.Simulation
{
    // Profile options also apply when ActivityRunner is launched directly. Keep the existing
    // environment variables as optional overrides for terminal-driven diagnostics.
    public static class DiagnosticTrace
    {
        private static readonly bool EnvironmentAiStops = Environment.GetEnvironmentVariable("RIEL_TRACE_AI_STOPS") == "1";
        private static readonly bool EnvironmentAiProgress = Environment.GetEnvironmentVariable("RIEL_TRACE_AI_PROGRESS") == "1";
        private static readonly bool EnvironmentSignals = Environment.GetEnvironmentVariable("RIEL_TRACE_SIGNALS") == "1";
        private static readonly bool EnvironmentRoadCrossings = Environment.GetEnvironmentVariable("RIEL_TRACE_ROAD_CROSSINGS") == "1";
        private static readonly bool EnvironmentAiRouteResolution = Environment.GetEnvironmentVariable("RIEL_TRACE_AI_ROUTE") == "1";
        private static readonly bool EnvironmentSoundDiagnostics = Environment.GetEnvironmentVariable("RIEL_TRACE_SOUND") == "1";
        private static readonly bool EnvironmentLightDiagnostics = Environment.GetEnvironmentVariable("RIEL_TRACE_LIGHTS") == "1";
        private static readonly bool EnvironmentLoadMarkers = Environment.GetEnvironmentVariable("RIEL_TRACE_LOAD_MARKERS") == "1";
        private static readonly bool EnvironmentTrainVisuals = Environment.GetEnvironmentVariable("RIEL_TRACE_TRAIN_VISUALS") == "1";
        private static readonly bool EnvironmentSuppressMissingPlatforms = Environment.GetEnvironmentVariable("RIEL_SUPPRESS_MISSING_PLATFORMS") == "1";
        private static readonly string EnvironmentAiTrains = Environment.GetEnvironmentVariable("RIEL_TRACE_AI_TRAINS");

        internal static bool AiStops => EnvironmentAiStops || Simulator.Instance?.UserSettings.TraceAiStops == true;
        internal static bool AiProgress => EnvironmentAiProgress || Simulator.Instance?.UserSettings.TraceAiProgress == true;
        internal static bool Signals => EnvironmentSignals || Simulator.Instance?.UserSettings.TraceSignalDiagnostics == true;
        internal static bool RoadCrossings => EnvironmentRoadCrossings || Simulator.Instance?.UserSettings.TraceRoadCrossings == true;
        internal static bool AiRouteResolution => EnvironmentAiRouteResolution || Simulator.Instance?.UserSettings.TraceAiRouteResolution == true;
        public static bool SoundDiagnostics => EnvironmentSoundDiagnostics || Simulator.Instance?.UserSettings.TraceSoundDiagnostics == true;
        public static bool LightDiagnostics => EnvironmentLightDiagnostics || Simulator.Instance?.UserSettings.TraceLightDiagnostics == true;
        public static bool LoadMarkers => EnvironmentLoadMarkers || Simulator.Instance?.UserSettings.TraceLoadMarkers == true;
        public static bool TrainVisuals => EnvironmentTrainVisuals || Simulator.Instance?.UserSettings.TraceTrainVisualDiagnostics == true;
        internal static bool SuppressMissingPlatformWarnings => EnvironmentSuppressMissingPlatforms || Simulator.Instance?.UserSettings.SuppressMissingPlatformWarnings == true;

        public static bool AiTrain(int number) =>
            ContainsTrain(EnvironmentAiTrains, number) ||
            ContainsTrain(Simulator.Instance?.UserSettings.TraceAiTrainNumbers, number);

        private static bool ContainsTrain(string numbers, int number)
        {
            if (string.IsNullOrWhiteSpace(numbers))
                return false;

            ReadOnlySpan<char> remaining = numbers.AsSpan();
            while (!remaining.IsEmpty)
            {
                int comma = remaining.IndexOf(',');
                ReadOnlySpan<char> item = (comma < 0 ? remaining : remaining[..comma]).Trim();
                if (item.SequenceEqual("*".AsSpan()) || int.TryParse(item, out int selected) && selected == number)
                    return true;
                if (comma < 0)
                    break;
                remaining = remaining[(comma + 1)..];
            }
            return false;
        }
    }
}
