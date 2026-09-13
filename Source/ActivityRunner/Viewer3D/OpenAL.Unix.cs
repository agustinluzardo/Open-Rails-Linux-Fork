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
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Orts.ActivityRunner.Viewer3D
{
    /// <summary>
    /// Points the OpenAL bindings at the platform's own OpenAL Soft.
    /// </summary>
    /// <remarks>
    /// The bindings in OpenAL.cs name <c>soft_oal.dll</c>, the file name OpenAL Soft ships under
    /// on Windows. The library is the same one here, just called libopenal: MonoGame's OpenGL
    /// backend brings a copy next to the game, and every distribution packages it as well. Both
    /// are tried so the game works whether it runs from a build directory or from a system
    /// package that dropped the bundled copy.
    /// </remarks>
    internal static class OpenALLibrary
    {
        private const string WindowsName = "soft_oal.dll";

        private static readonly string[] candidates =
        {
            // What MonoGame.Library.OpenAL copies into runtimes/<rid>/native.
            "libopenal.so",
            // What the distributions install.
            "libopenal.so.1",
            "libopenal.so.1.24.0",
            "libopenal.so.1.23.1",
        };

        [ModuleInitializer]
        internal static void Initialize()
        {
            NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), static (name, assembly, path) =>
            {
                if (!string.Equals(name, WindowsName, StringComparison.OrdinalIgnoreCase))
                    return IntPtr.Zero;

                foreach (string candidate in candidates)
                {
                    if (NativeLibrary.TryLoad(candidate, assembly, path, out IntPtr handle))
                        return handle;
                }

                Trace.TraceError("OpenAL could not be loaded. Install openal (or openal-soft) and try again; the simulator will run without sound.");
                return IntPtr.Zero;
            });
        }

        /// <summary>
        /// The OpenAL Soft configuration file for this user.
        /// </summary>
        /// <remarks>
        /// OpenAL Soft reads alsoft.ini from the roaming profile on Windows and alsoft.conf from
        /// the XDG configuration directory here. SpecialFolder.ApplicationData already resolves
        /// to $XDG_CONFIG_HOME (or ~/.config), so only the file name differs.
        /// </remarks>
        internal static string ConfigurationFile => Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "alsoft.conf");
    }
}
