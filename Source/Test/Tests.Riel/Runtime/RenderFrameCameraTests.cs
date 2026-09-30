using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

using Riel.Models.Settings;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Processes;
using Orts.ActivityRunner.Viewer3D;

namespace Tests.Riel.Runtime
{
    [TestClass]
    [DoNotParallelize]
    public class RenderFrameCameraTests
    {
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void PreparedFrameKeepsCameraMatricesWhenNextUpdateMovesCamera(bool blended)
        {
            RenderFrame frame = CreateFrame();
            Camera camera = CreateCamera();
            Matrix view = camera.XnaView;
            Matrix projection = camera.XnaProjection;
            frame.SetCamera(camera);
            CaptureMaterial material = new CaptureMaterial(blended);
            Matrix world = Matrix.CreateTranslation(5, 0, 10);
            frame.AddPrimitive(material, new NoopPrimitive(), RenderPrimitiveGroup.World, ref world);

            // Render and Updater run concurrently. The next update may move the
            // same camera across a tile boundary after this frame was prepared.
            MoveCamera(camera);
            Draw(frame, "DrawSequences");

            Assert.AreEqual(view, material.View, "The prepared scene must use its own view matrix.");
            Assert.AreEqual(projection, material.Projection, "Projection must match the prepared scene.");
            Assert.AreEqual(view * projection, material.ViewProjection);
        }

        [TestMethod]
        public void NextFrameUsesNewCameraWithoutChangingPreviousFrame()
        {
            Camera camera = CreateCamera();
            RenderFrame previous = CreateFrame();
            RenderFrame next = CreateFrame();
            CaptureMaterial previousMaterial = new CaptureMaterial(false);
            CaptureMaterial nextMaterial = new CaptureMaterial(false);
            Matrix world = Matrix.Identity;
            Matrix previousView = camera.XnaView;
            previous.SetCamera(camera);
            previous.AddPrimitive(previousMaterial, new NoopPrimitive(), RenderPrimitiveGroup.World, ref world);

            MoveCamera(camera);
            Matrix nextView = camera.XnaView;
            next.SetCamera(camera);
            next.AddPrimitive(nextMaterial, new NoopPrimitive(), RenderPrimitiveGroup.World, ref world);
            Draw(previous, "DrawSequences");
            Draw(next, "DrawSequences");

            Assert.AreEqual(previousView, previousMaterial.View);
            Assert.AreEqual(nextView, nextMaterial.View);
            Assert.AreNotEqual(previousMaterial.View, nextMaterial.View);
        }

        [TestMethod]
        public void DistantMountainsKeepPreparedViewAndProjection()
        {
            Matrix originalMountainProjection = Camera.XnaDistantMountainProjection;
            try
            {
                Camera camera = CreateCamera();
                Matrix view = camera.XnaView;
                Matrix mountainProjection = Matrix.CreatePerspectiveFieldOfView(0.8f, 1.6f, 1, 20000);
                Camera.XnaDistantMountainProjection = mountainProjection;
                RenderFrame frame = CreateFrame(20000);
                frame.SetCamera(camera);
                CaptureMountainMaterial material = (CaptureMountainMaterial)RuntimeHelpers.GetUninitializedObject(typeof(CaptureMountainMaterial));
                Matrix world = Matrix.Identity;
                frame.AddPrimitive(material, new NoopPrimitive(), RenderPrimitiveGroup.Sky, ref world);

                MoveCamera(camera);
                Camera.XnaDistantMountainProjection = Matrix.CreatePerspectiveFieldOfView(0.5f, 1.2f, 2, 10000);
                Draw(frame, "DrawSequencesDistantMountains");

                Assert.AreEqual(view, material.View);
                Assert.AreEqual(mountainProjection, material.Projection);
                Assert.AreEqual(view * mountainProjection, material.ViewProjection);
            }
            finally
            {
                Camera.XnaDistantMountainProjection = originalMountainProjection;
            }
        }

        [TestMethod]
        public void FrameWithoutCameraRetainsLoadingScreenProjection()
        {
            RenderFrame frame = CreateFrame();
            CaptureMaterial material = new CaptureMaterial(false);
            Matrix world = Matrix.Identity;
            frame.AddPrimitive(material, new NoopPrimitive(), RenderPrimitiveGroup.Overlay, ref world);
            Draw(frame, "DrawSequences");

            Assert.AreEqual(Matrix.Identity, material.View);
            Assert.AreEqual(Matrix.CreateOrthographic(800, 480, 1, 100), material.Projection);
            Assert.AreEqual(material.View * material.Projection, material.ViewProjection);
        }

        private static Camera CreateCamera()
        {
            // SetCamera reads camera data only. No simulator or graphics device
            // is needed to test the actual render sequences with recording materials.
            Camera camera = (Camera)RuntimeHelpers.GetUninitializedObject(typeof(FreeRoamCamera));
            camera.XnaView = Matrix.CreateLookAt(new Vector3(0, 10, -30), Vector3.Zero, Vector3.Up);
            camera.XnaProjection = Matrix.CreatePerspectiveFieldOfView(0.8f, 1.6f, 1, 1000);
            return camera;
        }

        private static void MoveCamera(Camera camera)
        {
            camera.XnaView = Matrix.CreateLookAt(new Vector3(2048, 10, -30), new Vector3(2048, 0, 0), Vector3.Up);
            camera.XnaProjection = Matrix.CreatePerspectiveFieldOfView(0.5f, 1.2f, 2, 1500);
        }

        private static RenderFrame CreateFrame(int mountains = 0)
        {
            GameHost game = (GameHost)RuntimeHelpers.GetUninitializedObject(typeof(GameHost));
            GC.SuppressFinalize(game);
            RenderProcess process = (RenderProcess)RuntimeHelpers.GetUninitializedObject(typeof(RenderProcess));
            SetField(process, "<DisplaySize>k__BackingField", new Point(800, 480));
            SetField(game, "<RenderProcess>k__BackingField", process);
            SetField(game, "<UserSettings>k__BackingField", new ProfileUserSettingsModel
            {
                DynamicShadows = false,
                FarMountainsViewingDistance = mountains,
            });
            return new RenderFrame(game);
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static void Draw(RenderFrame frame, string method) =>
            typeof(RenderFrame).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(frame, new object[] { false });

        private sealed class NoopPrimitive : RenderPrimitive
        {
            public override void Draw() { }
        }

        private sealed class CaptureMaterial : Material
        {
            private readonly bool blended;
            public Matrix View { get; private set; }
            public Matrix Projection { get; private set; }
            public Matrix ViewProjection { get; private set; }

            public CaptureMaterial(bool blended) : base(null, null) => this.blended = blended;
            public override bool GetBlending() => blended;

            public override void Render(List<RenderItem> items, ref Matrix view, ref Matrix projection, ref Matrix viewProjection)
            {
                View = view;
                Projection = projection;
                ViewProjection = viewProjection;
            }
        }

        private sealed class CaptureMountainMaterial : TerrainSharedDistantMountain
        {
            public Matrix View { get; private set; }
            public Matrix Projection { get; private set; }
            public Matrix ViewProjection { get; private set; }

            private CaptureMountainMaterial() : base(null, null) { }

            public override void SetState(Material previousMaterial) { }
            public override void ResetState() { }

            public override void Render(List<RenderItem> items, ref Matrix view, ref Matrix projection, ref Matrix viewProjection)
            {
                View = view;
                Projection = projection;
                ViewProjection = viewProjection;
            }
        }
    }
}
