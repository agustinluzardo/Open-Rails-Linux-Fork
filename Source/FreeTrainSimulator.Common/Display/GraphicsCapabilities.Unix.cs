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
                if (SdlNative.InitSubSystem(SdlNative.InitVideo) != 0)
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
    }
}
