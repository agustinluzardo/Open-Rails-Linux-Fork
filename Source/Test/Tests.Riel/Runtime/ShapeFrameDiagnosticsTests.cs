using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

using Riel.Common;
using Riel.Common.Position;
using Riel.Models.Settings;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Processes;
using Orts.ActivityRunner.Viewer3D;
using Orts.ActivityRunner.Viewer3D.Shapes;

namespace Tests.Riel.Runtime
{
    [TestClass]
    [DoNotParallelize]
    public class ShapeFrameDiagnosticsTests
    {
        private Viewer previousShapeViewer;
        private Matrix previousSkyProjection;
        private Viewer viewer;
        private RenderFrame frame;
        private CaptureMaterial material;

        [TestInitialize]
        public void Initialize()
        {
            previousShapeViewer = (Viewer)typeof(SharedShape).GetField("viewer", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            previousSkyProjection = Camera.XNASkyProjection;
            ProfileUserSettingsModel settings = new ProfileUserSettingsModel
            {
                DynamicShadows = false,
                ViewingDistance = 1000,
                FarMountainsViewingDistance = 0,
                FieldOfView = 60,
                DetailLevelBias = 0,
                ExtendedDetailLevelView = false,
            };
            GameHost game = (GameHost)RuntimeHelpers.GetUninitializedObject(typeof(GameHost));
            GC.SuppressFinalize(game);
            RenderProcess process = (RenderProcess)RuntimeHelpers.GetUninitializedObject(typeof(RenderProcess));
            SetField(typeof(RenderProcess), process, "<DisplaySize>k__BackingField", new Point(800, 480));
            SetField(typeof(GameHost), game, "<RenderProcess>k__BackingField", process);
            SetField(typeof(GameHost), game, "<UserSettings>k__BackingField", settings);
            frame = new RenderFrame(game);

            viewer = (Viewer)RuntimeHelpers.GetUninitializedObject(typeof(Viewer));
            SetField(typeof(Viewer), viewer, "<UserSettings>k__BackingField", settings);
            SetField(typeof(Viewer), viewer, "<RenderProcess>k__BackingField", process);
            Camera camera = (Camera)RuntimeHelpers.GetUninitializedObject(typeof(FreeRoamCamera));
            SetField(typeof(Camera), camera, "viewer", viewer);
            SetField(typeof(Camera), camera, "cameraLocation", new WorldLocation(Tile.Zero, Vector3.Zero));
            SetField(typeof(Camera), camera, "NearPlane", 1f);
            camera.FieldOfView = settings.FieldOfView;
            viewer.Camera = camera;
            camera.ScreenChanged();
            camera.PrepareFrame(frame, ElapsedTime.Zero);
            SharedShape.Initialize(viewer);
            material = new CaptureMaterial();
        }

        [TestCleanup]
        public void Cleanup()
        {
            SharedShape.Initialize(previousShapeViewer);
            Camera.XNASkyProjection = previousSkyProjection;
        }

        [TestMethod]
        [DataRow("near", 0f, 50f, 0, 2)]
        [DataRow("lower-lod", 0f, 200f, 1, 1)]
        [DataRow("range", 0f, 700f, 1, 0)]
        [DataRow("fov", 1000f, 10f, 1, 0)]
        public void DiagnosticPassReportsRealCullingAndKeepsNormalSubmission(string reason, float x, float z, int expectedLevel, int expectedPrimitives)
        {
            SharedShape shape = CreateShape();
            WorldPosition position = new WorldPosition(Tile.Zero, Matrix.CreateTranslation(x, 0, -z));
            shape.PrepareFrame(frame, position, shape.Matrices, ShapeOptions.None);
            Draw();
            Matrix[] normalMatrices = material.Matrices.ToArray();
            Assert.AreEqual(expectedPrimitives, normalMatrices.Length);

            frame.Clear();
            material.Matrices.Clear();
            ShapeFrameResult result = shape.PrepareFrameWithDiagnostics(frame, position, shape.Matrices, ShapeOptions.None);
            Draw();

            CollectionAssert.AreEqual(normalMatrices, material.Matrices.ToArray(), "Tracing must keep the real render submissions unchanged.");
            Assert.AreEqual(expectedPrimitives, result.VisiblePrimitives);
            Assert.AreEqual(expectedPrimitives > 0, result.Visible);
            Assert.AreEqual(expectedLevel, result.Lods[0].SelectedLevel);
            Assert.AreEqual(reason != "fov", result.Lods[0].InFov);
            Assert.AreEqual(reason == "fov" ? (bool?)null : reason != "range", result.Lods[0].InRange);
            Assert.AreEqual(0, result.InvalidPrimitiveMatrices);
            Assert.AreEqual(0, result.NonFinitePrimitiveMatrices);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void DiagnosticPassReportsInvalidPrimitiveMatricesWithoutSuppressingThem(bool nonFinite)
        {
            SharedShape shape = CreateShape();
            Matrix animation = Matrix.Identity;
            animation.M11 = nonFinite ? float.NaN : 0;
            WorldPosition position = new WorldPosition(Tile.Zero, Matrix.CreateTranslation(0, 0, -50));

            ShapeFrameResult result = shape.PrepareFrameWithDiagnostics(frame, position, new[] { animation }, ShapeOptions.None);
            Draw();

            Assert.IsTrue(result.Visible, "Invalid matrices are observed, not replaced or removed by the diagnostic.");
            Assert.AreEqual(2, result.VisiblePrimitives);
            Assert.AreEqual(2, material.Matrices.Count);
            Assert.AreEqual(2, result.InvalidPrimitiveMatrices);
            Assert.AreEqual(nonFinite ? 2 : 0, result.NonFinitePrimitiveMatrices);
            Assert.AreEqual(!nonFinite, SharedShape.IsFinite(material.Matrices[0]));
        }

        [TestMethod]
        public void EmptyShapeReportsNoSubmittedGeometry()
        {
            SharedShape shape = CreateShape();
            shape.LodControls = Array.Empty<SharedShape.LodControl>();

            ShapeFrameResult result = shape.PrepareFrameWithDiagnostics(frame,
                new WorldPosition(Tile.Zero, Matrix.CreateTranslation(0, 0, -50)), shape.Matrices, ShapeOptions.None);

            Assert.IsFalse(result.Visible);
            Assert.AreEqual(0, result.VisiblePrimitives);
            Assert.AreEqual(0, result.Lods.Length);
        }

        private SharedShape CreateShape()
        {
            // All GPU allocation lives in the shape loader. This fixture supplies its
            // completed CPU-side data and exercises the production preparation pass.
            SharedShape shape = (SharedShape)RuntimeHelpers.GetUninitializedObject(typeof(SharedShape));
            shape.Matrices = new[] { Matrix.Identity };
            SharedShape.LodControl lod = (SharedShape.LodControl)RuntimeHelpers.GetUninitializedObject(typeof(SharedShape.LodControl));
            lod.DistanceLevels = new[] { CreateLevel(100, 2), CreateLevel(500, 1) };
            shape.LodControls = new[] { lod };
            return shape;
        }

        private SharedShape.DistanceLevel CreateLevel(float distance, int primitiveCount)
        {
            SharedShape.DistanceLevel level = (SharedShape.DistanceLevel)RuntimeHelpers.GetUninitializedObject(typeof(SharedShape.DistanceLevel));
            level.ViewingDistance = distance;
            level.ViewSphereRadius = 10;
            SharedShape.SubObject subObject = (SharedShape.SubObject)RuntimeHelpers.GetUninitializedObject(typeof(SharedShape.SubObject));
            subObject.ShapePrimitives = new ShapePrimitive[primitiveCount];
            for (int index = 0; index < primitiveCount; index++)
                subObject.ShapePrimitives[index] = new HeadlessPrimitive(material);
            level.SubObjects = new[] { subObject };
            return level;
        }

        private void Draw() => typeof(RenderFrame).GetMethod("DrawSequences", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(frame, new object[] { false });

        private static void SetField(Type type, object target, string name, object value) =>
            type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private sealed class HeadlessPrimitive : ShapePrimitive
        {
            internal HeadlessPrimitive(Material material)
            {
                Material = material;
                Hierarchy = new[] { -1 };
                HierarchyIndex = 0;
                PrimitiveCount = 1;
            }

            public override void Draw() { }
        }

        private sealed class CaptureMaterial : Material
        {
            internal List<Matrix> Matrices { get; } = new List<Matrix>();

            internal CaptureMaterial() : base(null, null) { }

            public override bool GetBlending() => false;

            public override void Render(List<RenderItem> items, ref Matrix view, ref Matrix projection, ref Matrix viewProjection)
            {
                foreach (RenderItem item in items)
                    Matrices.Add(item.XNAMatrix);
            }
        }
    }
}
