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
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;

namespace FreeTrainSimulator.Common.Display
{
    /// <summary>
    /// Display enumeration through SDL, the window system MonoGame's OpenGL backend uses.
    /// </summary>
    public static partial class DisplayDevices
    {
        private static IReadOnlyList<DisplayDevice> Enumerate()
        {
            int count;
            try
            {
                count = SdlNative.GetNumVideoDisplays();
            }
            catch (DllNotFoundException ex)
            {
                Trace.WriteLine($"Display enumeration unavailable: {ex.Message}");
                return null;
            }

            // A negative count means the video subsystem is not up yet, which is the normal state
            // until MonoGame creates the game window.
            if (count <= 0)
                return null;

            List<DisplayDevice> found = new List<DisplayDevice>(count);
            for (int index = 0; index < count; index++)
            {
                if (SdlNative.GetDisplayBounds(index, out SdlNative.Rect bounds) != 0)
                    continue;

                // Usable bounds exclude panels and docks. Not every backend implements it, so
                // fall back to the full extent.
                if (SdlNative.GetDisplayUsableBounds(index, out SdlNative.Rect usable) != 0)
                    usable = bounds;

                string name = Marshal.PtrToStringUTF8(SdlNative.GetDisplayName(index))
                    ?? string.Create(CultureInfo.InvariantCulture, $"Display {index + 1}");

                float scaling = 1f;
                if (SdlNative.GetDisplayDPI(index, out _, out float horizontalDpi, out _) == 0 && horizontalDpi > 0)
                    scaling = (float)Math.Round(horizontalDpi / 96.0, 2);

                found.Add(new DisplayDevice(
                    index,
                    name,
                    new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height),
                    new Rectangle(usable.X, usable.Y, usable.Width, usable.Height),
                    // SDL always reports the primary display first.
                    index == 0,
                    scaling));
            }
            return found;
        }
    }
}
