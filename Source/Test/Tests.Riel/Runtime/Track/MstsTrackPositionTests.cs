using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

using Riel.Common;
using Riel.Common.Position;
using Riel.Models.Imported.ImportHandler.TrainSimulator;
using Riel.Models.Track;
using Riel.Runtime.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Orts.Formats.Msts.Parsers;
using Orts.Simulation.RollingStocks;

using TrackVectorSection = Orts.Formats.Msts.Models.TrackVectorSection;

using Tests.Riel.Common;

namespace Tests.Riel.Runtime.Track
{
    [TestClass]
    [DoNotParallelize]
    public class MstsTrackPositionTests
    {
        private TrackWorld previousWorld;

        [TestInitialize]
        public void SaveTrackWorld() => previousWorld = TrackWorld.Instance;

        [TestCleanup]
        public void RestoreTrackWorld() => GameService<TrackWorld>.Set(null, previousWorld);

        [TestMethod]
        [DataRow(0.03f, 0f)]
        [DataRow(-0.03f, 0f)]
        [DataRow(0.03f, 30f)]
        [DataRow(-0.03f, -30f)]
        [DataRow(0f, 0f)]
        [DataRow(0f, 30f)]
        [DataRow(0f, -30f)]
        public void ImportedMstsEndpointPreservesPitchAndRoll(float pitch, float angle)
        {
            WorldLocation start = new WorldLocation(-12556, 14759, 900, 1200, 1000);
            Vector3 direction = new Vector3(pitch, 0.4f, 0.01f);
            TrackSection shape = Shape(angle);
            AssertLocation(ReferencePosition(start, direction, shape, shape.Length), ImportEndpoint(start, direction, shape));
        }

        [TestMethod]
        [DataRow(0.03f, 0f)]
        [DataRow(-0.03f, 0f)]
        [DataRow(0.03f, 30f)]
        [DataRow(-0.03f, -30f)]
        [DataRow(0f, 0f)]
        [DataRow(0f, 30f)]
        [DataRow(0f, -30f)]
        public void TravellerFollowsMstsThreeDimensionalGeometry(float pitch, float angle)
        {
            WorldLocation start = new WorldLocation(-12556, 14759, 900, 1200, 1000);
            Vector3 direction = new Vector3(pitch, 0.4f, 0.01f);
            TrackSection shape = Shape(angle);
            (_, VectorNode node) = World(shape, Section(start, direction, shape));
            TrackTraveller traveller = TrackTraveller.InitializeTraveller(node, 0, 0);
            for (int i = 0; i <= 10; i++)
            {
                float distance = shape.Length * i / 10;
                AssertLocation(ReferencePosition(start, direction, shape, distance), traveller.Move(distance).Location);
            }
        }

        [TestMethod]
        [DataRow(30f)]
        [DataRow(-30f)]
        public void GradedCurveCanBeFoundFromRealOrZeroHeightPathPoint(float angle)
        {
            WorldLocation start = new WorldLocation(-12556, 14759, 900, 1200, 1000);
            Vector3 direction = new Vector3(0.07f, 0.4f, 0.025f);
            TrackSection shape = Shape(angle);
            (TrackWorld world, VectorNode node) = World(shape, Section(start, direction, shape));
            float offset = shape.Length * 0.6f;
            WorldLocation point = ReferencePosition(start, direction, shape, offset);
            SectionGeometry geometry = world.SectionGeometry[node.VectorSections[0]];
            foreach (WorldLocation query in new[] { point, point.SetElevation(0) })
            {
                TrackTraveller? snapped = TrackTraveller.InitializeTraveller(query, node);
                Assert.IsTrue(snapped.HasValue, "A point on the placed MSTS curve must be found.");
                Assert.AreEqual(offset, snapped.Value.SectionOffset, 0.003);
                AssertLocation(point, snapped.Value.Location);
                AssertLocation(point, geometry.SnapToSection(query));
                Assert.AreEqual(offset, geometry.DistanceOnSection(query), 0.003);
            }
            TrackDistanceDiagnostic diagnostic = world.NearestTrackDistance(PointD.FromWorldLocation(point));
            Assert.IsNotNull(diagnostic);
            Assert.AreEqual(0, diagnostic.DistanceMeters, 0.003);
        }

