// COPYRIGHT 2026 by the Riel project.
// This file is part of Riel, a fork of Open Rails, under the GPL version 3 or later.

using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Riel.Launcher;

namespace Tests.Riel.Launcher
{
    [TestClass]
    public class DiagnosticsTests
    {
        [TestMethod]
        public void AFileThatIsNotANativeLibraryCannotPassTheLibraryCheck()
        {
            string library = Path.GetTempFileName();
            try
            {
                File.WriteAllText(library, "This is a damaged native library.");
                (bool healthy, string detail) = Diagnostics.CheckLibrary(new[] { library }, "the game window cannot open");

                Assert.IsFalse(healthy);
                StringAssert.Contains(detail, "could not be loaded");
                StringAssert.Contains(detail, "the game window cannot open");
            }
            finally
            {
                File.Delete(library);
            }
        }
    }
}
