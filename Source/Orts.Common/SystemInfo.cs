// COPYRIGHT 2009 - 2026 by the Open Rails project and Riel contributors.
//
// This file is part of Open Rails / Riel and is distributed under GPL-3.0-or-later.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

#if !RIEL_UNIX
using System.Management;
using SharpDX.DXGI;
#endif

namespace ORTS.Common
{
    public static class SystemInfo
    {
        static readonly Regex NameAndVersionRegex = new Regex("^(.*?) +([0-9.]+)$");

#if !RIEL_UNIX
        static readonly NativeStructs.MemoryStatusExtended MemoryStatusExtended = new NativeStructs.MemoryStatusExtended()
        {
            Size = 64
        };
#endif

        static SystemInfo()
        {
            Application = new Platform
            {
                Name = ApplicationInfo.ProductName,
                Version = VersionInfo.VersionOrBuild,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            };

            var runtime = NameAndVersionRegex.Match(RuntimeInformation.FrameworkDescription.Trim());
            Runtime = new Platform
            {
                Name = runtime.Success ? runtime.Groups[1].Value : RuntimeInformation.FrameworkDescription,
                Version = runtime.Success ? runtime.Groups[2].Value : Environment.Version.ToString(),
            };

#if RIEL_UNIX
            OperatingSystem = new Platform
            {
                Name = ReadDistribution(),
                Version = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.OSArchitecture.ToString(),
                Language = CultureInfo.CurrentUICulture.IetfLanguageTag,
                Languages = new[] { CultureInfo.CurrentUICulture.IetfLanguageTag },
            };

            InstalledMemoryMB = ReadInstalledMemoryMB();
            CPUs = ReadLinuxCpus();
            GPUs = ReadLinuxGpus();
#else
            try
            {
                var operatingSystem = new ManagementClass("Win32_OperatingSystem").GetInstances().Cast<ManagementObject>().First();
                OperatingSystem = new Platform
                {
                    Name = (string)operatingSystem["Caption"],
                    Version = (string)operatingSystem["Version"],
                    Architecture = RuntimeInformation.OSArchitecture.ToString(),
                    Language = CultureInfo.CurrentUICulture.IetfLanguageTag,
                    Languages = (string[])operatingSystem["MUILanguages"],
                };
            }
            catch (Exception error)
            {
                Trace.WriteLine(error);
                OperatingSystem = new Platform
                {
                    Name = RuntimeInformation.OSDescription,
                    Version = Environment.OSVersion.Version.ToString(),
                    Architecture = RuntimeInformation.OSArchitecture.ToString(),
                    Language = CultureInfo.CurrentUICulture.IetfLanguageTag,
                    Languages = new[] { CultureInfo.CurrentUICulture.IetfLanguageTag },
                };
            }

            NativeMethods.GlobalMemoryStatusEx(MemoryStatusExtended);
            InstalledMemoryMB = (int)(MemoryStatusExtended.TotalPhysical / 1024 / 1024);

            try
            {
                CPUs = new ManagementClass("Win32_Processor").GetInstances().Cast<ManagementObject>().Select(processor => new CPU
                {
                    Name = (string)processor["Name"],
                    Manufacturer = (string)processor["Manufacturer"],
                    ThreadCount = (uint)processor["ThreadCount"],
                    MaxClockMHz = (uint)processor["MaxClockSpeed"],
                }).ToList();
            }
            catch (Exception error)
            {
                Trace.WriteLine(error);
            }

            var descriptions = new Factory1().Adapters.Select(adapter => adapter.Description).ToArray();
            try
            {
                GPUs = new ManagementClass("Win32_VideoController").GetInstances().Cast<ManagementObject>().Select(adapter => new GPU
                {
                    Name = (string)adapter["Name"],
                    Manufacturer = (string)adapter["AdapterCompatibility"],
                    MemoryMB = (uint)((long)descriptions.FirstOrDefault(desc => desc.Description == (string)adapter["Name"]).DedicatedVideoMemory / 1024 / 1024),
                }).ToList();
            }
            catch (Exception error)
            {
                Trace.WriteLine(error);
            }

            var featureLevels = new uint[] {
                NativeMethods.D3D_FEATURE_LEVEL_12_2,
                NativeMethods.D3D_FEATURE_LEVEL_12_1,
                NativeMethods.D3D_FEATURE_LEVEL_12_0,
                NativeMethods.D3D_FEATURE_LEVEL_11_1,
                NativeMethods.D3D_FEATURE_LEVEL_11_0,
                NativeMethods.D3D_FEATURE_LEVEL_10_1,
                NativeMethods.D3D_FEATURE_LEVEL_10_0,
                NativeMethods.D3D_FEATURE_LEVEL_9_3,
                NativeMethods.D3D_FEATURE_LEVEL_9_2,
                NativeMethods.D3D_FEATURE_LEVEL_9_1,
            };
            foreach (var featureLevel in featureLevels)
            {
                var levels = new uint[] { featureLevel };
                try
                {
                    NativeMethods.D3D11CreateDevice(IntPtr.Zero, NativeMethods.D3D_DRIVER_TYPE_HARDWARE, IntPtr.Zero, 0, levels, levels.Length, NativeMethods.D3D11_SDK_VERSION, IntPtr.Zero, out uint level, IntPtr.Zero);
                    if (level == featureLevel)
                        Direct3DFeatureLevels.Add(string.Format("{0}_{1}", level >> 12 & 0xF, level >> 8 & 0xF));
                }
                catch (EntryPointNotFoundException) { }
                catch (DllNotFoundException) { }
            }
#endif
        }

