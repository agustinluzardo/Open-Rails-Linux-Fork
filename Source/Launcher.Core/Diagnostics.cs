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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Native;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;

namespace Riel.Launcher
{
    /// <summary>How a check came out.</summary>
    public enum CheckState
    {
        /// <summary>In place.</summary>
        Ok,
        /// <summary>Missing or broken, and the simulator needs it.</summary>
        Failed,
        /// <summary>Not present, and not needed: optional hardware, for instance.</summary>
        Absent,
    }

    /// <summary>One line of the diagnosis.</summary>
    public sealed record CheckResult(string Name, CheckState State, string Detail);

    /// <summary>
    /// Checks the things that stop the simulator starting, so a first run that fails says why.
    /// </summary>
    public static class Diagnostics
    {
        public static async Task<IReadOnlyList<CheckResult>> Run(CancellationToken cancellationToken)
        {
            List<CheckResult> results = new List<CheckResult>
            {
                Required("simulator", LocateSimulator()),
                Required("shaders", CheckShaders()),
                Required("OpenAL", CheckLibrary(new[] { "libopenal.so", "libopenal.so.1" }, "sound will be silent; install openal")),
                Required("SDL", CheckLibrary(new[] { "libSDL2-2.0.so.0", "libSDL2.so.0" }, "the game window cannot open; install sdl2")),
                Required("display", CheckDisplay()),
                Required("content", await CheckContent(cancellationToken).ConfigureAwait(false)),
            };

            // Optional hardware, so never a failure: most people drive with the keyboard. It is
            // listed so someone who does own the desk can see whether it was found.
            (bool desk, string deskDetail) = CheckRailDriver();
            results.Add(new CheckResult("RailDriver", desk ? CheckState.Ok : CheckState.Absent, deskDetail));

            return results;
        }

        /// <summary>Whether every check that matters passed.</summary>
        public static bool Healthy(IEnumerable<CheckResult> results) => results.All(result => result.State != CheckState.Failed);

        private static CheckResult Required(string name, (bool Ok, string Detail) result)
        {
            return new CheckResult(name, result.Ok ? CheckState.Ok : CheckState.Failed, result.Detail);
        }

        private static (bool, string) LocateSimulator()
        {
            string beside = Path.Combine(AppContext.BaseDirectory, "ActivityRunner");
            string configured = Environment.GetEnvironmentVariable("RIEL_SIMULATOR");

            if (!string.IsNullOrEmpty(configured))
                return (File.Exists(configured), $"{configured} (from RIEL_SIMULATOR)");
            return File.Exists(beside)
                ? (true, beside)
                : (false, $"not found at {beside}");
        }

        private static (bool, string) CheckShaders()
        {
            string content = Path.Combine(AppContext.BaseDirectory, "Content");
            string[] shaders = Directory.Exists(content) ? Directory.GetFiles(content, "*.mgfx") : Array.Empty<string>();

            // Twelve effects are compiled for the OpenGL profile; anything less means the
            // prebuilt set is incomplete and the simulator will fail as it loads a scene.
            const int expected = 12;
            if (shaders.Length >= expected)
                return (true, $"{shaders.Length} compiled effects in {content}");
            return (false, shaders.Length == 0
                ? $"none in {content}; run scripts/build-shaders.sh"
                : $"only {shaders.Length} of {expected} in {content}; run scripts/build-shaders.sh");
        }

        private static (bool, string) CheckLibrary(IEnumerable<string> candidates, string consequence)
        {
            foreach (string candidate in candidates)
            {
                if (NativeLibrary.TryLoad(candidate, out IntPtr handle))
                {
                    NativeLibrary.Free(handle);
                    return (true, candidate);
                }

                // The copy shipped with the game is not on the loader's search path: it sits beside
                // the executable once published and installed, and under runtimes/ in a build tree.
                foreach (string bundled in new[]
                {
                    Path.Combine(AppContext.BaseDirectory, candidate),
                    Path.Combine(AppContext.BaseDirectory, "runtimes", "linux-x64", "native", candidate),
                })
                {
                    if (File.Exists(bundled))
                        return (true, $"{bundled} (bundled)");
                }
            }
            return (false, $"not found - {consequence}");
        }

        private static (bool, string) CheckDisplay()
        {
            string wayland = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
            string x11 = Environment.GetEnvironmentVariable("DISPLAY");

            if (!string.IsNullOrEmpty(wayland))
                return (true, $"Wayland ({wayland})");
            if (!string.IsNullOrEmpty(x11))
                return (true, $"X11 ({x11})");
            return (false, "neither WAYLAND_DISPLAY nor DISPLAY is set; there is no desktop session to open a window on");
        }

        private static (bool, string) CheckRailDriver()
        {
            const string hidraw = "/sys/class/hidraw";
            if (!Directory.Exists(hidraw))
                return (false, "none (optional USB control desk; the keyboard works without it)");

            foreach (string entry in Directory.EnumerateDirectories(hidraw))
            {
                string uevent = Path.Combine(entry, "device", "uevent");
                if (!File.Exists(uevent))
                    continue;

                // 05F3 is PI Engineering, 00D2 the RailDriver desk.
                if (File.ReadAllLines(uevent).Any(line => line.Contains("000005F3:000000D2", StringComparison.OrdinalIgnoreCase)))
                {
                    string device = $"/dev/{Path.GetFileName(entry)}";
                    return CanRead(device)
                        ? (true, device)
                        : (false, $"{device} found but not readable; install the udev rule from packaging/linux/udev");
                }
            }
            return (false, "none (optional USB control desk; the keyboard works without it)");
        }

        private static bool CanRead(string device)
        {
            try
            {
                using FileStream stream = new FileStream(device, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static async Task<(bool, string)> CheckContent(CancellationToken cancellationToken)
        {
            try
            {
                ContentModel content = await ContentStore.Load(null, cancellationToken).ConfigureAwait(false);
                if (content.ContentFolders.Length == 0)
                {
                    string detected = MstsInstallationHint();
                    return (false, detected == null
                        ? "no folders configured; add one in the launcher or with 'riel content add'"
                        : $"no folders configured, but content looks present at {detected}");
                }

                int routes = 0;
                foreach (FolderModel folder in content.ContentFolders)
                    routes += (await folder.GetRoutes(cancellationToken).ConfigureAwait(false)).Length;

                return routes > 0
                    ? (true, $"{content.ContentFolders.Length} folder(s), {routes} route(s)")
                    : (false, $"{content.ContentFolders.Length} folder(s) but no routes; check the paths and run 'riel content refresh'");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
            {
                return (false, ex.Message);
            }
        }

        private static string MstsInstallationHint()
        {
            try
            {
                string folder = Orts.Formats.Msts.FolderStructure.MstsFolder;
                return ContentIO.DirectoryExists(folder) ? folder : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
