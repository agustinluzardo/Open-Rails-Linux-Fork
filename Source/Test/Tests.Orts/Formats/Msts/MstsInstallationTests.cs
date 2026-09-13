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

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Orts.Formats.Msts;

namespace Tests.Orts.Formats.Msts
{
    /// <summary>
    /// Where the search looks depends on the machine, so what is checked here is what makes a
    /// folder an installation and how the override behaves.
    /// </summary>
    [TestClass]
    public class MstsInstallationTests
    {
        private string root;

        [TestInitialize]
        public void Setup()
        {
            root = Path.Combine(Path.GetTempPath(), $"riel-msts-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        private string Install(string relative, string routes = "ROUTES", string global = "GLOBAL")
        {
            string path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.Combine(path, routes));
            Directory.CreateDirectory(Path.Combine(path, global));
            return path;
        }

        [TestMethod]
        public void RoutesAndGlobalMakeAnInstallation()
        {
            string path = Install("MSTS");
            Assert.IsNotNull(MstsInstallation.Validate(path));
        }

        [TestMethod]
        public void RoutesAloneIsNotAnInstallation()
        {
            string path = Path.Combine(root, "MSTS");
            Directory.CreateDirectory(Path.Combine(path, "ROUTES"));
            Assert.IsNull(MstsInstallation.Validate(path));
        }

        [TestMethod]
        public void MissingFolderIsNotAnInstallation()
        {
            Assert.IsNull(MstsInstallation.Validate(Path.Combine(root, "nothing here")));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        public void NothingIsNotAnInstallation(string path)
        {
            Assert.IsNull(MstsInstallation.Validate(path));
        }

        /// <summary>
        /// Content copied off a Windows machine keeps whatever case it was written with, which is
        /// the whole reason the resolver exists; the installation check has to survive it too.
        /// </summary>
        [TestMethod]
        public void CaseOfRoutesAndGlobalDoesNotMatter()
        {
            string path = Install("MSTS", routes: "Routes", global: "global");
            Assert.IsNotNull(MstsInstallation.Validate(path));
        }

        [TestMethod]
        public void OverrideWins()
        {
            string path = Install("somewhere the search would never look");
            string previous = Environment.GetEnvironmentVariable(MstsInstallation.OverrideVariable);
            try
            {
                Environment.SetEnvironmentVariable(MstsInstallation.OverrideVariable, path);
                Assert.AreEqual(path, MstsInstallation.Locate(@"C:\default"));
            }
            finally
            {
                Environment.SetEnvironmentVariable(MstsInstallation.OverrideVariable, previous);
            }
        }

        [TestMethod]
        public void OverridePointingNowhereFallsBackToTheSearch()
        {
            string previous = Environment.GetEnvironmentVariable(MstsInstallation.OverrideVariable);
            try
            {
                Environment.SetEnvironmentVariable(MstsInstallation.OverrideVariable, Path.Combine(root, "gone"));
                // Whatever the machine has, the missing override must not become the answer.
                Assert.AreNotEqual(Path.Combine(root, "gone"), MstsInstallation.Locate("fallback"));
            }
            finally
            {
                Environment.SetEnvironmentVariable(MstsInstallation.OverrideVariable, previous);
            }
        }
    }
}
