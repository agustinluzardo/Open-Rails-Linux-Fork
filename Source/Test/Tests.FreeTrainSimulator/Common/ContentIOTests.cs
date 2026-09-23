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
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using FreeTrainSimulator.Common.Native;

using Orts.Formats.Msts;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Common
{
    /// <summary>
    /// Covers the path resolution that lets MSTS content load on a case sensitive file system.
    /// </summary>
    /// <remarks>
    /// The fixture writes a miniature route whose folders and files are cased the way MSTS
    /// content is, then asks for them the way MSTS content asks - wrong case, backslashes, and
    /// relative hops out of a route folder.
    /// </remarks>
    [TestClass]
    public class ContentIOTests
    {
        private static string root;

        [ClassInitialize]
        public static void CreateContent(TestContext context)
        {
            root = Path.Combine(Path.GetTempPath(), $"riel-contentio-{Guid.NewGuid():N}");

            Directory.CreateDirectory(Path.Combine(root, "ROUTES", "Marias Pass", "TEXTURES"));
            Directory.CreateDirectory(Path.Combine(root, "Global", "Shapes"));

            File.WriteAllText(Path.Combine(root, "ROUTES", "Marias Pass", "MariasPass.trk"), "route");
            File.WriteAllText(Path.Combine(root, "ROUTES", "Marias Pass", "TEXTURES", "Rail.ace"), "texture");
            File.WriteAllText(Path.Combine(root, "Global", "Shapes", "Track1.s"), "shape");

            // An install off a Windows disk: folders and extensions shouting, which is what the
            // engine's own lower case search patterns have to find.
            Directory.CreateDirectory(Path.Combine(root, "TRAINS", "CONSISTS"));
            Directory.CreateDirectory(Path.Combine(root, "TRAINS", "TRAINSET", "BNSF"));
            File.WriteAllText(Path.Combine(root, "TRAINS", "CONSISTS", "COAL.CON"), "consist");
            File.WriteAllText(Path.Combine(root, "TRAINS", "CONSISTS", "Grain.con"), "consist");
            File.WriteAllText(Path.Combine(root, "TRAINS", "TRAINSET", "BNSF", "DASH9.ENG"), "engine");
        }

        [ClassCleanup]
        public static void RemoveContent()
        {
            if (root != null && Directory.Exists(root))
                Directory.Delete(root, true);
        }

        [TestMethod]
        public void ResolvesAnExactPath()
        {
            string path = Path.Combine(root, "Global", "Shapes", "Track1.s");
            Assert.AreEqual(path, ContentIO.ResolveFile(path));
        }

        [TestMethod]
        public void ResolvesTheWrongCase()
        {
            string requested = Path.Combine(root, "GLOBAL", "SHAPES", "TRACK1.S");
            string resolved = ContentIO.ResolveFile(requested);

            Assert.IsNotNull(resolved, "a differently cased path should still find the file");
            Assert.AreEqual("Track1.s", Path.GetFileName(resolved));
            Assert.IsTrue(File.Exists(resolved));
        }

        [TestMethod]
        public void ResolvesWindowsSeparators()
        {
            // Exactly what a .w file writes when it names a shape.
            string requested = $@"{root}\GLOBAL\SHAPES\track1.s";
            Assert.IsTrue(ContentIO.FileExists(requested));
        }

        [TestMethod]
        public void ResolvesARelativeReferenceOutOfARouteFolder()
        {
            string requested = Path.Combine(root, "routes", "marias pass", "..", "..", "GLOBAL", "shapes", "track1.s");
            Assert.IsTrue(ContentIO.FileExists(requested));
        }

        [TestMethod]
        public void ResolvesADirectory()
        {
            Assert.IsTrue(ContentIO.DirectoryExists(Path.Combine(root, "routes", "MARIAS PASS", "textures")));
        }

        [TestMethod]
        public void ReportsAMissingFile()
        {
            Assert.IsNull(ContentIO.ResolveFile(Path.Combine(root, "GLOBAL", "SHAPES", "nosuchshape.s")));
            Assert.IsFalse(ContentIO.FileExists(Path.Combine(root, "no", "such", "path.dat")));
        }

        [TestMethod]
        public void DoesNotMistakeAFileForADirectory()
        {
            string file = Path.Combine(root, "global", "shapes", "track1.s");
            Assert.IsTrue(ContentIO.FileExists(file));
            Assert.IsFalse(ContentIO.DirectoryExists(file));
        }

        [TestMethod]
        public void OpensAWronglyCasedFile()
        {
            using StreamReader reader = ContentIO.OpenText(Path.Combine(root, "ROUTES", "MARIAS PASS", "mariaspass.trk"));
            Assert.AreEqual("route", reader.ReadToEnd());
        }

        [TestMethod]
        public void PicksUpAFileAddedAfterTheDirectoryWasIndexed()
        {
            string directory = Path.Combine(root, "Global", "Shapes");
            Assert.IsFalse(ContentIO.FileExists(Path.Combine(directory, "LATER.S")));

            File.WriteAllText(Path.Combine(directory, "Later.s"), "shape");
            try
            {
                // The cached listing is revalidated against the directory's timestamp, so a file
                // that appears while the game is running is still found.
                Assert.IsTrue(ContentIO.FileExists(Path.Combine(directory, "LATER.S")));
            }
            finally
            {
                File.Delete(Path.Combine(directory, "Later.s"));
            }
        }

        [TestMethod]
        public void NormalizesSeparators()
        {
            string normalized = ContentIO.Normalize(@"a\b\c");
            Assert.AreEqual($"a{Path.DirectorySeparatorChar}b{Path.DirectorySeparatorChar}c", normalized);
        }

        [TestMethod]
        public void FallsBackToTheRequestedPathWhenNothingExists()
        {
            string missing = Path.Combine(root, "nothing", "here.dat");
            Assert.AreEqual(ContentIO.Normalize(missing), ContentIO.ResolveOrOriginal(missing));
        }
        // ------------------------------------------------------------------------- enumeration

        /// <summary>
        /// The case that crashed a scan: the folder is asked for as Trains/Consists and stored as
        /// TRAINS/CONSISTS, and the guard that said it existed resolved the case while the
        /// enumeration that followed did not.
        /// </summary>
        [TestMethod]
        public void EnumeratesAFolderWhoseCaseDiffers()
        {
            string requested = Path.Combine(root, "Trains", "Consists");
            Assert.AreEqual(2, ContentIO.EnumerateFiles(requested, "*.con").Count());
        }

        /// <summary>
        /// Resolving the folder is only half of it: the pattern has to ignore case too, or the
        /// folder is found and reported empty.
        /// </summary>
        [TestMethod]
        public void FindFileFromFoldersReturnsTheCorrectlyCasedPath()
        {
            string soundFolder = Path.Combine(root, "SOUND");
            Directory.CreateDirectory(soundFolder);
            string soundFile = Path.Combine(soundFolder, "e_wind_3.wav");
            File.WriteAllText(soundFile, "sound");

            try
            {
                string resolved = FolderStructure.FindFileFromFolders(
                    ImmutableArray.Create(Path.Combine(root, "Sound")),
                    "e_wind_3.wav");

                Assert.AreEqual(soundFile, resolved);
            }
            finally
            {
                File.Delete(soundFile);
                Directory.Delete(soundFolder);
            }
        }

        [TestMethod]
        public void MatchesThePatternWithoutRegardToCase()
        {
            string consists = Path.Combine(root, "TRAINS", "CONSISTS");
            CollectionAssert.AreEquivalent(
                new[] { "COAL.CON", "Grain.con" },
                ContentIO.EnumerateFiles(consists, "*.con").Select(Path.GetFileName).ToArray());
            Assert.AreEqual(2, ContentIO.EnumerateFiles(consists, "*.CON").Count());
        }

        /// <summary>
        /// An installation without timetables has no timetable folder, and one route in twenty
        /// has no PATHS. That used to end the scan of everything else.
        /// </summary>
        [TestMethod]
        public void MissingFolderEnumeratesEmptyRatherThanThrowing()
        {
            Assert.AreEqual(0, ContentIO.EnumerateFiles(Path.Combine(root, "PATHS"), "*.pat").Count());
            Assert.AreEqual(0, ContentIO.EnumerateDirectories(Path.Combine(root, "not here")).Count());
        }

        [TestMethod]
        public void EnumeratesSubfoldersWhoseCaseDiffers()
        {
            string requested = Path.Combine(root, "trains", "trainset");
            CollectionAssert.AreEquivalent(
                new[] { "BNSF" },
                ContentIO.EnumerateDirectories(requested).Select(Path.GetFileName).ToArray());
        }

        /// <summary>Rolling stock sits one level below TRAINSET, in a folder per manufacturer.</summary>
        [TestMethod]
        public void DescendsAsFarAsItIsAsked()
        {
            string trainset = Path.Combine(root, "Trains", "Trainset");
            Assert.AreEqual(0, ContentIO.EnumerateFiles(trainset, "*.eng").Count());
            Assert.AreEqual(1, ContentIO.EnumerateFiles(trainset, "*.eng", depth: 1).Count());
        }

    }
}
