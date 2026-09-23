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
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Imported.ImportHandler;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Orts.Formats.Msts.Files;

namespace Tests.FreeTrainSimulator.Launcher
{
    /// <summary>
    /// One unreadable content file must cost that file only, and say so, rather than end the
    /// scan and leave the route list empty.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class ImportFailuresTests
    {
        [TestInitialize]
        public void Clear() => _ = ImportFailures.Take();

        /// <summary>
        /// A real malformed route file, read by the real parser: the shape of the failure that
        /// used to end the scan.
        /// </summary>
        [TestMethod]
        public async Task AMalformedRouteFileIsSkippedAndRecorded()
        {
            string trk = Path.Combine(Path.GetTempPath(), $"riel-broken-{Guid.NewGuid():N}.trk");
            File.WriteAllText(trk, "SIMISA@@@@@@@@@@JINX0r0t______\n\nTr_RouteFile (\n\tRouteID ( BROKEN )\n\tName ( \"Broken\" )\n)\n");
            try
            {
                object result = await ImportFailures.Guard(Task.Run(() => (object)new RouteFile(trk)), "route", trk);

                Assert.IsNull(result);
                var skipped = ImportFailures.Take();
                Assert.AreEqual(1, skipped.Count);
                Assert.AreEqual("route", skipped[0].Kind);
                Assert.AreEqual(trk, skipped[0].Path);
                // The parser's own words, which name the file and the line: exactly what is needed
                // to find the problem in a route someone else wrote.
                StringAssert.Contains(skipped[0].Reason, Path.GetFileName(trk));
                StringAssert.Contains(skipped[0].Reason, ":line ");
            }
            finally
            {
                File.Delete(trk);
            }
        }

        [TestMethod]
        public async Task AMissingFileIsSkipped()
        {
            Assert.IsNull(await ImportFailures.Guard(Task.FromException<string>(new FileNotFoundException("gone")), "consist", "a.con"));
            Assert.AreEqual(1, ImportFailures.Take().Count);
        }

        [TestMethod]
        public async Task AGoodFileComesThrough()
        {
            Assert.AreEqual("model", await ImportFailures.Guard(Task.FromResult("model"), "path", "a.pat"));
            Assert.AreEqual(0, ImportFailures.Take().Count);
        }

        /// <summary>Cancelling is the user stopping the scan, not a bad file.</summary>
        [TestMethod]
        public async Task CancellingStillCancels()
        {
            await Assert.ThrowsExactlyAsync<TaskCanceledException>(() =>
                ImportFailures.Guard(Task.FromCanceled<string>(new CancellationToken(true)), "route", "a.trk"));
            Assert.AreEqual(0, ImportFailures.Take().Count);
        }

        [TestMethod]
        public void TakingForgets()
        {
            ImportFailures.Record("activity", "a.act", new InvalidDataException("bad"));
            Assert.AreEqual(1, ImportFailures.Take().Count);
            Assert.AreEqual(0, ImportFailures.Take().Count);
        }
    }
}