        public static readonly Platform Application;
        public static readonly Platform Runtime;
        public static readonly Platform OperatingSystem;
        public static readonly int InstalledMemoryMB;
        public static List<CPU> CPUs = new List<CPU>();
        public static List<GPU> GPUs = new List<GPU>();
        public static readonly List<string> Direct3DFeatureLevels = new List<string>();

        public static void WriteSystemDetails(TextWriter output)
        {
            output.WriteLine("Date/time   = {0} ({1:u})", DateTime.Now, DateTime.UtcNow);
            output.WriteLine("Application = {0} {1} ({2})", Application.Name, Application.Version, Application.Architecture);
            output.WriteLine("Runtime     = {0} {1}", Runtime.Name, Runtime.Version);
            output.WriteLine("System      = {0} {1} ({2}; {3}; {4})", OperatingSystem.Name, OperatingSystem.Version, OperatingSystem.Architecture, OperatingSystem.Language, string.Join(",", OperatingSystem.Languages ?? Array.Empty<string>()));
            output.WriteLine("Memory      = {0:N0} MB", InstalledMemoryMB);
            foreach (var cpu in CPUs) output.WriteLine("CPU         = {0} ({1}; {2} threads; {3:N0} MHz)", cpu.Name, cpu.Manufacturer, cpu.ThreadCount, cpu.MaxClockMHz);
            foreach (var gpu in GPUs) output.WriteLine("GPU         = {0} ({1}; {2:N0} MB)", gpu.Name, gpu.Manufacturer, gpu.MemoryMB);
#if RIEL_UNIX
            output.WriteLine("Graphics    = OpenGL / DesktopGL");
#else
            output.WriteLine("Direct3D    = {0}", string.Join(",", Direct3DFeatureLevels));
#endif
        }

#if RIEL_UNIX
        static int ReadInstalledMemoryMB()
        {
            try
            {
                foreach (string line in File.ReadLines("/proc/meminfo"))
                {
                    if (!line.StartsWith("MemTotal:", StringComparison.Ordinal))
                        continue;
                    string value = new string(line.Where(char.IsDigit).ToArray());
                    if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long kb))
                        return (int)(kb / 1024);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Trace.WriteLine(ex);
            }
            return 0;
        }

