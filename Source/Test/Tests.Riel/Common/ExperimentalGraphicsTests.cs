using System;
using System.Linq;
using System.Runtime.InteropServices;

using Riel.Common.Display;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Riel.Common
{
    [TestClass]
    public class ExperimentalGraphicsTests
    {
        private static readonly string[] Variables = {
            "RIEL_VULKAN", "MESA_LOADER_DRIVER_OVERRIDE", "GALLIUM_DRIVER",
            "__GLX_VENDOR_LIBRARY_NAME", "__EGL_VENDOR_LIBRARY_FILENAMES"
        };

        [TestMethod]
        public void VulkanRequestReachesTheNativeDriverEnvironment()
        {
            if (!OperatingSystem.IsLinux())
                return;
            var managed = Variables.ToDictionary(name => name, Environment.GetEnvironmentVariable);
            var native = Variables.ToDictionary(name => name, NativeValue);
            try
            {
                ExperimentalGraphics.Apply(false);
                foreach (string name in Variables)
                {
                    Assert.AreEqual(managed[name], Environment.GetEnvironmentVariable(name));
                    Assert.AreEqual(native[name], NativeValue(name));
                }
                ExperimentalGraphics.Apply(true);
                foreach (var setting in new[] {
                    ("RIEL_VULKAN", "1"), ("MESA_LOADER_DRIVER_OVERRIDE", "zink"),
                    ("GALLIUM_DRIVER", "zink"), ("__GLX_VENDOR_LIBRARY_NAME", "mesa") })
                {
                    Assert.AreEqual(setting.Item2, Environment.GetEnvironmentVariable(setting.Item1));
                    Assert.AreEqual(setting.Item2, NativeValue(setting.Item1),
                        "The actual native driver must see the request");
                }
            }
            finally
            {
                foreach (string name in Variables)
                {
                    ExperimentalGraphics.SetDriverVariable(name, native[name]);
                    Environment.SetEnvironmentVariable(name, managed[name]);
                }
            }
        }

        [TestMethod]
        public void NativeEnvironmentSupportsUnicodeAndRemoval()
        {
            if (!OperatingSystem.IsLinux())
                return;
            string name = "RIEL_GRAPHICS_TEST_" + Guid.NewGuid().ToString("N");
            try
            {
                ExperimentalGraphics.SetDriverVariable(name, "textura-ñ");
                Assert.AreEqual("textura-ñ", NativeValue(name));
                Assert.AreEqual("textura-ñ", Environment.GetEnvironmentVariable(name));
                ExperimentalGraphics.SetDriverVariable(name, null);
                Assert.IsNull(NativeValue(name));
                Assert.IsNull(Environment.GetEnvironmentVariable(name));
            }
            finally { ExperimentalGraphics.SetDriverVariable(name, null); }
        }

        private static string NativeValue(string name) => Marshal.PtrToStringUTF8(GetEnv(name));

        [DllImport("libc", EntryPoint = "getenv")]
        private static extern IntPtr GetEnv([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    }
}
