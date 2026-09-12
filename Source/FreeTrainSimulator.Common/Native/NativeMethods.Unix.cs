// COPYRIGHT 2026 by the Open Rails Linux Fork project.
//
// This file is part of Open Rails.
//
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FreeTrainSimulator.Common.Native
{
    /// <summary>
    /// Unix implementations of the Win32 entry points the engine calls.
    /// </summary>
    /// <remarks>
    /// The Windows build keeps the P/Invokes in GDI32.Windows.cs, User32.Windows.cs and
    /// Kernel32.Windows.cs; this file provides the same signatures for the Linux build so the
    /// call sites in shared code stay identical. Only the entry points the engine actually uses
    /// are implemented - the window management ones are exclusive to the WPF Toolbox, which the
    /// Linux build does not compile.
    /// </remarks>
    public static class NativeMethods
    {
        /// <summary>
        /// Reads a value from an initialization file. See <see cref="IniFile"/> for the
        /// behaviour this reproduces.
        /// </summary>
        public static int GetPrivateProfileString(string sectionName, string keyName, string defaultValue, StringBuilder value, int size, string fileName)
        {
            return IniFile.GetString(sectionName, keyName, defaultValue, value, size, fileName);
        }

        /// <summary>
        /// Overload taking a preallocated string buffer. Win32 writes into the caller's buffer;
        /// a .NET string is immutable, so this returns what would have been written and callers
        /// on this platform use the <see cref="StringBuilder"/> overload instead.
        /// </summary>
        public static int GetPrivateProfileString(string sectionName, string keyName, string defaultValue, string value, int size, string fileName)
        {
            _ = value;
            StringBuilder builder = new StringBuilder(size);
            return IniFile.GetString(sectionName, keyName, defaultValue, builder, size, fileName);
        }

        /// <summary>
        /// Reads every <c>key=value</c> pair of a section into a buffer of null terminated
        /// strings closed by a second null.
        /// </summary>
        public static int GetPrivateProfileSection(string sectionName, string value, int size, string fileName)
        {
            _ = value;
            int written = 0;
            foreach (string entry in IniFile.GetSection(fileName, sectionName))
            {
                if (written + entry.Length + 1 > size - 2)
                    return size - 2;
                written += entry.Length + 1;
            }
            return written;
        }

        /// <summary>
        /// Writes a value to an initialization file, creating it when needed.
        /// </summary>
        public static int WritePrivateProfileString(string sectionName, string keyName, string value, string fileName)
        {
            return IniFile.WriteValue(fileName, sectionName, keyName, value) ? 1 : 0;
        }

        /// <summary>
        /// Fills <paramref name="buffer"/> from <c>/proc/meminfo</c>.
        /// </summary>
        /// <remarks>
        /// Only the fields the engine logs are meaningful here: physical memory, its free part
        /// and the swap totals, which stand in for the Windows page file.
        /// </remarks>
        public static bool GlobalMemoryStatusEx(NativeStructs.MemoryStatusExtended buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer);

            if (!TryReadMemInfo(out ulong totalPhysical, out ulong availablePhysical, out ulong totalSwap, out ulong freeSwap))
                return false;

            buffer.TotalPhysical = totalPhysical;
            buffer.AvailablePhysical = availablePhysical;
            buffer.TotalPageFile = totalPhysical + totalSwap;
            buffer.AvailablePageFile = availablePhysical + freeSwap;
            buffer.MemoryLoad = totalPhysical == 0 ? 0 : (uint)(100 - availablePhysical * 100 / totalPhysical);

            // 64 bit Linux gives a process 128 TiB of user address space.
            buffer.TotalVirtual = 128UL * 1024 * 1024 * 1024 * 1024;
            buffer.AvailableVirtual = buffer.TotalVirtual;
            buffer.AvailableExtendedVirtual = 0;
            return true;
        }

        /// <summary>
        /// Overload for the alternately named struct the upstream code also declares.
        /// </summary>
        public static bool GlobalMemoryStatusEx(NativeStructs.MEMORYSTATUSEX buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer);

            NativeStructs.MemoryStatusExtended proxy = new NativeStructs.MemoryStatusExtended { Size = buffer.Size };
            if (!GlobalMemoryStatusEx(proxy))
                return false;

            buffer.MemoryLoad = proxy.MemoryLoad;
            buffer.TotalPhysical = proxy.TotalPhysical;
            buffer.AvailablePhysical = proxy.AvailablePhysical;
            buffer.TotalPageFile = proxy.TotalPageFile;
            buffer.AvailablePageFile = proxy.AvailablePageFile;
            buffer.TotalVirtual = proxy.TotalVirtual;
            buffer.AvailableVirtual = proxy.AvailableVirtual;
            buffer.AvailableExtendedVirtual = proxy.AvailableExtendedVirtual;
            return true;
        }

        /// <summary>
        /// Fills <paramref name="ioCounters"/> from <c>/proc/self/io</c>, which the kernel only
        /// exposes to the process itself, so <paramref name="processHandle"/> is ignored.
        /// </summary>
        public static bool GetProcessIoCounters(IntPtr processHandle, out NativeStructs.IO_COUNTERS ioCounters)
        {
            _ = processHandle;
            ioCounters = default;
            try
            {
                foreach (string line in File.ReadLines("/proc/self/io"))
                {
                    int separator = line.IndexOf(':', StringComparison.Ordinal);
                    if (separator < 0 || !ulong.TryParse(line.AsSpan(separator + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong amount))
                        continue;

                    switch (line.Substring(0, separator))
                    {
                        case "syscr": ioCounters.ReadOperationCount = amount; break;
                        case "syscw": ioCounters.WriteOperationCount = amount; break;
                        case "rchar": ioCounters.ReadTransferCount = amount; break;
                        case "wchar": ioCounters.WriteTransferCount = amount; break;
                        default: break;
                    }
                }
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// No-op. Windows needs this to load the right soft_oal.dll; on Linux the OpenAL binding
        /// is resolved to the distribution's libopenal.so.1 by a DllImport resolver instead.
        /// </summary>
        public static bool SetDllDirectory(string pathName)
        {
            _ = pathName;
            return true;
        }

        /// <summary>
        /// Returns the kernel thread id, the closest equivalent of a Win32 thread id and what a
        /// profiler needs to line its samples up with <c>top</c> or <c>perf</c>.
        /// </summary>
        public static uint GetCurrentWin32ThreadId()
        {
            try
            {
                return (uint)gettid();
            }
            catch (EntryPointNotFoundException)
            {
                return (uint)Environment.CurrentManagedThreadId;
            }
        }

        [DllImport("libc", EntryPoint = "gettid", SetLastError = true)]
        private static extern int gettid();

        /// <summary>
        /// Not applicable outside Windows; returns zero so callers treat it as "no module".
        /// </summary>
        public static IntPtr GetModuleHandle(string moduleName)
        {
            _ = moduleName;
            return IntPtr.Zero;
        }

        /// <summary>
        /// Reports the standard 96 dpi. Actual scaling on Linux comes from SDL, which the
        /// window manager reads through MonoGame.
        /// </summary>
        public static uint GetDpiForWindow(IntPtr windowHandle)
        {
            _ = windowHandle;
            return 96;
        }

        public static int GetDeviceCaps(IntPtr deviceContext, int index)
        {
            _ = deviceContext;
            return index is (int)DeviceCap.LOGPIXELSX or (int)DeviceCap.LOGPIXELSY ? 96 : 0;
        }

        public static IntPtr CreateCompatibleDC(IntPtr deviceContext) => IntPtr.Zero;

        public static bool DeleteDC(IntPtr deviceContext) => true;

        public static IntPtr SelectObject(IntPtr deviceContext, IntPtr handle) => IntPtr.Zero;

#pragma warning disable CA1008 // Enums should have zero value - mirrors the Win32 constants
        public enum DeviceCap
#pragma warning restore CA1008 // Enums should have zero value
        {
            VERTRES = 10,
            DESKTOPVERTRES = 117,
            LOGPIXELSX = 88,
            LOGPIXELSY = 90,
        }

        public enum MapVirtualKeyType
        {
            VirtualToCharacter = 2,
            VirtualToScan = 0,
            VirtualToScanEx = 4,
            ScanToVirtual = 1,
            ScanToVirtualEx = 3,
        }

        /// <summary>
        /// Translates between PC/AT set 1 scan codes and virtual key codes.
        /// </summary>
        /// <remarks>
        /// Open Rails stores its key bindings as scan codes so they survive a keyboard layout
        /// change, and turns them into XNA <c>Keys</c> - which are Windows virtual key codes -
        /// through this call. <see cref="ScanCodeMap"/> holds the table Windows would consult.
        /// </remarks>
        public static int MapVirtualKey(int code, MapVirtualKeyType type)
        {
            return type switch
            {
                MapVirtualKeyType.ScanToVirtual or MapVirtualKeyType.ScanToVirtualEx => ScanCodeMap.ToVirtualKey(code),
                MapVirtualKeyType.VirtualToScan or MapVirtualKeyType.VirtualToScanEx => ScanCodeMap.ToScanCode(code),
                _ => 0,
            };
        }

        /// <summary>
        /// Writes the display name of a key into <paramref name="name"/>.
        /// </summary>
        /// <param name="parameter">
        /// The Win32 lParam layout: bits 16-23 hold the scan code and bit 24 marks an extended
        /// key.
        /// </param>
        public static int GetKeyNameText(int parameter, StringBuilder name, int length)
        {
            ArgumentNullException.ThrowIfNull(name);

            string keyName = ScanCodeMap.GetKeyName((parameter >> 16) & 0xFF, (parameter & 0x01000000) != 0);
            if (keyName.Length > length - 1)
                keyName = keyName.Substring(0, Math.Max(0, length - 1));
            name.Clear();
            name.Append(keyName);
            return keyName.Length;
        }

        private static bool TryReadMemInfo(out ulong totalPhysical, out ulong availablePhysical, out ulong totalSwap, out ulong freeSwap)
        {
            totalPhysical = availablePhysical = totalSwap = freeSwap = 0;
            try
            {
                foreach (string line in File.ReadLines("/proc/meminfo"))
                {
                    int separator = line.IndexOf(':', StringComparison.Ordinal);
                    if (separator < 0)
                        continue;

                    ReadOnlySpan<char> amount = line.AsSpan(separator + 1).Trim();
                    // Values are reported in kB; drop the unit before parsing.
                    int space = amount.IndexOf(' ');
                    if (space > 0)
                        amount = amount.Slice(0, space);
                    if (!ulong.TryParse(amount, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong kilobytes))
                        continue;

                    switch (line.Substring(0, separator))
                    {
                        case "MemTotal": totalPhysical = kilobytes * 1024; break;
                        // MemAvailable accounts for reclaimable cache, so it is the honest
                        // answer to "how much can this process still allocate".
                        case "MemAvailable": availablePhysical = kilobytes * 1024; break;
                        case "SwapTotal": totalSwap = kilobytes * 1024; break;
                        case "SwapFree": freeSwap = kilobytes * 1024; break;
                        default: break;
                    }
                }
                return totalPhysical > 0;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

#pragma warning disable CA1707 // Identifiers should not contain underscores - mirrors Win32 names
#pragma warning disable CA1815 // Override equals on value types - mirrors the Win32 layout
    public static partial class NativeStructs
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

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

        [StructLayout(LayoutKind.Sequential, Size = 64)]
        public class MEMORYSTATUSEX
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
#pragma warning restore CA1815 // Override equals on value types
#pragma warning restore CA1707 // Identifiers should not contain underscores
}
