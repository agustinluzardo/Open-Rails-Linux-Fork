using System;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Viewer3D;
using Orts.ActivityRunner.Viewer3D.Common;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

namespace Tests.Riel.Runtime
{
    [TestClass]
    public class SolarPositionTests
    {
        [TestMethod]
        [DataRow(-34.58, -58.41, 355)]
        [DataRow(-34.58, -58.41, 172)]
        [DataRow(0.0, 0.0, 82)]
        [DataRow(89.0, 0.0, 172)]
        [DataRow(-89.0, 0.0, 355)]
        public void MissingSunProducesFiniteUnitDirectionsThroughoutDay(double latitude, double longitude, int ordinalDate)
        {
            SkyDate date = new SkyDate(ordinalDate);
            for (int step = 0; step < 72; step++)
                AssertDirection(SunMoonPos.SolarAngle(ToRadians(latitude), ToRadians(longitude), null, step / 72f, date));
        }

        [TestMethod]
        public void UnrecognizedEnvironmentSunUsesSameFallbackAsUnsetTimes()
        {
            // Some legacy ENV files leave the satellite light/type block empty.
            // They parse as Unknown, so EnvironmentFile.Sun legitimately returns null.
            SkySatellite unknown = ReadSun("world_sky_satellite_light ( )");
            SkySatellite unset = ReadSun("world_sky_satellite_light ( 1 )");
            Assert.IsNull(unknown);
            Assert.IsNotNull(unset);
            SkyDate date = new SkyDate(355);
            for (int step = 0; step < 72; step++)
            {
                Vector3 expected = SunMoonPos.SolarAngle(ToRadians(-34.58), ToRadians(-58.41), unset, step / 72f, date);
                Vector3 actual = SunMoonPos.SolarAngle(ToRadians(-34.58), ToRadians(-58.41), unknown, step / 72f, date);
                AssertDirection(actual);
                Assert.AreEqual(expected, actual);
            }
        }

        [TestMethod]
        public void ConfiguredEnvironmentRiseAndSetTimesStillReachHorizon()
        {
            SkySatellite sun = ReadSun("""
                world_sky_satellite_light ( 1 )
                world_sky_satellite_rise_time ( 06:00:00 )
                world_sky_satellite_set_time ( 18:00:00 )
                """);
            SkyDate date = new SkyDate(82);
            Vector3 rise = SunMoonPos.SolarAngle(ToRadians(-34.58), ToRadians(-58.41), sun, 0.25f, date);
            Vector3 noon = SunMoonPos.SolarAngle(ToRadians(-34.58), ToRadians(-58.41), sun, 0.5f, date);
            Vector3 set = SunMoonPos.SolarAngle(ToRadians(-34.58), ToRadians(-58.41), sun, 0.75f, date);
            AssertDirection(rise);
            AssertDirection(noon);
            AssertDirection(set);
            Assert.AreEqual(0f, rise.Y, 0.00001f);
            Assert.AreEqual(0f, set.Y, 0.00001f);
            Assert.IsTrue(noon.Y > 0.8f);
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180;

        private static void AssertDirection(Vector3 direction)
        {
            Assert.IsTrue(float.IsFinite(direction.X) && float.IsFinite(direction.Y) && float.IsFinite(direction.Z));
            Assert.AreEqual(1f, direction.Length(), 0.00001f);
        }

        private static SkySatellite ReadSun(string satellite)
        {
            string file = Path.GetTempFileName();
            try
            {
                File.WriteAllText(file, $$"""
                    SIMISA@@@@@@@@@@JINX0E0t______
                    world (
                        world_sky (
                            world_sky_satellites ( 1
                                world_sky_satellite ( {{satellite}} )
                            )
                        )
                    )
                    """);
                return new EnvironmentFile(file).Sun;
            }
            finally
            {
                File.Delete(file);
            }
        }
    }
}