        static List<CPU> ReadLinuxCpus()
        {
            var result = new List<CPU>();
            try
            {
                string model = null;
                string vendor = null;
                uint mhz = 0;
                uint threads = 0;
                foreach (string line in File.ReadLines("/proc/cpuinfo"))
                {
                    int split = line.IndexOf(':');
                    if (split < 0)
                        continue;
                    string key = line.Substring(0, split).Trim();
                    string value = line.Substring(split + 1).Trim();
                    if (key == "processor") threads++;
                    else if (key == "model name" && model == null) model = value;
                    else if (key == "vendor_id" && vendor == null) vendor = value;
                    else if (key == "cpu MHz" && mhz == 0 && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                        mhz = (uint)Math.Round(parsed);
                }
                if (threads > 0 || model != null)
                    result.Add(new CPU { Name = model ?? "CPU", Manufacturer = vendor ?? "Unknown", ThreadCount = threads, MaxClockMHz = mhz });
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Trace.WriteLine(ex);
            }
            return result;
        }

        static List<GPU> ReadLinuxGpus()
        {
            var result = new List<GPU>();
            try
            {
                if (!Directory.Exists("/sys/class/drm"))
                    return result;
                foreach (string card in Directory.EnumerateDirectories("/sys/class/drm", "card[0-9]*"))
                {
                    string device = Path.Combine(card, "device");
                    if (!Directory.Exists(device))
                        continue;
                    string vendorId = ReadText(Path.Combine(device, "vendor"));
                    string vendor = vendorId switch
                    {
                        "0x1002" => "AMD",
                        "0x10de" => "NVIDIA",
                        "0x8086" => "Intel",
                        "0x1af4" => "Virtio",
                        _ => string.IsNullOrEmpty(vendorId) ? "Unknown" : vendorId,
                    };
                    uint memoryMb = 0;
                    string vram = ReadText(Path.Combine(device, "mem_info_vram_total"));
                    if (ulong.TryParse(vram, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong bytes))
                        memoryMb = (uint)Math.Min(uint.MaxValue, bytes / 1024 / 1024);
                    result.Add(new GPU { Name = Path.GetFileName(card), Manufacturer = vendor, MemoryMB = memoryMb });
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Trace.WriteLine(ex);
            }
            return result;
        }

        static string ReadDistribution()
        {
            try
            {
                if (File.Exists("/etc/os-release"))
                {
                    foreach (string line in File.ReadLines("/etc/os-release"))
                        if (line.StartsWith("PRETTY_NAME=", StringComparison.Ordinal))
                            return line.Substring("PRETTY_NAME=".Length).Trim().Trim('"');
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Trace.WriteLine(ex);
            }
            return RuntimeInformation.OSDescription;
        }

        static string ReadText(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty; }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return string.Empty; }
        }
#endif

        public struct Platform
        {
            public string Name;
            public string Version;
            public string Architecture;
            public string Language;
            public string[] Languages;
        }

        public struct CPU
        {
            public string Name;
            public string Manufacturer;
            public uint ThreadCount;
            public uint MaxClockMHz;
        }

        public struct GPU
        {
            public string Name;
            public string Manufacturer;
            public uint MemoryMB;
        }

#if !RIEL_UNIX
        static class NativeStructs
        {
            [StructLayout(LayoutKind.Sequential, Size = 64)]
            public class MemoryStatusExtended
            {
                public uint Size;
                public uint MemoryLoad;
                public ulong TotalPhysical;
                public ulong AvailablePhysical;
                public ulong TotalPageFile;
                public ulong AvailablePageFile;
                public ulong TotalVirtual;
                public ulong AvailableVirtual;
                public ulong AvailableExtendedVirtual;
            }
        }

        static class NativeMethods
        {
            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool GlobalMemoryStatusEx([In, Out] NativeStructs.MemoryStatusExtended buffer);

            public const uint D3D11_SDK_VERSION = 7;
            public const uint D3D_DRIVER_TYPE_HARDWARE = 1;
            public const uint D3D_FEATURE_LEVEL_9_1 = 0x9100;
            public const uint D3D_FEATURE_LEVEL_9_2 = 0x9200;
            public const uint D3D_FEATURE_LEVEL_9_3 = 0x9300;
            public const uint D3D_FEATURE_LEVEL_10_0 = 0xA000;
            public const uint D3D_FEATURE_LEVEL_10_1 = 0xA100;
            public const uint D3D_FEATURE_LEVEL_11_0 = 0xB000;
            public const uint D3D_FEATURE_LEVEL_11_1 = 0xB100;
            public const uint D3D_FEATURE_LEVEL_12_0 = 0xC000;
            public const uint D3D_FEATURE_LEVEL_12_1 = 0xC100;
            public const uint D3D_FEATURE_LEVEL_12_2 = 0xC200;

            [DllImport("d3d11.dll", ExactSpelling = true)]
            public static extern uint D3D11CreateDevice(
                IntPtr adapter, uint driverType, IntPtr software, uint flags, [In] uint[] featureLevels,
                int featureLevelCount, uint sdkVersion, IntPtr device, out uint featureLevel, IntPtr immediateContext);
        }
#endif
    }
}
