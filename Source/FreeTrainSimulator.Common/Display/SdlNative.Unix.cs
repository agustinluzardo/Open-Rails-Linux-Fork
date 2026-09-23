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
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FreeTrainSimulator.Common.Display
{
    /// <summary>
    /// The parts of SDL the game needs beyond what MonoGame exposes.
    /// </summary>
    /// <remarks>
    /// MonoGame's OpenGL backend runs on SDL but keeps its bindings internal, so the desktop
    /// layout and the message box are declared here. They bind to the library MonoGame already
    /// has open rather than a second copy: the resolver asks the loader for the same soname and
    /// gets back the existing handle.
    /// </remarks>
    internal static class SdlNative
    {
        internal const string LibraryName = "SDL2";

        /// <summary>
        /// Sonames to try, most specific first. The versioned one is what MonoGame ships and
        /// what distributions install; the bare names only exist with a development package.
        /// </summary>
        private static readonly string[] candidates = { "libSDL2-2.0.so.0", "libSDL2-2.0.so", "libSDL2.so.0", "libSDL2.so" };

        /// <summary>
        /// Registers the resolver once for the whole assembly. A second registration throws, so
        /// this must stay the only caller.
        /// </summary>
        [ModuleInitializer]
        internal static void Initialize()
        {
            NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), static (name, assembly, path) =>
            {
                if (name != LibraryName)
                    return IntPtr.Zero;

                foreach (string candidate in candidates)
                {
                    if (NativeLibrary.TryLoad(candidate, out IntPtr handle))
                        return handle;
                }
                return IntPtr.Zero;
            });
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect
        {
            public int X;
            public int Y;
            public int Width;
            public int Height;
        }

        [DllImport(LibraryName, EntryPoint = "SDL_GetCurrentVideoDriver", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr GetCurrentVideoDriver();

        [DllImport(LibraryName, EntryPoint = "SDL_GetNumVideoDisplays", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetNumVideoDisplays();

        [DllImport(LibraryName, EntryPoint = "SDL_GetDisplayBounds", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetDisplayBounds(int displayIndex, out Rect rect);

        [DllImport(LibraryName, EntryPoint = "SDL_GetDisplayUsableBounds", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetDisplayUsableBounds(int displayIndex, out Rect rect);

        [DllImport(LibraryName, EntryPoint = "SDL_GetDisplayName", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr GetDisplayName(int displayIndex);

        [DllImport(LibraryName, EntryPoint = "SDL_GetDisplayDPI", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetDisplayDPI(int displayIndex, out float diagonalDpi, out float horizontalDpi, out float verticalDpi);

        [DllImport(LibraryName, EntryPoint = "SDL_ShowMessageBox", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ShowMessageBox(ref MessageBoxData data, out int buttonId);

        [DllImport(LibraryName, EntryPoint = "SDL_InitSubSystem", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int InitSubSystem(uint flags);

        [DllImport(LibraryName, EntryPoint = "SDL_GL_SetAttribute", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GlSetAttribute(GlAttribute attribute, int value);

        [DllImport(LibraryName, EntryPoint = "SDL_GL_LoadLibrary", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GlLoadLibrary(IntPtr path);

        [DllImport(LibraryName, EntryPoint = "SDL_GL_ResetAttributes", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void GlResetAttributes();

        [DllImport(LibraryName, EntryPoint = "SDL_CreateWindow", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr CreateWindow([MarshalAs(UnmanagedType.LPUTF8Str)] string title, int x, int y, int width, int height, uint flags);

        [DllImport(LibraryName, EntryPoint = "SDL_DestroyWindow", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void DestroyWindow(IntPtr window);

        internal const uint InitVideo = 0x00000020;
        internal const uint WindowOpenGl = 0x00000002;
        internal const uint WindowHidden = 0x00000008;

        /// <summary>The subset of SDL_GLattr the multisample probe sets.</summary>
        internal enum GlAttribute
        {
            RedSize = 0,
            GreenSize = 1,
            BlueSize = 2,
            AlphaSize = 3,
            DoubleBuffer = 5,
            DepthSize = 6,
            StencilSize = 7,
            MultiSampleBuffers = 13,
            MultiSampleSamples = 14,
        }

        internal const uint MessageBoxError = 0x00000010;
        internal const uint MessageBoxWarning = 0x00000020;
        internal const uint MessageBoxInformation = 0x00000040;
        internal const uint ButtonReturnKeyDefault = 0x00000001;
        internal const uint ButtonEscapeKeyDefault = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        internal struct MessageBoxButtonData
        {
            public uint Flags;
            public int ButtonId;
            public IntPtr Text;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MessageBoxData
        {
            public uint Flags;
            public IntPtr Window;
            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public string Title;
            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public string Message;
            public int ButtonCount;
            public IntPtr Buttons;
            public IntPtr ColorScheme;
        }
    }
}
