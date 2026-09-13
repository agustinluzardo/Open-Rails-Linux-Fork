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
using System.Globalization;
using System.IO;
using System.Text;

using FreeTrainSimulator.Common.Native;

using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Common.Info
{
    /// <summary>
    /// Linux hardware inventory, read from /proc, /sys and DMI.
    /// </summary>
    /// <remarks>
    /// The Windows build queries WMI for the same details. Everything here is diagnostics that
    /// ends up in the log next to a bug report, so each reader degrades to "n/a" rather than
    /// failing: /sys/class/dmi needs no privileges to read on a normal desktop but is absent in
    /// containers and on some ARM boards, and the drm nodes are missing when the session runs on
    /// llvmpipe.
    /// </remarks>
    public static partial class SystemInfo
    {
        public static string GraphicAdapterMemoryInformation { get; private set; }
        public static string CpuInformation { get; private set; }

        /// <summary>
        /// Reports the memory of the adapter the game is running on, taken from the DRM node
        /// whose name matches <paramref name="adapterName"/>, or from the first node when the
        /// names do not line up.
        /// </summary>
        public static string SetGraphicAdapterInformation(string adapterName)
        {
            GraphicAdapterMemoryInformation ??= ReadGraphicsMemory(adapterName) ?? "n/a";
            return GraphicAdapterMemoryInformation;
        }

        private static void WriteEnvironment(StringBuilder output)
        {
            ArgumentNullException.ThrowIfNull(output);

            output.AppendLine(CultureInfo.InvariantCulture, $"{"BIOS",-12}= {ReadDmi("bios_version")} ({ReadDmi("bios_vendor")})");
            output.AppendLine(CultureInfo.InvariantCulture, $"{"System",-12}= {ReadDmi("sys_vendor")} {ReadDmi("product_name")}");

            CpuInformation = ReadProcessorInformation();
            output.AppendLine(CpuInformation);

            NativeStructs.MemoryStatusExtended memory = new NativeStructs.MemoryStatusExtended { Size = 64 };
            if (NativeMethods.GlobalMemoryStatusEx(memory))
                output.AppendLine(CultureInfo.InvariantCulture, $"{"Memory",-12}= {memory.TotalPhysical / 1024f / 1024 / 1024:F1} GB");

            foreach (string card in ReadGraphicsCards())
                output.AppendLine(CultureInfo.InvariantCulture, $"{"Video",-12}= {card}");

            // The OpenGL backend has no DeviceName or IsDefaultAdapter; SDL reports one adapter
            // per display and the description is all it carries.
            foreach (GraphicsAdapter adapter in GraphicsAdapter.Adapters)
                output.AppendLine(CultureInfo.InvariantCulture, $"{"Display",-12}= {adapter.Description} (resolution {adapter.CurrentDisplayMode.Width} x {adapter.CurrentDisplayMode.Height}{(adapter == GraphicsAdapter.DefaultAdapter ? ", primary" : "")})");

            foreach (string device in ReadSoundDevices())
                output.AppendLine(CultureInfo.InvariantCulture, $"{"Sound",-12}= {device}");

            foreach (DriveInfo drive in GetReadyDrives())
                output.AppendLine(CultureInfo.InvariantCulture, $"{"Disk",-12}= {drive.Name} ({drive.DriveType}, {drive.DriveFormat}, {drive.TotalSize / 1024f / 1024 / 1024:F1} GB, {drive.AvailableFreeSpace / 1024f / 1024 / 1024:F1} GB free)");

            output.AppendLine(CultureInfo.InvariantCulture, $"{"OS",-12}= {ReadDistribution()} {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture} ({Environment.OSVersion.Version})");
        }

        private static IEnumerable<DriveInfo> GetReadyDrives()
        {
            DriveInfo[] drives;
            try
            {
                drives = DriveInfo.GetDrives();
            }
            catch (IOException)
            {
                yield break;
            }

            foreach (DriveInfo drive in drives)
            {
                bool usable;
                try
                {
                    // Pseudo file systems report themselves as ready but have no size; listing
                    // every cgroup and tmpfs mount would bury the real disks.
                    usable = drive.IsReady && drive.TotalSize > 0 &&
                        drive.DriveType is DriveType.Fixed or DriveType.Removable or DriveType.Network;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    usable = false;
                }
                if (usable)
                    yield return drive;
            }
        }

        private static string ReadProcessorInformation()
        {
            string model = "unknown";
            double megahertz = 0;
            int threads = 0;
            HashSet<string> cores = new HashSet<string>(StringComparer.Ordinal);
            string physicalId = string.Empty;

            try
            {
                foreach (string line in File.ReadLines("/proc/cpuinfo"))
                {
                    int separator = line.IndexOf(':', StringComparison.Ordinal);
                    if (separator < 0)
                        continue;

                    string key = line.Substring(0, separator).Trim();
                    string value = line.Substring(separator + 1).Trim();
                    switch (key)
                    {
                        case "model name":
                            model = value;
                            break;
                        case "processor":
                            threads++;
                            break;
                        case "cpu MHz":
                            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double current))
                                megahertz = Math.Max(megahertz, current);
                            break;
                        case "physical id":
                            physicalId = value;
                            break;
                        case "core id":
                            // A core is only unique together with the socket it sits in.
                            cores.Add($"{physicalId}:{value}");
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }

            if (threads == 0)
                threads = Environment.ProcessorCount;
            if (cores.Count == 0)
                cores.Add("0");

            // The nominal maximum is more useful than whatever the governor happens to be doing.
            double maximum = ReadDouble("/sys/devices/system/cpu/cpu0/cpufreq/cpuinfo_max_freq") / 1000;
            if (maximum > 0)
                megahertz = maximum;

            return string.Create(CultureInfo.InvariantCulture,
                $"{"Processor",-12}= {model} ({threads} threads, {cores.Count} cores, {megahertz / 1000:F1} GHz)");
        }

        private static IEnumerable<string> ReadGraphicsCards()
        {
            List<string> cards = new List<string>();
            try
            {
                foreach (string card in Directory.EnumerateDirectories("/sys/class/drm", "card?"))
                {
                    string device = Path.Combine(card, "device");
                    string vendor = ReadText(Path.Combine(device, "vendor"));
                    string identifier = ReadText(Path.Combine(device, "device"));
                    string driver = string.Empty;
                    try
                    {
                        string driverLink = Path.Combine(device, "driver");
                        if (Directory.Exists(driverLink))
                            driver = Path.GetFileName(Directory.ResolveLinkTarget(driverLink, true)?.FullName ?? string.Empty);
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                    }

                    string memory = FormatVideoMemory(device);
                    cards.Add($"{DescribePciVendor(vendor)} {identifier} ({driver}{memory})");
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
            return cards;
        }

        private static string ReadGraphicsMemory(string adapterName)
        {
            _ = adapterName;
            try
            {
                foreach (string card in Directory.EnumerateDirectories("/sys/class/drm", "card?"))
                {
                    string memory = FormatVideoMemory(Path.Combine(card, "device"));
                    if (!string.IsNullOrEmpty(memory))
                        return memory.TrimStart(',', ' ');
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
            return null;
        }

        private static string FormatVideoMemory(string devicePath)
        {
            // amdgpu and nouveau expose the VRAM size here; the i915 driver does not, and
            // NVIDIA's proprietary driver reports it only through NVML.
            double bytes = ReadDouble(Path.Combine(devicePath, "mem_info_vram_total"));
            return bytes > 0
                ? string.Create(CultureInfo.InvariantCulture, $", {bytes / 1024 / 1024:F0} MB RAM")
                : string.Empty;
        }

        private static string DescribePciVendor(string vendor)
        {
            return vendor switch
            {
                "0x1002" => "AMD",
                "0x10de" => "NVIDIA",
                "0x8086" => "Intel",
                "0x1af4" => "Virtio",
                "0x1234" => "QEMU",
                _ => string.IsNullOrEmpty(vendor) ? "Unknown" : vendor,
            };
        }

        private static IEnumerable<string> ReadSoundDevices()
        {
            List<string> devices = new List<string>();
            try
            {
                foreach (string card in Directory.EnumerateDirectories("/proc/asound", "card?"))
                {
                    string identifier = ReadText(Path.Combine(card, "id"));
                    if (!string.IsNullOrEmpty(identifier))
                        devices.Add(identifier);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
            return devices;
        }

        private static string ReadDistribution()
        {
            try
            {
                foreach (string line in File.ReadLines("/etc/os-release"))
                {
                    if (line.StartsWith("PRETTY_NAME=", StringComparison.Ordinal))
                        return line.Substring("PRETTY_NAME=".Length).Trim('"');
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
            return System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        }

        private static string ReadDmi(string name)
        {
            string value = ReadText($"/sys/class/dmi/id/{name}");
            return string.IsNullOrEmpty(value) ? "n/a" : value;
        }

        private static string ReadText(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return string.Empty;
            }
        }

        private static double ReadDouble(string path)
        {
            return double.TryParse(ReadText(path), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0;
        }
    }
}
