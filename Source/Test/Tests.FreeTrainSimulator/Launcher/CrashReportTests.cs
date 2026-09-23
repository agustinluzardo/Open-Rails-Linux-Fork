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

using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Riel.Launcher;

namespace Tests.FreeTrainSimulator.Launcher
{
    /// <summary>
    /// A native crash is only as useful as what the launcher reads out of .NET's crash report:
    /// the library it happened in and the managed code that called it.
    /// </summary>
    [TestClass]
    public class CrashReportTests
    {
        private static string Frame(bool managed, string module = null, string symbol = null, string method = null, string assembly = null, string offset = "0x10", string imageOffset = "0x1234")
        {
            List<string> fields = new List<string> { $"\"is_managed\": \"{(managed ? "true" : "false")}\"", $"\"native_offset\": \"{offset}\"", $"\"native_image_offset\": \"{imageOffset}\"" };
            if (module != null)
                fields.Add($"\"native_module\": \"{module}\"");
            if (symbol != null)
                fields.Add($"\"unmanaged_name\": \"{symbol}\"");
            if (method != null)
                fields.Add($"\"method_name\": \"{method}\"");
            if (assembly != null)
                fields.Add($"\"filename\": \"{assembly}\"");
            return "{" + string.Join(", ", fields) + "}";
        }

        private static string Report(params string[] crashedFrames)
        {
            return "{ \"payload\": { \"protocol_version\": \"1.0.0\", \"process_name\": \"ActivityRunner\", \"threads\": [" +
                "{ \"is_managed\": \"true\", \"crashed\": \"false\", \"stack_frames\": [" + Frame(false, "libc.so.6", "futex_wait") + "] }," +
                "{ \"is_managed\": \"true\", \"crashed\": \"true\", \"stack_frames\": [" + string.Join(",", crashedFrames) + "] }" +
                "] }, \"parameters\": { \"ExceptionType\": \"0x20000000\" } }";
        }

        /// <summary>A fault in a driver, reached from MonoGame: the case the report exists for.</summary>
        [TestMethod]
        public void AFaultNamesTheLibraryAndItsCaller()
        {
            string json = Report(
                Frame(false, "libnvidia-glcore.so.550.54.14", imageOffset: "0x9a1b2c"),
                Frame(false, "libnvidia-glcore.so.550.54.14", imageOffset: "0x9a0000"),
                Frame(true),
                Frame(true, method: "Microsoft.Xna.Framework.Graphics.Shader.GetShaderHandle()", assembly: "MonoGame.Framework.dll"),
                Frame(true, method: "Orts.ActivityRunner.Viewer3D.RenderFrame.Draw(Microsoft.Xna.Framework.GameTime)", assembly: "ActivityRunner.dll"));

            CrashReport report = CrashReport.Parse("report.json", json);

            Assert.IsNotNull(report);
            Assert.AreEqual("libnvidia-glcore.so.550.54.14", report.Module);
            Assert.AreEqual("Microsoft.Xna.Framework.Graphics.Shader.GetShaderHandle()", report.Caller);
            CollectionAssert.AreEqual(new[]
            {
                "libnvidia-glcore.so.550.54.14+0x9a1b2c",
                "libnvidia-glcore.so.550.54.14+0x9a0000",
                "Microsoft.Xna.Framework.Graphics.Shader.GetShaderHandle() [MonoGame.Framework.dll]",
                "Orts.ActivityRunner.Viewer3D.RenderFrame.Draw(Microsoft.Xna.Framework.GameTime) [ActivityRunner.dll]",
            }, new List<string>(report.Frames), "the unnamed stub frame is left out");
        }

        [TestMethod]
        public void AnExportedSymbolIsNamed()
        {
            CrashReport report = CrashReport.Parse("report.json", Report(Frame(false, "libc.so.6", "strlen", offset: "0x1c")));
            Assert.AreEqual("libc.so.6!strlen+0x1c", report.Frames[0]);
        }

        /// <summary>
        /// A thread stopped by a signal from outside reports from inside the runtime's handler;
        /// those frames say nothing about the crash and are skipped, trampoline included.
        /// </summary>
        [TestMethod]
        public void TheDumpHandlerFramesAreSkipped()
        {
            string json = Report(
                Frame(false, "libc.so.6", "wait4"),
                Frame(false, "libcoreclr.so"),
                Frame(false, "libcoreclr.so"),
                Frame(false, "libc.so.6", imageOffset: "0x45330"),
                Frame(false, "libopenal.so", "alcOpenDevice"),
                Frame(true, method: "Orts.ActivityRunner.Viewer3D.OpenAL.Initialize()", assembly: "ActivityRunner.dll"));

            CrashReport report = CrashReport.Parse("report.json", json);

            Assert.AreEqual("libopenal.so", report.Module);
            Assert.AreEqual("Orts.ActivityRunner.Viewer3D.OpenAL.Initialize()", report.Caller);
            Assert.AreEqual(2, report.Frames.Count);
        }

        /// <summary>An unhandled exception aborts from inside the runtime, with no trampoline.</summary>
        [TestMethod]
        public void AnAbortOverAnExceptionStartsInManagedCode()
        {
            string json = Report(
                Frame(false, "libc.so.6", "wait4"),
                Frame(false, "libcoreclr.so"),
                Frame(true, method: "System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()", assembly: "System.Private.CoreLib.dll"));

            CrashReport report = CrashReport.Parse("report.json", json);

            Assert.IsNull(report.Module, "the crashing thread was in managed code");
            Assert.AreEqual("System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()", report.Caller);
        }

        [TestMethod]
        public void NoCrashedThreadMeansNoReport()
        {
            string json = "{ \"payload\": { \"threads\": [ { \"crashed\": \"false\", \"stack_frames\": [] } ] } }";
            Assert.IsNull(CrashReport.Parse("report.json", json));
        }

        [TestMethod]
        public void AHalfWrittenReportIsIgnored()
        {
            Assert.IsNull(CrashReport.Parse("report.json", "{ \"payload\": { \"threads\": [ { \"crashed\": \"tr"));
            Assert.IsNull(CrashReport.Parse("report.json", "[]"));
        }

        [TestMethod]
        public void TheRuntimeIsAskedForAReportOnly()
        {
            Dictionary<string, string> environment = new Dictionary<string, string>();
            CrashReport.Request(environment);

            Assert.AreEqual("1", environment["DOTNET_DbgEnableMiniDump"]);
            Assert.AreEqual("1", environment["DOTNET_EnableCrashReportOnly"], "a report, never a dump the size of the process");
            StringAssert.StartsWith(environment["DOTNET_DbgMiniDumpName"], CrashReport.Folder);
            StringAssert.EndsWith(environment["DOTNET_DbgMiniDumpName"], "ActivityRunner.%p");
        }

        [TestMethod]
        public void ADumpSetupOfTheUsersOwnIsLeftAlone()
        {
            Dictionary<string, string> environment = new Dictionary<string, string> { ["DOTNET_DbgEnableMiniDump"] = "1", ["DOTNET_DbgMiniDumpType"] = "4" };
            CrashReport.Request(environment);

            Assert.IsFalse(environment.ContainsKey("DOTNET_EnableCrashReportOnly"));
            Assert.IsFalse(environment.ContainsKey("DOTNET_DbgMiniDumpName"));
        }
    }
}
