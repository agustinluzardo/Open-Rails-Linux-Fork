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

namespace FreeTrainSimulator.Common.Display
{
    public static partial class GraphicsCapabilities
    {
        private static bool driverHeld;

        /// <summary>
        /// Tries to create a hidden window with that many samples. SDL picks the GLX or EGL
        /// visual here, so a sample count with no matching visual fails at this point rather
        /// than later inside the graphics device, where the error says nothing useful.
        /// </summary>
        /// <remarks>
        /// The rest of the pixel format matches what MonoGame asks for, because a visual is
        /// chosen for the whole format and not for the sample count alone. The attributes are
        /// reset afterwards so this leaves nothing behind for the real window.
        /// </remarks>
        private static partial bool WindowSupportsMultiSample(int samples)
        {
            IntPtr window = IntPtr.Zero;
            try
            {
                if (SdlNative.InitSubSystem(SdlNative.InitVideo) != 0 || !HoldDriver())
                    return true;

                SdlNative.GlSetAttribute(SdlNative.GlAttribute.RedSize, 8);
                SdlNative.GlSetAttribute(SdlNative.GlAttribute.GreenSize, 8);
                SdlNative.GlSetAttribute(SdlNative.GlAttribute.BlueSize, 8);
                SdlNative.GlSetAttribute(SdlNative.GlAttribute.AlphaSize, 8);
                SdlNative.GlSetAttribute(SdlNative.GlAttribute.DepthSize, 24);
                SdlNative.GlSetAttribute(SdlNative.GlAttribute.StencilSize, 8);
                SdlNative.GlSetAttribute(SdlNative.GlAttribute.DoubleBuffer, 1);
                SdlNative.GlSetAttribute(SdlNative.GlAttribute.MultiSampleBuffers, 1);
                SdlNative.GlSetAttribute(SdlNative.GlAttribute.MultiSampleSamples, samples);

                window = SdlNative.CreateWindow("probe", 0, 0, 64, 64,
                    SdlNative.WindowOpenGl | SdlNative.WindowHidden);
                return window != IntPtr.Zero;
            }
            catch (DllNotFoundException)
            {
                // Without SDL there is no window to create; the caller finds that out soon enough.
                return true;
            }
            catch (EntryPointNotFoundException)
            {
                return true;
            }
            finally
            {
                // With the driver held, destroying the last OpenGL window no longer unloads it.
                if (window != IntPtr.Zero)
                    SdlNative.DestroyWindow(window);
                try
                {
                    SdlNative.GlResetAttributes();
                }
                catch (DllNotFoundException) { }
                catch (EntryPointNotFoundException) { }
            }
        }

        /// <summary>
        /// Loads the OpenGL driver through SDL once and keeps it loaded for the rest of the run.
        /// </summary>
        /// <remarks>
        /// The probe runs before MonoGame has a window, so its window is the first OpenGL one and
        /// SDL loads the driver for it - and unloads it again when that window, the last OpenGL
        /// window, is destroyed. On EGL, which Wayland uses, that means terminating the display and
        /// closing the libraries, only for MonoGame's window to load them again a moment later. Not
        /// every driver survives being unloaded and reloaded inside one process, and nothing is
        /// gained by it: SDL counts loads, so holding one here keeps a single driver instance from
        /// the probe through to the game's own window. It also means the driver is set up with
        /// SDL's default attributes rather than the probe's.
        /// </remarks>
        private static bool HoldDriver()
        {
            if (!driverHeld)
                driverHeld = SdlNative.GlLoadLibrary(IntPtr.Zero) == 0;
            return driverHeld;
        }
    }
}
