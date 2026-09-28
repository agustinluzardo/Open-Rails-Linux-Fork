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
    /// Hardware monitoring is optional; an unavailable sensor is reported as n/a.
    /// </summary>
    internal sealed class HardwareTemperatures
    {
        private const string HwmonPath = "/sys/class/hwmon";
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);

        private Task<(string Cpu, string Gpu)> pendingSample;
        private DateTime nextRefreshUtc;
        private string cpu = "n/a";
        private string gpu = "n/a";

        public (string Cpu, string Gpu) Read(string adapterName)
        {
            if (!OperatingSystem.IsLinux())
                return (cpu, gpu);

            if (pendingSample?.IsCompleted == true)
            {
                if (pendingSample.IsCompletedSuccessfully)
                    (cpu, gpu) = pendingSample.Result;
                else
                    _ = pendingSample.Exception;
                pendingSample = null;
            }

            if (pendingSample == null && DateTime.UtcNow >= nextRefreshUtc)
            {
                nextRefreshUtc = DateTime.UtcNow + RefreshInterval;
                pendingSample = Task.Run(() => Sample(adapterName));
            }

            return (cpu, gpu);
        }

        private static (string Cpu, string Gpu) Sample(string adapterName)
        {
            double? cpuTemperature = ReadHwmon("coretemp", "Package id")
                ?? ReadHwmon("k10temp", "Tdie")
                ?? ReadHwmon("zenpower", "Tdie");

            double? gpuTemperature = adapterName?.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) == true
                ? ReadNvidiaTemperature(adapterName)
                : adapterName?.Contains("AMD", StringComparison.OrdinalIgnoreCase) == true ||
                  adapterName?.Contains("Radeon", StringComparison.OrdinalIgnoreCase) == true
                    ? ReadHwmon("amdgpu", "edge")
                    : null;

            return (Format(cpuTemperature), Format(gpuTemperature));
        }

        private static string Format(double? value) => value.HasValue
            ? $"{value.Value:0.#} °C"
            : "n/a";

        private static double? ReadHwmon(string sensorName, string preferredLabel)
        {
            if (!Directory.Exists(HwmonPath))
                return null;

            try
            {
                double? fallback = null;
                foreach (string sensor in Directory.EnumerateDirectories(HwmonPath, "hwmon*"))
                {
                    if (!string.Equals(ReadText(Path.Combine(sensor, "name")), sensorName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    foreach (string inputPath in Directory.EnumerateFiles(sensor, "temp*_input"))
                    {
                        if (!long.TryParse(ReadText(inputPath), NumberStyles.Integer, CultureInfo.InvariantCulture, out long milliCelsius) ||
                            milliCelsius <= 0 || milliCelsius >= 125000)
                            continue;

                        double temperature = milliCelsius / 1000.0;
                        string labelPath = inputPath.Substring(0, inputPath.Length - "_input".Length) + "_label";
                        if (ReadText(labelPath)?.StartsWith(preferredLabel, StringComparison.OrdinalIgnoreCase) == true)
                            return temperature;
                        fallback ??= temperature;
                    }
                }
                return fallback;
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

        private static double? ReadNvidiaTemperature(string adapterName)
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
                if (!process.WaitForExit(1500))
                {
                    process.Kill(entireProcessTree: true);
                    return null;
                }
                if (process.ExitCode != 0)
                    return null;

                string[] lines = process.StandardOutput.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
                double? onlyGpuTemperature = null;
                int gpuCount = 0;
                foreach (string line in lines)
                {
                    int separator = line.LastIndexOf(',');
                    if (separator < 0 || !double.TryParse(line.AsSpan(separator + 1).Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out double temperature) || temperature <= 0 || temperature >= 125)
                        continue;

                    gpuCount++;
                    onlyGpuTemperature = temperature;
                    if (adapterName.Contains(line.Substring(0, separator).Trim(), StringComparison.OrdinalIgnoreCase))
                        return temperature;
                }

                // One installed GPU is unambiguous even when the OpenGL and NVML names differ.
                return gpuCount == 1 ? onlyGpuTemperature : null;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                Win32Exception or InvalidOperationException)
            {
                return null;
            }
        }
    }
}
