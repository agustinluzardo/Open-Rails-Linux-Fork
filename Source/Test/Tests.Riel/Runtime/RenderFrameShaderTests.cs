using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using Riel.Common.Position;
using Riel.Models.Settings;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Processes;
using Orts.ActivityRunner.Viewer3D;
using Orts.ActivityRunner.Viewer3D.Environment;
using Orts.Simulation;
using Orts.Simulation.World;

namespace Tests.Riel.Runtime
{
    [TestClass]
    [DoNotParallelize]
    public class RenderFrameShaderTests
    {
        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void ShaderDistanceFadingUsesPreparedCameraAfterUpdaterCrossesTile(bool mstsEnvironment, bool crossZ)
        {
            ProfileUserSettingsModel settings = new ProfileUserSettingsModel
            {
                DynamicShadows = false,
                MstsEnvironment = mstsEnvironment,
                ViewingDistance = 1000,
            };
            Camera camera = Uninitialized<FreeRoamCamera>();
            WorldLocation preparedLocation = new WorldLocation(-12556, 14759,
                crossZ ? 40 : 1023, 10, crossZ ? 1023 : 40);
            SetField(typeof(Camera), camera, "cameraLocation", preparedLocation);
            camera.XnaView = Matrix.Identity;
            camera.XnaProjection = Matrix.Identity;
            RenderFrame prepared = CreateFrame(settings);
            prepared.SetCamera(camera);
            Vector3 expected = camera.XnaLocation(preparedLocation);

            SharedMaterialManager manager = CreateManager(settings, camera, out EffectParameter viewerPosition);

            // The updater's next two-metre movement normalizes the live camera to
            // the adjacent tile while the renderer still draws prepared meshes.
            // Reading the new local camera coordinates makes those meshes appear
            // more than two kilometres away to the shader's distance fading.
            WorldLocation nextLocation = new WorldLocation(preparedLocation.Tile,
                preparedLocation.Location + (crossZ ? Vector3.UnitZ : Vector3.UnitX) * 2, normalize: true);
            SetField(typeof(Camera), camera, "cameraLocation", nextLocation);
            Vector3 nextExpected = camera.XnaLocation(nextLocation);
            Assert.AreNotEqual(preparedLocation.Tile, camera.Tile);
            Assert.IsTrue(Vector3.Distance(expected, nextExpected) > 2000);

            manager.UpdateShaders(prepared);
            Assert.AreEqual(expected, viewerPosition.GetValueVector3(),
                "The scenery uniform must use the same camera tile as the prepared meshes and matrices.");

            RenderFrame next = CreateFrame(settings);
            next.SetCamera(camera);
            manager.UpdateShaders(next);
            Assert.AreEqual(nextExpected, viewerPosition.GetValueVector3(),
                "The next frame must use the camera captured for its own meshes.");

            manager.UpdateShaders(prepared);
            Assert.AreEqual(expected, viewerPosition.GetValueVector3(),
                "Preparing the next frame must not change the previous frame's camera uniform.");
        }

        private static SharedMaterialManager CreateManager(ProfileUserSettingsModel settings, Camera camera,
            out EffectParameter viewerPosition)
        {
            Viewer viewer = Uninitialized<Viewer>();
            SetField(typeof(Viewer), viewer, "<UserSettings>k__BackingField", settings);
            viewer.Camera = camera;
            World world = Uninitialized<World>();
            SkyViewer sky = Uninitialized<SkyViewer>();
            SetField(typeof(SkyViewer), sky, "<SolarDirection>k__BackingField", Vector3.Up);
            SetField(typeof(World), world, "<Sky>k__BackingField", sky);
            MSTSSkyDrawer mstsSky = Uninitialized<MSTSSkyDrawer>();
            mstsSky.mstsskysolarDirection = Vector3.Up;
            mstsSky.mstsskyfogDistance = 20000;
            SetField(typeof(World), world, "<MSTSSky>k__BackingField", mstsSky);
            SetField(typeof(Viewer), viewer, "<World>k__BackingField", world);
            Simulator simulator = Uninitialized<Simulator>();
            SetField(typeof(Simulator), simulator, "<Weather>k__BackingField", new Weather
            {
                FogVisibilityDistance = 20000,
            });
            SetField(typeof(Viewer), viewer, "<Simulator>k__BackingField", simulator);

            // These are real MonoGame parameters: UpdateShaders executes the
            // production shader setters and we read back the actual uniform.
            // Only GPU allocation and unrelated simulator initialization are bypassed.
            SceneryShader scenery = Uninitialized<SceneryShader>();
            SetField(typeof(SceneryShader), scenery, "lightVector_ZFar", Parameter("LightVector_ZFar", 4));
            SetField(typeof(SceneryShader), scenery, "headlightPosition", Parameter("HeadlightPosition", 4));
            SetField(typeof(SceneryShader), scenery, "fog", Parameter("Fog", 4));
            SetField(typeof(SceneryShader), scenery, "overcast", Parameter("Overcast", 2));
            viewerPosition = Parameter("ViewerPos", 3);
            SetField(typeof(SceneryShader), scenery, "viewerPos", viewerPosition);
            ParticleEmitterShader particles = Uninitialized<ParticleEmitterShader>();
            SetField(typeof(ParticleEmitterShader), particles, "fog", Parameter("Fog", 4));

            SharedMaterialManager manager = Uninitialized<SharedMaterialManager>();
            SetField(typeof(SharedMaterialManager), manager, "Viewer", viewer);
            SetField(typeof(SharedMaterialManager), manager, "SceneryShader", scenery);
            SetField(typeof(SharedMaterialManager), manager, "ParticleEmitterShader", particles);
            return manager;
        }

        private static EffectParameter Parameter(string name, int components)
        {
            ConstructorInfo constructor = typeof(EffectParameter)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(candidate => candidate.GetParameters().Length == 10);
            return (EffectParameter)constructor.Invoke(new object[]
            {
                EffectParameterClass.Vector, EffectParameterType.Single, name, 1, components,
                string.Empty, null, null, null, new float[components],
            });
        }

        private static RenderFrame CreateFrame(ProfileUserSettingsModel settings)
        {
            GameHost game = Uninitialized<GameHost>();
            RenderProcess process = Uninitialized<RenderProcess>();
            SetField(typeof(RenderProcess), process, "<DisplaySize>k__BackingField", new Point(800, 480));
            SetField(typeof(GameHost), game, "<RenderProcess>k__BackingField", process);
            SetField(typeof(GameHost), game, "<UserSettings>k__BackingField", settings);
            return new RenderFrame(game);
        }

        private static T Uninitialized<T>() where T : class
        {
            T instance = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
            GC.SuppressFinalize(instance);
            return instance;
        }

        private static void SetField(Type declaringType, object target, string name, object value) =>
            declaringType.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
    }
}
