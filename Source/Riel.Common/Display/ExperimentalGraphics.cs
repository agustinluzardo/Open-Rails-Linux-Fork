// COPYRIGHT 2026 by the Riel project. Licensed under GPL-3.0-or-later.

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace Riel.Common.Display
{
    /// <summary>Requests Mesa Zink before the simulator creates a graphics context.</summary>
    public static class ExperimentalGraphics
    {
        public static void Apply(bool enabled)
        {
            if (!OperatingSystem.IsLinux() || !enabled)
                return;

            SetDriverVariable("RIEL_VULKAN", "1");
            SetDriverVariable("MESA_LOADER_DRIVER_OVERRIDE", "zink");
            SetDriverVariable("GALLIUM_DRIVER", "zink");
            // NVIDIA's GLX vendor otherwise ignores Mesa's driver override.
            SetDriverVariable("__GLX_VENDOR_LIBRARY_NAME", "mesa");
            foreach (string path in new[] {
                "/etc/glvnd/egl_vendor.d/50_mesa.json",
                "/usr/share/glvnd/egl_vendor.d/50_mesa.json" })
            {
                if (File.Exists(path))
                {
                    SetDriverVariable("__EGL_VENDOR_LIBRARY_FILENAMES", path);
                    break;
                }
            }
        }

        // On Unix .NET maintains its own environment copy. Mesa/SDL use libc's
        // getenv, so updating only Environment.SetEnvironmentVariable is ineffective.
        // Call this during startup, before graphics libraries or workers are initialized.
        internal static void SetDriverVariable(string name, string value)
        {
            if (OperatingSystem.IsLinux())
            {
                int result = value == null ? UnsetEnv(name) : SetEnv(name, value, 1);
                if (result != 0)
                    throw new Win32Exception(Marshal.GetLastPInvokeError(),
                        $"Cannot configure graphics environment variable {name}");
            }
            Environment.SetEnvironmentVariable(name, value);
        }

        [DllImport("libc", EntryPoint = "setenv", SetLastError = true)]
        private static extern int SetEnv([MarshalAs(UnmanagedType.LPUTF8Str)] string name,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string value, int overwrite);

        [DllImport("libc", EntryPoint = "unsetenv", SetLastError = true)]
        private static extern int UnsetEnv([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    }
}
