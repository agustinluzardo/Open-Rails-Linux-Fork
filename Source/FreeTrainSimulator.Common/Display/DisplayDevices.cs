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
using System.Threading;

namespace FreeTrainSimulator.Common.Display
{
    /// <summary>
    /// One display attached to the machine.
    /// </summary>
    public sealed class DisplayDevice : IEquatable<DisplayDevice>
    {
        internal DisplayDevice(int index, string name, Rectangle bounds, Rectangle workingArea, bool primary, float scalingFactor)
        {
            Index = index;
            Name = name;
            Bounds = bounds;
            WorkingArea = workingArea;
            Primary = primary;
            ScalingFactor = scalingFactor;
        }

        /// <summary>Position of this display in the enumeration, and how it is persisted in the settings.</summary>
        public int Index { get; }

        public string Name { get; }

        /// <summary>The full extent of the display in desktop coordinates.</summary>
        public Rectangle Bounds { get; }

        /// <summary>The extent left after panels and docks, where a window should be placed.</summary>
        public Rectangle WorkingArea { get; }

        public bool Primary { get; }

        /// <summary>Scaling of this display, 1.0 at 96 dpi.</summary>
        public float ScalingFactor { get; }

        public bool Equals(DisplayDevice other) => other != null && other.Index == Index && string.Equals(other.Name, Name, StringComparison.Ordinal);

        public override bool Equals(object obj) => Equals(obj as DisplayDevice);

        public override int GetHashCode() => HashCode.Combine(Index, Name);

        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Name} {Bounds.Width}x{Bounds.Height} at {Bounds.X},{Bounds.Y}");
    }

    /// <summary>
    /// The displays available to the game.
    /// </summary>
    /// <remarks>
    /// The engine used to get this from WinForms' <c>Screen</c>, which does not exist outside
    /// Windows. Both platforms now go through this instead: Windows keeps asking the same Win32
    /// functions WinForms itself calls, while Linux asks SDL, which MonoGame already has open for
    /// the game window.
    ///
    /// A caveat worth knowing under Wayland: a client cannot read or set its own absolute
    /// position there, so the saved window position is advisory and the compositor decides where
    /// the window lands. Everything else - display list, sizes, scaling - is reported correctly.
    /// </remarks>
    public static partial class DisplayDevices
    {
        private static IReadOnlyList<DisplayDevice> displays;
        private static readonly Lock cacheLock = new Lock();

        /// <summary>All attached displays, in the order the platform reports them.</summary>
        public static IReadOnlyList<DisplayDevice> All
        {
            get
            {
                lock (cacheLock)
                {
                    return displays ??= EnumerateOrFallback();
                }
            }
        }

        /// <summary>The primary display, or the first one when the platform names none.</summary>
        public static DisplayDevice Primary
        {
            get
            {
                IReadOnlyList<DisplayDevice> all = All;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].Primary)
                        return all[i];
                }
                return all[0];
            }
        }

        /// <summary>
        /// Re-reads the display layout. Call after the desktop configuration changes.
        /// </summary>
        public static void Refresh()
        {
            lock (cacheLock)
            {
                displays = null;
            }
        }

        /// <summary>
        /// The display at <paramref name="index"/>, or the primary one when the index no longer
        /// matches a display - which happens whenever a monitor is unplugged between sessions.
        /// </summary>
        public static DisplayDevice At(int index)
        {
            IReadOnlyList<DisplayDevice> all = All;
            return index >= 0 && index < all.Count ? all[index] : Primary;
        }

        /// <summary>The display holding <paramref name="point"/>, or the nearest one.</summary>
        public static DisplayDevice FromPoint(Point point)
        {
            return FromRectangle(new Rectangle(point.X, point.Y, 1, 1));
        }

        /// <summary>
        /// The display holding most of <paramref name="rectangle"/>. A window straddling two
        /// monitors belongs to the one showing more of it, which is what WinForms does too.
        /// </summary>
        public static DisplayDevice FromRectangle(Rectangle rectangle)
        {
            DisplayDevice best = null;
            long bestArea = -1;

            foreach (DisplayDevice display in All)
            {
                Rectangle overlap = Rectangle.Intersect(display.Bounds, rectangle);
                long area = (long)Math.Max(0, overlap.Width) * Math.Max(0, overlap.Height);
                if (area > bestArea)
                {
                    bestArea = area;
                    best = display;
                }
            }

            // Entirely off screen - a stale saved position, or Wayland refusing to say where the
            // window is. The primary display is the sane place to put it.
            return bestArea > 0 ? best : Primary;
        }

        public static DisplayDevice FromBounds(int x, int y, int width, int height)
        {
            return FromRectangle(new Rectangle(x, y, width, height));
        }

        private static IReadOnlyList<DisplayDevice> EnumerateOrFallback()
        {
            IReadOnlyList<DisplayDevice> enumerated = Enumerate();
            if (enumerated != null && enumerated.Count > 0)
                return enumerated;

            // Nothing to query yet - the window system is not up. A single notional display
            // keeps the callers' arithmetic sane until Refresh picks up the real layout.
            return new[] { new DisplayDevice(0, "Display", new Rectangle(0, 0, 1920, 1080), new Rectangle(0, 0, 1920, 1080), true, 1f) };
        }
    }
}
