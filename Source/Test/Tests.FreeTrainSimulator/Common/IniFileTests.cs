// COPYRIGHT 2026 by the Open Rails Linux Fork project.
//
// This file is part of Open Rails.
//
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.IO;
using System.Text;

using FreeTrainSimulator.Common.Native;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Common
{
    /// <summary>
    /// Covers the initialization file reader that stands in for GetPrivateProfileString, which
    /// MSTS content and the train control system scripts rely on.
    /// </summary>
    [TestClass]
    public class IniFileTests
    {
        private string file;

        [TestInitialize]
        public void WriteFile()
        {
            file = Path.Combine(Path.GetTempPath(), $"fts-ini-{Guid.NewGuid():N}.ini");
            File.WriteAllText(file, """
                ; a comment
                [General]
                sources=1024
                Name = "Marias Pass"
                Empty=
                NoValue

                [Sounds]
                Volume=80
                """);
        }

        [TestCleanup]
        public void RemoveFile()
        {
            if (file != null && File.Exists(file))
                File.Delete(file);
        }

        [TestMethod]
        public void ReadsAValue()
        {
            Assert.AreEqual("1024", IniFile.GetValue(file, "General", "sources", null));
        }

        [TestMethod]
        public void IgnoresCaseInSectionsAndKeys()
        {
            Assert.AreEqual("1024", IniFile.GetValue(file, "GENERAL", "SOURCES", null));
            Assert.AreEqual("80", IniFile.GetValue(file, "sounds", "volume", null));
        }

        [TestMethod]
        public void StripsSurroundingQuotesAndPadding()
        {
            Assert.AreEqual("Marias Pass", IniFile.GetValue(file, "General", "Name", null));
        }

        [TestMethod]
        public void ReadsAKeyWithNoValue()
        {
            Assert.AreEqual(string.Empty, IniFile.GetValue(file, "General", "Empty", "fallback"));
            Assert.AreEqual(string.Empty, IniFile.GetValue(file, "General", "NoValue", "fallback"));
        }

        [TestMethod]
        public void ReturnsTheDefaultForWhatIsMissing()
        {
            Assert.AreEqual("fallback", IniFile.GetValue(file, "General", "absent", "fallback"));
            Assert.AreEqual("fallback", IniFile.GetValue(file, "Absent", "sources", "fallback"));
            Assert.AreEqual("fallback", IniFile.GetValue(Path.Combine(Path.GetTempPath(), "no-such.ini"), "General", "sources", "fallback"));
        }

        [TestMethod]
        public void FillsABufferLikeWin32()
        {
            StringBuilder buffer = new StringBuilder(255);
            int written = IniFile.GetString("General", "sources", string.Empty, buffer, 255, file);

            Assert.AreEqual(4, written);
            Assert.AreEqual("1024", buffer.ToString());
        }

        [TestMethod]
        public void TruncatesToTheBuffer()
        {
            StringBuilder buffer = new StringBuilder(4);
            int written = IniFile.GetString("General", "Name", string.Empty, buffer, 4, file);

            // Win32 leaves room for the terminating null, so three characters fit in four.
            Assert.AreEqual(3, written);
            Assert.AreEqual("Mar", buffer.ToString());
        }

        [TestMethod]
        public void ListsKeyNamesWhenTheKeyIsNull()
        {
            StringBuilder buffer = new StringBuilder(255);
            _ = IniFile.GetString("General", null, null, buffer, 255, file);

            string[] names = buffer.ToString().Split('\0', StringSplitOptions.RemoveEmptyEntries);
            CollectionAssert.AreEquivalent(new[] { "sources", "Name", "Empty", "NoValue" }, names);
        }

        [TestMethod]
        public void ListsSectionNamesWhenTheSectionIsNull()
        {
            StringBuilder buffer = new StringBuilder(255);
            _ = IniFile.GetString(null, null, null, buffer, 255, file);

            string[] names = buffer.ToString().Split('\0', StringSplitOptions.RemoveEmptyEntries);
            CollectionAssert.AreEquivalent(new[] { "General", "Sounds" }, names);
        }

        [TestMethod]
        public void WritesAValueBackAndKeepsTheRest()
        {
            Assert.IsTrue(IniFile.WriteValue(file, "General", "sources", "2048"));

            Assert.AreEqual("2048", IniFile.GetValue(file, "General", "sources", null));
            Assert.AreEqual("80", IniFile.GetValue(file, "Sounds", "Volume", null));
        }

        [TestMethod]
        public void CreatesAMissingFile()
        {
            string fresh = Path.Combine(Path.GetTempPath(), $"fts-ini-{Guid.NewGuid():N}.ini");
            try
            {
                Assert.IsTrue(IniFile.WriteValue(fresh, "General", "sources", "1024"));
                Assert.AreEqual("1024", IniFile.GetValue(fresh, "General", "sources", null));
            }
            finally
            {
                if (File.Exists(fresh))
                    File.Delete(fresh);
            }
        }
    }
}
