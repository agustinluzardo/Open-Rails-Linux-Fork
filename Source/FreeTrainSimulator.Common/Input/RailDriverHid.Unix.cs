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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace RailDriver
{
    /// <summary>
    /// Linux replacement for the PIEHid device wrapper the RailDriver NuGet package provides.
    /// </summary>
    /// <remarks>
    /// The upstream package talks to piehid64.dll, which does not exist outside Windows. The
    /// RailDriver desk is a plain USB HID device though, so the kernel's hidraw driver exposes
    /// everything needed: <c>/sys/class/hidraw</c> lists the attached devices with their vendor
    /// and product ids, and <c>/dev/hidrawN</c> carries the reports both ways.
    ///
    /// The class name, namespace and members match the Windows package so
    /// <c>RailDriverDevice</c> compiles unchanged on both platforms.
    ///
    /// Reading <c>/dev/hidrawN</c> requires permission. Distributions do not grant it by default;
    /// the udev rule shipped in packaging/linux/udev gives the <c>input</c> group access to the
    /// desk. Without it the constructor simply finds no usable device and the game runs with the
    /// RailDriver disabled, exactly as if it were unplugged.
    /// </remarks>
    public sealed class PIEDevice : IDisposable
    {
        /// <summary>PI Engineering's USB vendor id.</summary>
        private const int PieVendorId = 0x05F3;

        private readonly string devicePath;
        private readonly object readLock = new object();
        private FileStream stream;
        private Thread readThread;
        private volatile bool running;
        private byte[] lastReport;
        private bool lastReportRead;

        private PIEDevice(string devicePath, int productId, int usagePage, int readLength, int writeLength)
        {
            this.devicePath = devicePath;
            Pid = productId;
            HidUsagePage = usagePage;
            ReadLength = readLength;
            WriteLength = writeLength;
        }

        /// <summary>USB product id, used to tell a RailDriver (210) from PI Engineering's other desks.</summary>
        public int Pid { get; }

        /// <summary>HID usage page taken from the report descriptor; the RailDriver reports 0x0C.</summary>
        public int HidUsagePage { get; }

        /// <summary>Size of an input report, including the leading report id byte.</summary>
        public int ReadLength { get; }

        /// <summary>Size of an output report, including the leading report id byte.</summary>
        public int WriteLength { get; }

        /// <summary>
        /// When set, a report identical to the previous one is not handed out again. The engine
        /// polls the desk every frame, so this keeps unchanged lever positions from being
        /// reprocessed.
        /// </summary>
        public bool SuppressDuplicateReports { get; set; }

        /// <summary>
        /// Lists the PI Engineering devices the kernel has bound to hidraw.
        /// </summary>
        public static PIEDevice[] EnumeratePIE()
        {
            List<PIEDevice> devices = new List<PIEDevice>();
            const string hidrawClass = "/sys/class/hidraw";

            if (!Directory.Exists(hidrawClass))
                return devices.ToArray();

            try
            {
                foreach (string entry in Directory.EnumerateDirectories(hidrawClass))
                {
                    string name = Path.GetFileName(entry);
                    if (!TryReadIdentity(entry, out int vendorId, out int productId) || vendorId != PieVendorId)
                        continue;

                    (int usagePage, int reportLength) = ReadReportDescriptor(Path.Combine(entry, "device", "report_descriptor"));
                    // The desk sends 7 payload bytes; the buffers PIEHid hands out include a
                    // leading report id, so both directions are one byte longer.
                    int length = (reportLength > 0 ? reportLength : 7) + 1;
                    devices.Add(new PIEDevice($"/dev/{name}", productId, usagePage, length, length));
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Trace.WriteLine($"RailDriver: unable to enumerate hidraw devices: {ex.Message}");
            }

            return devices.ToArray();
        }

        /// <summary>
        /// Opens the device and starts the background reader. Failing to open - almost always a
        /// missing udev rule - is reported to the log and leaves the device inert.
        /// </summary>
        public void SetupInterface()
        {
            if (stream != null)
                return;

            try
            {
                stream = new FileStream(devicePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, ReadLength, false);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Trace.WriteLine($"RailDriver: cannot open {devicePath} ({ex.Message}). Install the udev rule from packaging/linux/udev to grant access.");
                return;
            }

            running = true;
            readThread = new Thread(ReadLoop)
            {
                Name = "RailDriver input",
                IsBackground = true,
            };
            readThread.Start();
        }

        /// <summary>
        /// Sends an output report. Returns 0 on success and -1 when the device is not open or
        /// the write fails.
        /// </summary>
        public int WriteData(byte[] buffer)
        {
            if (stream == null || buffer == null)
                return -1;

            try
            {
                stream.Write(buffer, 0, Math.Min(buffer.Length, WriteLength));
                stream.Flush();
                return 0;
            }
            catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException)
            {
                Trace.WriteLine($"RailDriver: write failed: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Copies the most recent input report into <paramref name="data"/>. Returns 0 when a
        /// report was copied and -1 when there is nothing new.
        /// </summary>
        public int ReadLast(ref byte[] data)
        {
            if (data == null)
                return -1;

            lock (readLock)
            {
                if (lastReport == null)
                    return -1;
                if (SuppressDuplicateReports && lastReportRead)
                    return -1;

                Array.Copy(lastReport, data, Math.Min(lastReport.Length, data.Length));
                lastReportRead = true;
                return 0;
            }
        }

        /// <summary>Stops the reader and releases the device.</summary>
        public void CloseInterface()
        {
            running = false;
            FileStream current = stream;
            stream = null;

            // Closing the handle unblocks the read in progress.
            current?.Dispose();
            readThread?.Join(TimeSpan.FromSeconds(1));
            readThread = null;
        }

        public void Dispose()
        {
            CloseInterface();
        }

        private void ReadLoop()
        {
            byte[] buffer = new byte[ReadLength];
            while (running)
            {
                FileStream current = stream;
                if (current == null)
                    return;

                int read;
                try
                {
                    read = current.Read(buffer, 0, buffer.Length);
                }
                catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException)
                {
                    // Unplugging the desk mid-session ends the loop rather than the game.
                    return;
                }

                if (read <= 0)
                    return;

                lock (readLock)
                {
                    lastReport ??= new byte[ReadLength];
                    bool changed = !AsSpan(buffer, read).SequenceEqual(AsSpan(lastReport, read));
                    Array.Copy(buffer, lastReport, read);
                    if (changed)
                        lastReportRead = false;
                }
            }
        }

        private static ReadOnlySpan<byte> AsSpan(byte[] buffer, int length) => buffer.AsSpan(0, Math.Min(length, buffer.Length));

        private static bool TryReadIdentity(string hidrawEntry, out int vendorId, out int productId)
        {
            vendorId = productId = 0;
            try
            {
                // HID_ID=0003:000005F3:000000D2 - bus, vendor, product.
                foreach (string line in File.ReadLines(Path.Combine(hidrawEntry, "device", "uevent")))
                {
                    if (!line.StartsWith("HID_ID=", StringComparison.Ordinal))
                        continue;

                    string[] parts = line.Substring("HID_ID=".Length).Split(':');
                    return parts.Length == 3
                        && int.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out vendorId)
                        && int.TryParse(parts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out productId);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
            return false;
        }

        /// <summary>
        /// Pulls the usage page and the input report size out of a HID report descriptor.
        /// </summary>
        /// <remarks>
        /// Only the few short items needed here are decoded: the global Usage Page (0x05), the
        /// global Report Size (0x75) and Report Count (0x95), and the Input main item (0x81)
        /// that closes a report. Long items (prefix 0xFE) carry their own length byte.
        /// </remarks>
        private static (int UsagePage, int ReportLength) ReadReportDescriptor(string path)
        {
            try
            {
                byte[] descriptor = File.ReadAllBytes(path);
                int usagePage = 0;
                int reportSize = 0;
                int reportCount = 0;
                int inputBits = 0;

                for (int index = 0; index < descriptor.Length;)
                {
                    byte prefix = descriptor[index++];
                    if (prefix == 0xFE)
                    {
                        if (index >= descriptor.Length)
                            break;
                        index += 1 + descriptor[index];
                        continue;
                    }

                    int size = prefix & 0x03;
                    if (size == 3)
                        size = 4;
                    if (index + size > descriptor.Length)
                        break;

                    int value = 0;
                    for (int i = 0; i < size; i++)
                        value |= descriptor[index + i] << (8 * i);
                    index += size;

                    switch (prefix & 0xFC)
                    {
                        case 0x04 when usagePage == 0:
                            usagePage = value;
                            break;
                        case 0x74:
                            reportSize = value;
                            break;
                        case 0x94:
                            reportCount = value;
                            break;
                        case 0x80:
                            inputBits += reportSize * reportCount;
                            break;
                        default:
                            break;
                    }
                }

                return (usagePage, (inputBits + 7) / 8);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return (0, 0);
            }
        }
    }
}