        [TestMethod]
        [DataRow(0.03f, false)]
        [DataRow(-0.03f, false)]
        [DataRow(0.03f, true)]
        [DataRow(-0.03f, true)]
        public void RailcarRemainsOnGradeWhileWheelsCrossSectionBoundary(float pitch, bool reverse)
        {
            WorldLocation start = new WorldLocation(-12556, 14759, 900, 1200, 1000);
            Vector3 direction = new Vector3(pitch, 0.4f, 0);
            TrackSection shape = Shape(0);
            WorldLocation next = ReferencePosition(start, direction, shape, shape.Length);
            (_, VectorNode node) = World(shape, Section(start, direction, shape), Section(next, direction, shape));
            MSTSWagon car = (MSTSWagon)RuntimeHelpers.GetUninitializedObject(typeof(MSTSWagon));
            TrainCarPart body = new TrainCarPart(0, 0, false);
            TrainCarParts parts = new TrainCarParts();
            parts.Add(body);
            List<WheelAxle> wheels = new List<WheelAxle> { new WheelAxle(-9, 0, 0), new WheelAxle(9, 0, 0) };
            foreach (WheelAxle wheel in wheels)
                typeof(WheelAxle).GetProperty(nameof(WheelAxle.Part)).SetValue(wheel, body);
            SetCarField(car, "<CarLengthM>k__BackingField", 28f);
            SetCarField(car, "<Parts>k__BackingField", parts);
            SetCarField(car, "<WheelAxles>k__BackingField", wheels);
            for (int distance = 270; distance <= 305; distance++)
            {
                TrackTraveller traveller = TrackTraveller.InitializeTraveller(node, 0, 0).Move(distance);
                if (reverse)
                    traveller = traveller.Move(28).Reverse();
                // Skip suspension effects with their existing elapsed-time cutoff;
                // exercise the real wheel fitting and model placement directly.
                car.ComputePosition(ref traveller, !reverse, 1, 0, 0);
                WorldLocation expected = ReferencePosition(start, direction, shape, distance + 14);
                Assert.AreEqual(expected.Location.Y + 0.275f, car.WorldPosition.Location.Y, 0.003,
                    $"The car must stay on the rail at distance {distance}, including the section join.");
                Assert.AreEqual(Math.Abs(Math.Sin(pitch)), Math.Abs(car.WorldPosition.XNAMatrix.M32), 0.0001,
                    "The fitted car pitch must follow the grade without jumping at the join.");
            }
        }

        [TestMethod]
        [DataRow(0.03f, 0f)]
        [DataRow(-0.03f, 0f)]
        [DataRow(0.03f, 30f)]
        [DataRow(-0.03f, -30f)]
        public void CachedFlatEndpointDoesNotFlattenRuntimeTrack(float pitch, float angle)
        {
            WorldLocation start = new WorldLocation(-12556, 14759, 900, 1200, 1000);
            Vector3 direction = new Vector3(pitch, 0.4f, 0.01f);
            TrackSection shape = Shape(angle);
            // Old route models contain valid placement angles but flattened endpoints.
            WorldLocation staleEnd = ReferencePosition(start, new Vector3(0, direction.Y, 0), shape, shape.Length);
            VectorSectionNode section = new VectorSectionNode(start, start.Tile, direction, staleEnd) { NodeIndex = 1 };
            (TrackWorld world, VectorNode node) = World(shape, section);
            float offset = shape.Length * 0.6f;
            WorldLocation expected = ReferencePosition(start, direction, shape, offset);
            AssertLocation(expected, TrackTraveller.InitializeTraveller(node, 0, 0).Move(offset).Location);
            AssertLocation(ReferencePosition(start, direction, shape, shape.Length), world.SectionGeometry[section].EndLocation);
            foreach (WorldLocation query in new[] { expected, expected.SetElevation(0) })
            {
                TrackTraveller? snapped = TrackTraveller.InitializeTraveller(query, node);
                Assert.IsTrue(snapped.HasValue);
                Assert.AreEqual(offset, snapped.Value.SectionOffset, 0.003);
                AssertLocation(expected, snapped.Value.Location);
            }
        }

