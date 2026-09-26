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
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FxcBridge
{
    /// <summary>
    /// Compiles one shader function with D3DCompile, for mgfxc to call through Wine.
    /// </summary>
    /// <remarks>
    /// mgfxc runs <c>wine64 dotnet c:\fxccs.dll &lt;source&gt; &lt;entry point&gt; &lt;profile&gt;
    /// &lt;flags&gt; &lt;display path&gt; &lt;output&gt;</c> for every shader in an effect, reads the
    /// bytecode back from the output file and treats a non-zero exit code as a failure. This
    /// program implements exactly that contract.
    ///
    /// Installed into the Wine prefix as dotnet.exe, it is handed the c:\fxccs.dll argument that
    /// the real muxer would have loaded; that argument is recognised and skipped.
    /// </remarks>
    internal static class Program
    {
        /// <summary>
        /// Loads the HLSL compiler that sits next to this executable.
        /// </summary>
        /// <remarks>
        /// mgfxc runs the prefix with WINEDLLOVERRIDES=d3dcompiler_47=n - native only - because
        /// its own setup expects Microsoft's redistributable. Wine's implementation is a perfectly
        /// good PE module, but Wine recognises its own builtins even when they are loaded from
        /// disk, so that override rejects it.
        ///
        /// scripts/build-shaders.sh therefore copies it in under a different file name. The
        /// override keys off the module name, so a copy called something else loads normally, and
        /// no Microsoft redistributable is needed.
        /// </remarks>
        [ModuleInitializer]
        internal static void Initialize()
        {
            NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), static (name, assembly, path) =>
            {
                if (!string.Equals(name, CompilerLibrary, StringComparison.OrdinalIgnoreCase))
                    return IntPtr.Zero;

                foreach (string candidate in new[] { PrivateCompilerLibrary, CompilerLibrary })
                {
                    string local = Path.Combine(AppContext.BaseDirectory, candidate);
                    if (File.Exists(local) && NativeLibrary.TryLoad(local, out IntPtr handle))
                        return handle;
                }
                return NativeLibrary.TryLoad(CompilerLibrary, out IntPtr fallback) ? fallback : IntPtr.Zero;
            });
        }

        private const string CompilerLibrary = "d3dcompiler_47.dll";

        /// <summary>The renamed copy of the compiler, invisible to the DLL override.</summary>
        private const string PrivateCompilerLibrary = "hlslcompiler.dll";

        private static int Main(string[] args)
        {
            try
            {
                // Drop the assembly argument when we are standing in for the dotnet muxer.
                if (args.Length > 0 && args[0].EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    args = args[1..];

                // MonoGame 3.8.1 passes five arguments after fxccs.dll; newer releases add a
                // separate display path before the output file. Support both protocols so the
                // bridge stays tied to the Open Rails MonoGame version instead of the tool host.
                if (args.Length < 5)
                {
                    Console.Error.WriteLine("usage: fxcbridge <source file> <entry point> <profile> <flags> [display path] <output file>");
                    return 2;
                }

                string source = File.ReadAllText(args[0]);
                string entryPoint = args[1];
                string profile = args[2];
                uint flags = uint.TryParse(args[3], out uint parsed) ? parsed : 0;
                string displayPath = args.Length >= 6 ? args[4] : args[0];
                string outputPath = args.Length >= 6 ? args[5] : args[4];

                byte[] bytecode = Compile(source, entryPoint, profile, flags, displayPath, out string errors);
                if (!string.IsNullOrEmpty(errors))
                    Console.Error.WriteLine(errors);

                if (bytecode == null)
                    return 1;

                File.WriteAllBytes(outputPath, bytecode);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 3;
            }
        }

        private static unsafe byte[] Compile(string source, string entryPoint, string profile, uint flags, string displayPath, out string errors)
        {
            errors = null;

            byte[] sourceBytes = System.Text.Encoding.UTF8.GetBytes(source);
            IntPtr entryPointName = Marshal.StringToHGlobalAnsi(entryPoint);
            IntPtr target = Marshal.StringToHGlobalAnsi(profile);
            IntPtr sourceName = Marshal.StringToHGlobalAnsi(displayPath);

            try
            {
                fixed (byte* data = sourceBytes)
                {
                    int result = D3DCompile(
                        (IntPtr)data, (nuint)sourceBytes.Length, sourceName,
                        IntPtr.Zero, IntPtr.Zero,
                        entryPointName, target, flags, 0,
                        out IntPtr code, out IntPtr messages);

                    errors = ReadBlobText(messages);
                    Release(messages);

                    if (result < 0 || code == IntPtr.Zero)
                    {
                        Release(code);
                        errors ??= $"D3DCompile failed with 0x{result:X8}.";
                        return null;
                    }

                    byte[] bytecode = ReadBlobBytes(code);
                    Release(code);
                    return bytecode;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(entryPointName);
                Marshal.FreeHGlobal(target);
                Marshal.FreeHGlobal(sourceName);
            }
        }

        // ID3DBlob is a plain COM object; its vtable is IUnknown's three entries followed by
        // GetBufferPointer and GetBufferSize, which is all that is needed to read the result.
        private const int VtableRelease = 2;
        private const int VtableGetBufferPointer = 3;
        private const int VtableGetBufferSize = 4;

        private static unsafe IntPtr GetBufferPointer(IntPtr blob)
        {
            IntPtr* vtable = *(IntPtr**)blob;
            return ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr>)vtable[VtableGetBufferPointer])(blob);
        }

        private static unsafe nuint GetBufferSize(IntPtr blob)
        {
            IntPtr* vtable = *(IntPtr**)blob;
            return ((delegate* unmanaged[Stdcall]<IntPtr, nuint>)vtable[VtableGetBufferSize])(blob);
        }

        private static unsafe void Release(IntPtr blob)
        {
            if (blob == IntPtr.Zero)
                return;
            IntPtr* vtable = *(IntPtr**)blob;
            _ = ((delegate* unmanaged[Stdcall]<IntPtr, uint>)vtable[VtableRelease])(blob);
        }

        private static byte[] ReadBlobBytes(IntPtr blob)
        {
            if (blob == IntPtr.Zero)
                return null;
            int size = checked((int)GetBufferSize(blob));
            byte[] bytes = new byte[size];
            Marshal.Copy(GetBufferPointer(blob), bytes, 0, size);
            return bytes;
        }

        private static string ReadBlobText(IntPtr blob)
        {
            if (blob == IntPtr.Zero)
                return null;
            int size = checked((int)GetBufferSize(blob));
            return size == 0 ? null : Marshal.PtrToStringAnsi(GetBufferPointer(blob), size).TrimEnd('\0');
        }

        [DllImport(CompilerLibrary, CallingConvention = CallingConvention.StdCall)]
        private static extern int D3DCompile(
            IntPtr sourceData, nuint sourceDataSize, IntPtr sourceName,
            IntPtr defines, IntPtr include,
            IntPtr entryPoint, IntPtr target, uint flags1, uint flags2,
            out IntPtr code, out IntPtr errorMessages);
    }
}
