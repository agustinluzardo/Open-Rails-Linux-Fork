using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

using Riel.Common;
using Riel.Common.Position;
using Riel.Models.Settings;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Orts.ActivityRunner.Processes;
using Orts.ActivityRunner.Viewer3D;
using Orts.ActivityRunner.Viewer3D.RollingStock;
using Orts.Simulation;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Tests.Riel.Runtime
{
    [TestClass]
    [DoNotParallelize]
    public class TrainDrawerLoadingTests
    {
        // USA2/autotrnsetout positions from the 0.1.7 visibility log. The train
        // spans a tile boundary; its ends are about 1.1 km apart but exceed the
        // loader's 1.5 km Manhattan-distance limit when the camera changes ends.
        private static readonly WorldLocation FrontCamera = new WorldLocation(-12556, 14760, -530.98615f, 1165, -663.43365f);
        private static readonly WorldLocation RearCamera = new WorldLocation(-12556, 14759, 167.35172f, 1186.4014f, 493.04614f);

        [TestMethod]
        public void SwitchingEndsKeepsLoadedPlayerModelsAvailableAcrossTiles()
        {
            using Fixture fixture = new Fixture();
            Dictionary<TrainCar, TrainCarViewer> originalModels = fixture.Drawer.Cars;
            TrainCar lastCar = fixture.PlayerTrain.Cars[^1];
            Assert.IsTrue(WorldLocation.ApproximateDistance(FrontCamera, lastCar.WorldPosition.WorldLocation) > 1500);
            Assert.IsTrue(WorldLocation.GetDistanceSquared2D(FrontCamera, lastCar.WorldPosition.WorldLocation) < 1500 * 1500);

            foreach (WorldLocation camera in new[] { FrontCamera, RearCamera, FrontCamera, RearCamera })
            {
                fixture.SetCamera(camera);
                fixture.Drawer.LoadPrep();
                List<TrainCar> selected = fixture.SelectedCars;

                // Run the production loader, with already-loaded recording viewers.
                // It must reuse the complete snapshot without unloading/reloading an end.
                fixture.Drawer.Load();
                Assert.AreSame(originalModels, fixture.Drawer.Cars, "Changing camera ends must keep every loaded player model available.");
                CollectionAssert.AreEqual(fixture.PlayerTrain.Cars, selected);
                Assert.AreEqual(selected.Count, selected.Distinct().Count(), "The player locomotive must be selected only once.");
                foreach (RecordingCarViewer model in originalModels.Values)
                    Assert.AreEqual(0, model.UnloadCount);
            }
        }

        [TestMethod]
        public void CarsDetachedFromPlayerTrainReturnToDistanceStreaming()
        {
            using Fixture fixture = new Fixture();
            fixture.SetCamera(FrontCamera);
            fixture.Drawer.LoadPrep();
            fixture.Drawer.Load();
            TrainCar detached = fixture.PlayerTrain.Cars[^1];
            RecordingCarViewer model = (RecordingCarViewer)fixture.Drawer.Cars[detached];
            fixture.PlayerTrain.Cars.Remove(detached);
            Train detachedTrain = fixture.AddTrain(detached);
            Assert.AreSame(detachedTrain, detached.Train);

            fixture.Drawer.LoadPrep();
            Assert.IsFalse(fixture.SelectedCars.Contains(detached));
            fixture.Drawer.Load();
            Assert.IsFalse(fixture.Drawer.Cars.ContainsKey(detached));
            Assert.AreEqual(1, model.UnloadCount);
            foreach (TrainCar car in fixture.PlayerTrain.Cars)
                Assert.IsTrue(fixture.Drawer.Cars.ContainsKey(car));
        }

        [TestMethod]
        public void OtherTrainsStillUnloadOutsideCameraStreamingDistance()
        {
            using Fixture fixture = new Fixture();
            fixture.SetCamera(FrontCamera);
            TrainCar nearby = Fixture.Car(new WorldLocation(FrontCamera.Tile, FrontCamera.Location + Microsoft.Xna.Framework.Vector3.UnitX * 100));
            TrainCar distant = Fixture.Car(new WorldLocation(FrontCamera.Tile, FrontCamera.Location + Microsoft.Xna.Framework.Vector3.UnitX * 2000));
            fixture.AddTrain(nearby, distant);
            RecordingCarViewer nearbyModel = fixture.AddLoadedModel(nearby);
            RecordingCarViewer distantModel = fixture.AddLoadedModel(distant);

            fixture.Drawer.LoadPrep();
            Assert.IsTrue(fixture.SelectedCars.Contains(nearby));
            Assert.IsFalse(fixture.SelectedCars.Contains(distant));
            fixture.Drawer.Load();
            Assert.AreSame(nearbyModel, fixture.Drawer.Cars[nearby]);
            Assert.IsFalse(fixture.Drawer.Cars.ContainsKey(distant));
            Assert.AreEqual(0, nearbyModel.UnloadCount);
            Assert.AreEqual(1, distantModel.UnloadCount);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
            private readonly Simulator previousSimulator;
            private readonly Simulator simulator;
            private readonly Camera camera;
            internal TrainDrawer Drawer { get; }
            internal Train PlayerTrain { get; }
            internal List<TrainCar> SelectedCars => (List<TrainCar>)typeof(TrainDrawer)
                .GetField("VisibleCars", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Drawer);

            internal Fixture()
            {
                previousSimulator = Simulator.Instance;
                simulator = Uninitialized<Simulator>();
                ProfileUserSettingsModel settings = new ProfileUserSettingsModel { ViewingDistance = 1000 };
                SetField(typeof(Simulator), simulator, "<UserSettings>k__BackingField", settings);
                // Train's static logging defaults read the active simulator once.
                typeof(Simulator).GetProperty(nameof(Simulator.Instance)).SetValue(null, simulator);
                SetField(typeof(Simulator), simulator, "<Trains>k__BackingField", new TrainList(simulator));
                Viewer viewer = Uninitialized<Viewer>();
                SetField(typeof(Viewer), viewer, "<Simulator>k__BackingField", simulator);
                SetField(typeof(Viewer), viewer, "<UserSettings>k__BackingField", settings);
                LoaderProcess loader = Uninitialized<LoaderProcess>();
                SetField(typeof(ProcessBase), loader, "cancellationTokenSource", cancellation);
                SetField(typeof(Viewer), viewer, "<LoaderProcess>k__BackingField", loader);
                camera = Uninitialized<FreeRoamCamera>();
                viewer.Camera = camera;
                SetCamera(FrontCamera);

                MSTSDieselLocomotive locomotive = Uninitialized<MSTSDieselLocomotive>();
                SetField(typeof(TrainCar), locomotive, "worldPosition",
                    new WorldPosition(new WorldLocation(-12556, 14760, -530.98615f, 1158.5332f, -663.43365f)));
                PlayerTrain = AddTrain(locomotive,
                    Car(new WorldLocation(-12556, 14760, -510.8284f, 1158.5403f, -672.28186f)),
                    Car(new WorldLocation(-12556, 14760, -487.64258f, 1158.5253f, -683.16345f)),
                    Car(new WorldLocation(-12556, 14759, 159.04324f, 1176.1436f, 569.2305f)),
                    Car(new WorldLocation(-12556, 14759, 163.88658f, 1177.6738f, 540.4961f)),
                    Car(new WorldLocation(-12556, 14759, 168.7409f, 1179.209f, 511.76352f)));
                simulator.PlayerLocomotive = locomotive;
                Drawer = new TrainDrawer(viewer);
                foreach (TrainCar car in PlayerTrain.Cars)
                    AddLoadedModel(car);
            }

            internal void SetCamera(WorldLocation location) => SetField(typeof(Camera), camera, "cameraLocation", location);

            internal Train AddTrain(params TrainCar[] cars)
            {
                Train train = Uninitialized<Train>();
                SetField(typeof(Train), train, "<Cars>k__BackingField", cars.ToList());
                foreach (TrainCar car in cars)
                    SetField(typeof(TrainCar), car, "<Train>k__BackingField", train);
                simulator.Trains.Add(train);
                return train;
            }

            internal RecordingCarViewer AddLoadedModel(TrainCar car)
            {
                RecordingCarViewer model = Uninitialized<RecordingCarViewer>();
                SetField(typeof(TrainCarViewer), model, "<Car>k__BackingField", car);
                Drawer.Cars.Add(car, model);
                return model;
            }

            internal static TrainCar Car(WorldLocation location)
            {
                MSTSWagon car = Uninitialized<MSTSWagon>();
                SetField(typeof(TrainCar), car, "worldPosition", new WorldPosition(location));
                return car;
            }

            public void Dispose()
            {
                typeof(Simulator).GetProperty(nameof(Simulator.Instance)).SetValue(null, previousSimulator);
                cancellation.Dispose();
            }
        }

        private sealed class RecordingCarViewer : TrainCarViewer
        {
            private RecordingCarViewer() : base(null, null) { }
            internal int UnloadCount { get; private set; }
            public override void Unload() => UnloadCount++;
            internal override void LoadForPlayer() { }
            public override void HandleUserInput(in ElapsedTime elapsedTime) { }
            public override void RegisterUserCommandHandling() { }
            public override void UnregisterUserCommandHandling() { }
            public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime) { }
            internal override void Mark() { }
        }

        private static T Uninitialized<T>() where T : class
        {
            T value = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
            GC.SuppressFinalize(value);
            return value;
        }

        private static void SetField(Type type, object target, string name, object value) =>
            type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
