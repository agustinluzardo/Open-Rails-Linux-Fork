using System;
using System.Diagnostics;
using System.Reflection;

using Riel.Common;
using Riel.Common.Info;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using NuGet.Versioning;

namespace Tests.Riel.Common.Info
{
    [TestClass]
    public class VersionInfoTests
    {
        [TestMethod]
        public void VersionTest()
        {
            //VersionInfo.FullVersion: "1.3.2-alpha.4+LocalBuild"
            //VersionInfo.Version: "1.3.2-alpha.4"
            //VersionInfo.FileVersion: "1.3.2.0"
            //VersionInfo.Channel: "alpha"
            //VersionInfo.Build: "4"
            //VersionInfo.CodeVersion: "LocalBuild"

            Assert.AreEqual(FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(VersionInfo)).Location).ProductVersion, VersionInfo.FullVersion);

            Assert.IsGreaterThanOrEqualTo(5, VersionInfo.FullVersion.IndexOf('+', System.StringComparison.OrdinalIgnoreCase));    // there should be a + sign for product metadata
            Assert.IsFalse(string.IsNullOrEmpty(VersionInfo.CodeVersion));
        }

        [TestMethod()]
        public void CompareTest()
        {
            Assert.AreEqual(1, VersionInfo.Compare("0"));   //Passing invalid version, so the current version should in each case be ahead
            Assert.AreEqual(1, VersionInfo.Compare(MinVersion().ToNormalizedString()));
            Assert.AreEqual(0, VersionInfo.Compare(VersionInfo.CurrentVersion.ToNormalizedString()));
            Assert.AreEqual(-1, VersionInfo.Compare(NextVersion().ToNormalizedString()));
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD")))  // only if running in Azure Pipelines, the version is expected to be the same as the one of the assembly,                                                                                        // so we can check that as well
                return;
#pragma warning disable CS0436 // Type conflicts with imported type
            Assert.AreEqual(ThisAssembly.AssemblyInformationalVersion, VersionInfo.FullVersion);
#pragma warning restore CS0436 // Type conflicts with imported type
        }

        [TestMethod()]
        public void AvailableVersionCompareCurrentTest()
        {
            Assert.IsNull(VersionInfo.GetBestAvailableVersion(new[] { PackageVersion() }, UpdateMode.PreRelease));
        }

        [TestMethod()]
        public void AvailableVersionCompareNextTest()
        {
            NuGetVersion current = PackageVersion();
            NuGetVersion next = new NuGetVersion(current.Major, current.Minor, current.Patch + 1);
            NuGetVersion available = VersionInfo.GetBestAvailableVersion(new[] { next }, UpdateMode.PreRelease);
            Assert.IsNotNull(available);
            Assert.AreEqual(-1, VersionInfo.Compare(available.ToFullString()));
        }

        [TestMethod]
        public void AvailableVersionRejectsOlderTest()
        {
            Assert.IsNull(VersionInfo.GetBestAvailableVersion(new[] { new NuGetVersion(0, 0, 0) }, UpdateMode.PreRelease));
        }

        private static NuGetVersion PackageVersion()
        {
#pragma warning disable CS0436 // Type conflicts with imported type
            return NuGetVersion.Parse(ThisAssembly.NuGetPackageVersion);
#pragma warning restore CS0436 // Type conflicts with imported type
        }

        private static NuGetVersion MinVersion()
        {
            return new NuGetVersion(0, 0, 0);

        }

        private static NuGetVersion NextVersion()
        {
            NuGetVersion current = VersionInfo.CurrentVersion;
            return new NuGetVersion(current.Major, current.Minor, current.Patch + 1, current.Release);

        }
    }
}
