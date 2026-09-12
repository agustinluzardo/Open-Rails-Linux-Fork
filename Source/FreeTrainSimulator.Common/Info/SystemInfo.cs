using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FreeTrainSimulator.Common.Info
{
    /// <summary>
    /// Hardware and environment details written to the log, plus the shell helpers used to open
    /// files, folders and links. The inventory itself is platform specific and lives in
    /// SystemInfo.Windows.cs and SystemInfo.Unix.cs.
    /// </summary>
    public static partial class SystemInfo
    {
        public static void WriteSystemDetails()
        {
            StringBuilder builder = new StringBuilder();
            try
            {
                WriteEnvironment(builder);
            }
            catch (Exception ex) when (ex is TypeInitializationException || ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
            {
                builder.Append("Hardware information not available on this platform.");
            }
            Trace.Write(builder.ToString());
        }

        public static void OpenFolder(string path)
        {
            if (Directory.Exists(path))
                OpenShellTarget(path, "explore");
        }

        public static void OpenApplication(string path)
        {
            if (File.Exists(path))
                _ = Process.Start(path);
        }

        public static void OpenFile(string fileName)
        {
            OpenShellTarget(fileName, "open");
        }

        /// <summary>
        /// Hands a file, folder or URL to the desktop environment.
        /// </summary>
        /// <remarks>
        /// Windows takes a shell verb; the free desktops have no such concept and instead expect
        /// the target as an argument to xdg-open, and macOS to open. Passing the target as the
        /// process to execute - as this did before - asks the kernel to run the document itself,
        /// which fails for everything that is not an executable.
        /// </remarks>
        private static void OpenShellTarget(string target, string verb)
        {
            if (string.IsNullOrEmpty(target))
                return;

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true, Verb = verb });
                else
                    Process.Start(new ProcessStartInfo(RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "open" : "xdg-open")
                    {
                        ArgumentList = { target },
                        UseShellExecute = false,
                    });
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
            {
                // A desktop without xdg-utils installed is not a reason to take the game down.
                Trace.WriteLine($"Unable to open '{target}': {ex.Message}");
            }
        }

#pragma warning disable CA1054 // URI-like parameters should not be strings
        public static void OpenBrowser(string url)
#pragma warning restore CA1054 // URI-like parameters should not be strings
        {
            OpenFile(url);
        }
    }
}