        private static TrackSection Shape(float angle) => new TrackSection
        {
            SectionIndex = 1, Gauge = 1.435f, Curved = angle != 0,
            Radius = angle == 0 ? 0 : 600, Angle = angle,
            Length = angle == 0 ? 300 : 600 * Math.Abs(MathHelper.ToRadians(angle)),
        };

        private static VectorSectionNode Section(WorldLocation start, Vector3 direction, TrackSection shape) =>
            new VectorSectionNode(start, start.Tile, direction, ImportEndpoint(start, direction, shape)) { NodeIndex = 1 };

        private static WorldLocation ImportEndpoint(WorldLocation start, Vector3 direction, TrackSection shape)
        {
            string path = Path.Combine(Path.GetTempPath(), "riel-graded-section-" + Guid.NewGuid().ToString("N") + ".tdb");
            try
            {
                File.WriteAllText(path, string.Create(CultureInfo.InvariantCulture,
                    $"SIMISA@@@@@@@@@@JINX0T0t______\n\nTrVectorSection ( 1 1 {start.TileX} {start.TileZ} 1 0 1 00 {start.TileX} {start.TileZ} {start.Location.X:R} {start.Location.Y:R} {start.Location.Z:R} {direction.X:R} {direction.Y:R} {direction.Z:R} )\n"));
                using STFReader reader = new STFReader(path, false);
                reader.MustMatch("TrVectorSection");
                reader.MustMatch("(");
                TrackVectorSection parsed = (TrackVectorSection)Activator.CreateInstance(typeof(TrackVectorSection),
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { reader }, null);
                reader.MustMatch(")");
                return (WorldLocation)typeof(TrackModelImportHandler).GetMethod("ComputeEndLocationFromDirection",
                    BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { parsed, shape });
            }
            finally { File.Delete(path); }
        }

        private static (TrackWorld world, VectorNode node) World(TrackSection shape, params VectorSectionNode[] sections)
        {
            VectorNode node = new VectorNode(sections[0].Location, sections[0].WorldTile, sections[^1].EndLocation)
                { NodeIndex = 1, VectorSections = sections.ToImmutableArray() };
            TrackDatabase database = new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, node),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), new TrackNodeConnectorIndex()),
            };
            TrackWorldTestFixture.InitializeTrackDatabase(database);
            return (TrackWorld.Initialize(null, new TrackModel { TrackDatabase = database },
                new TrackSectionModel { TrackSections = ImmutableDictionary<int, TrackSection>.Empty.Add(1, shape) }), node);
        }

        private static WorldLocation ReferencePosition(WorldLocation start, Vector3 direction, TrackSection shape, double distance)
        {
            // Independent reference: the displacement-matrix algorithm in
            // Open Rails Traveller.SetLocation, including all MSTS placement angles.
            Matrix displacement = Matrix.Identity;
            if (shape.Curved)
            {
                int sign = Math.Sign(shape.Angle);
                Vector3 center = Vector3.Right * shape.Radius * sign;
                displacement.Translation = -center;
                displacement *= Matrix.CreateRotationY((float)(distance / shape.Radius) * sign);
                displacement.Translation += center;
            }
            else { displacement.Translation = new Vector3(0, 0, (float)distance); }
            displacement *= Matrix.CreateFromYawPitchRoll(direction.Y, direction.X, direction.Z);
            return new WorldLocation(start.Tile, start.Location + displacement.Translation, true);
        }

        private static void AssertLocation(WorldLocation expected, WorldLocation actual) =>
            Assert.AreEqual(0, Math.Sqrt(WorldLocation.GetDistanceSquared(expected, actual)), 0.003,
                $"Expected {expected}; actual {actual}");

        private static void SetCarField(TrainCar car, string name, object value) =>
            typeof(TrainCar).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(car, value);
    }
}
