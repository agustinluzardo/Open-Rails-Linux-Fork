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
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;

namespace FreeTrainSimulator.Common.Display
{
    /// <summary>
    /// Display enumeration through the Win32 monitor functions.
    /// </summary>
    /// <remarks>
    /// These are the same calls WinForms' <c>Screen</c> makes, so the results match what the
    /// engine saw before, without the Windows Forms dependency that would keep the shared code
    /// from compiling elsewhere.
    /// </remarks>
    public static partial class DisplayDevices
    {
        private static IReadOnlyList<DisplayDevice> Enumerate()
        {
            List<DisplayDevice> found = new List<DisplayDevice>();

            bool Callback(IntPtr monitor, IntPtr deviceContext, ref NativeRectangle rectangle, IntPtr data)
            {
                MonitorInfoEx info = new MonitorInfoEx { Size = Marshal.SizeOf<MonitorInfoEx>() };
                if (!GetMonitorInfo(monitor, ref info))
                    return true;

                float scaling = 1f;
                try
                {
                    // Effective dpi accounts for the user's scaling setting, which is what the
                    // interface should be sized against.
                    if (GetDpiForMonitor(monitor, MonitorDpiType.Effective, out uint dpiX, out _) == 0 && dpiX > 0)
                        scaling = (float)Math.Round(dpiX / 96.0, 2);
                }
                catch (DllNotFoundException)
                {
                    // shcore.dll predates Windows 8.1; leave the scaling at 1.
                }

                found.Add(new DisplayDevice(
                    found.Count,
                    info.DeviceName ?? string.Create(CultureInfo.InvariantCulture, $"Display {found.Count + 1}"),
                    ToRectangle(info.Monitor),
                    ToRectangle(info.WorkArea),
                    (info.Flags & MonitorInfoPrimary) != 0,
                    scaling));
                return true;
            }

            try
            {
                if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero))
                    return null;
            }
            catch (DllNotFoundException)
            {
                return null;
            }
            return found;
        }

        private static Rectangle ToRectangle(NativeRectangle rectangle)
        {
            return new Rectangle(rectangle.Left, rectangle.Top, rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top);
        }

        private const int MonitorInfoPrimary = 0x01;

        private delegate bool MonitorEnumCallback(IntPtr monitor, IntPtr deviceContext, ref NativeRectangle rectangle, IntPtr data);

        private enum MonitorDpiType
        {
            Effective = 0,
            Angular = 1,
            Raw = 2,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRectangle
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MonitorInfoEx
        {
            public int Size;
            public NativeRectangle Monitor;
            public NativeRectangle WorkArea;
            public int Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern bool EnumDisplayMonitors(IntPtr deviceContext, IntPtr clip, MonitorEnumCallback callback, IntPtr data);

        [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);

        [DllImport("shcore.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int GetDpiForMonitor(IntPtr monitor, MonitorDpiType type, out uint dpiX, out uint dpiY);
    }
}
