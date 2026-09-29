using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace Riel.Common.Diagnostics
{
    /// <summary>
    /// Samples Linux hardware sensors without holding up the game or system update threads.
    /// Hardware monitoring is optional; an unavailable or ambiguous sensor is reported as n/a.
    /// </summary>
    internal sealed class HardwareTemperatures
    {
        private const string HwmonPath = "/sys/class/hwmon";
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);

        private Task<(string Cpu, string Gpu)> pendingSample;
        private string pendingAdapterName;
        private string observedAdapterName;
        private DateTime nextRefreshUtc;
        private string cpu = "n/a";
        private string gpu = "n/a";

        public (string Cpu, string Gpu) Read(string adapterName)
        {
            if (!OperatingSystem.IsLinux())
                return (cpu, gpu);

            if (!string.Equals(adapterName, observedAdapterName, StringComparison.Ordinal))
            {
                observedAdapterName = adapterName;
                gpu = "n/a";
                nextRefreshUtc = DateTime.MinValue;
            }

            if (pendingSample?.IsCompleted == true)
            {
                if (pendingSample.IsCompletedSuccessfully)
                {
                    (string currentCpu, string currentGpu) = pendingSample.Result;
                    cpu = currentCpu;
                    if (string.Equals(pendingAdapterName, observedAdapterName, StringComparison.Ordinal))
                        gpu = currentGpu;
                }
                else
                    _ = pendingSample.Exception;
                pendingSample = null;
            }

            if (pendingSample == null && DateTime.UtcNow >= nextRefreshUtc)
            {
                nextRefreshUtc = DateTime.UtcNow + RefreshInterval;
                pendingAdapterName = adapterName;
                pendingSample = Task.Run(() => Sample(adapterName, HwmonPath, ReadNvidiaOutput));
            }

            return (cpu, gpu);
        }

        // The path and command output are supplied by tests so sensor selection can be checked
        // without depending on the test machine's particular hardware.
        internal static (string Cpu, string Gpu) Sample(string adapterName, string hwmonPath, Func<string> nvidiaOutput)
        {
            string cpuTemperature = Format(ReadHwmon(hwmonPath, "coretemp", "Package id"))
                ?? Format(ReadHwmon(hwmonPath, "k10temp", "Tdie"))
                ?? Format(ReadHwmon(hwmonPath, "zenpower", "Tdie"))
                ?? WithSource(ReadHwmon(hwmonPath, "k10temp", "Tctl"), "Tctl")
                ?? WithSource(ReadHwmon(hwmonPath, "zenpower", "Tctl"), "Tctl")
                ?? WithSource(ReadHwmon(hwmonPath, "coretemp", "Core "), "core")
                ?? "n/a";

            double? gpuTemperature = null;
            if (!string.IsNullOrWhiteSpace(adapterName))
            {
                if (adapterName.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                    adapterName.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
                    gpuTemperature = ReadHwmon(hwmonPath, "amdgpu", "edge", requireSingleDevice: true);
                else if (IsNvidiaRenderer(adapterName) ||
                         adapterName.Contains("zink", StringComparison.OrdinalIgnoreCase))
                    gpuTemperature = ParseNvidiaTemperatures(nvidiaOutput(), adapterName);
            }

            return (cpuTemperature, Format(gpuTemperature) ?? "n/a");
        }

        private static string WithSource(double? value, string source) =>
            Format(value) is string temperature ? $"{temperature} ({source})" : null;

        private static string Format(double? value) => value.HasValue
            ? $"{value.Value:0.#} °C"
            : null;

        private static bool IsNvidiaRenderer(string renderer) =>
            renderer.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
            renderer.Contains("GeForce", StringComparison.OrdinalIgnoreCase) ||
            renderer.Contains("Quadro", StringComparison.OrdinalIgnoreCase) ||
            renderer.Contains("Tesla", StringComparison.OrdinalIgnoreCase) ||
            renderer.Contains("TITAN", StringComparison.OrdinalIgnoreCase);

        private static double? ReadHwmon(string root, string sensorName, string labelPrefix, bool requireSingleDevice = false)
        {
            if (!Directory.Exists(root))
                return null;

            try
            {
                int deviceCount = 0;
                double? hottest = null;
                foreach (string sensor in Directory.EnumerateDirectories(root, "hwmon*"))
                {
                    if (!string.Equals(ReadText(Path.Combine(sensor, "name")), sensorName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    deviceCount++;
                    foreach (string inputPath in Directory.EnumerateFiles(sensor, "temp*_input"))
                    {
                        string labelPath = inputPath.Substring(0, inputPath.Length - "_input".Length) + "_label";
                        if (ReadText(labelPath)?.StartsWith(labelPrefix, StringComparison.OrdinalIgnoreCase) != true ||
                            !long.TryParse(ReadText(inputPath), NumberStyles.Integer, CultureInfo.InvariantCulture, out long milliCelsius) ||
                            milliCelsius <= 0 || milliCelsius >= 125000)
                            continue;

                        double temperature = milliCelsius / 1000.0;
                        hottest = Math.Max(hottest ?? temperature, temperature);
                    }
                }

                // A GL renderer name does not identify a particular AMD hwmon device. Picking
                // the first of two cards could show the temperature of an idle, unrelated GPU.
                return requireSingleDevice && deviceCount != 1 ? null : hottest;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static string ReadText(string path)
        {
            try
            {
                return File.ReadAllText(path).Trim();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        internal static double? ParseNvidiaTemperatures(string output, string adapterName)
        {
            if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(adapterName))
                return null;

            double? soleTemperature = null;
            double? matchedTemperature = null;
            int gpuCount = 0;
            int matchCount = 0;
            foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = line.LastIndexOf(',');
                if (separator < 0 || !double.TryParse(line.AsSpan(separator + 1).Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double temperature) || temperature <= 0 || temperature >= 125)
                    continue;

                string name = line.Substring(0, separator).Trim();
                if (name.Length == 0)
                    continue;

                gpuCount++;
                soleTemperature = temperature;
                // NVML may prefix a name with NVIDIA when Mesa's renderer does not.
                string withoutVendor = name.StartsWith("NVIDIA ", StringComparison.OrdinalIgnoreCase)
                    ? name.Substring("NVIDIA ".Length) : name;
                if (adapterName.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                    adapterName.Contains(withoutVendor, StringComparison.OrdinalIgnoreCase))
                {
                    matchCount++;
                    matchedTemperature = temperature;
                }
            }

            if (matchCount > 0)
                return matchCount == 1 ? matchedTemperature : null;

            // A single NVIDIA device is safe even if GL and NVML spell its name differently.
            // With an unspecified Zink renderer, an NVIDIA sensor could belong to another GPU.
            return gpuCount == 1 && IsNvidiaRenderer(adapterName)
                ? soleTemperature : null;
        }

        private static string ReadNvidiaOutput()
        {
            try
            {
                using Process process = new Process
                {
                    StartInfo = new ProcessStartInfo("nvidia-smi")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        ArgumentList = { "--query-gpu=name,temperature.gpu", "--format=csv,noheader,nounits" },
                    },
                };

                if (!process.Start())
                    return null;
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> errors = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(1500))
                {
                    process.Kill(entireProcessTree: true);
                    return null;
                }
                if (process.ExitCode != 0)
                    return null;

                _ = errors.GetAwaiter().GetResult();
                return output.GetAwaiter().GetResult();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                Win32Exception or InvalidOperationException)
            {
                return null;
            }
        }
    }
}
