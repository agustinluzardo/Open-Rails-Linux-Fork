// COPYRIGHT 2009, 2010, 2011, 2012, 2013 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// This file is the responsibility of the 3D & Environment Team. 

using Orts.Simulation;
using Orts.Simulation.AIs;
using Orts.Simulation.RollingStocks;
using Orts.ActivityRunner.Viewer3D.RollingStock;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Common;
using Orts.ActivityRunner.Viewer3D.RollingStock.CabView;

namespace Orts.ActivityRunner.Viewer3D
{
    public class TrainDrawer
    {
        private readonly Viewer Viewer;

        // THREAD SAFETY:
        //   All accesses must be done in local variables. No modifications to the objects are allowed except by
        //   assignment of a new instance (possibly cloned and then modified).
        public volatile Dictionary<TrainCar, TrainCarViewer> Cars = new Dictionary<TrainCar, TrainCarViewer>();
        private volatile List<TrainCar> VisibleCars = new List<TrainCar>();
        private volatile TrainCar PlayerCar;
        private TrainCar preparedPlayerCar;
        private static readonly TimeSpan IntermediatePublishInterval = TimeSpan.FromMilliseconds(100);
        private readonly ConcurrentDictionary<int, byte> selectedTrainTraces = new();
        private readonly ConcurrentDictionary<int, byte> sceneHeightTrainTraces = new();
        private readonly ConcurrentDictionary<int, byte> loadingTrainTraces = new();
        private readonly ConcurrentDictionary<int, byte> loadedTrainTraces = new();
        private readonly ConcurrentDictionary<int, byte> deferredTrainTraces = new();
        private readonly ConcurrentDictionary<int, byte> publishedTrainTraces = new();
        private readonly ConcurrentDictionary<int, byte> preparedTrainTraces = new();
        private readonly ConcurrentDictionary<int, bool> tracedAiTrainNumbers = new();

        public TrainDrawer(Viewer viewer)
        {
            Viewer = viewer;
            Viewer.Simulator.QueryCarViewerLoaded += Simulator_QueryCarViewerLoaded;
        }

        private void Simulator_QueryCarViewerLoaded(object sender, QueryCarViewerLoadedEventArgs e)
        {
            if (Cars.ContainsKey(e.Car))
                e.Loaded = true;
        }

        public void Load()
        {
            var cancellation = Viewer.LoaderProcess.CancellationToken;
            var visibleCars = VisibleCars;
            var visibleSet = new HashSet<TrainCar>(visibleCars);
            var cars = Cars;
            TrainCar playerCar = PlayerCar;
            bool playerReady = playerCar != null && playerCar == preparedPlayerCar && cars.ContainsKey(playerCar);
            if (!playerReady && playerCar != null && cars.TryGetValue(playerCar, out TrainCarViewer existingPlayer))
            {
                existingPlayer.LoadForPlayer();
                preparedPlayerCar = playerCar;
                playerReady = true;
            }
            if (visibleCars.Any(c => !cars.ContainsKey(c)) || cars.Keys.Any(c => !visibleSet.Contains(c)))
            {
                var newCars = new Dictionary<TrainCar, TrainCarViewer>(visibleCars.Count);
                var publishedAiTrains = new HashSet<AITrain>();
                foreach (TrainCar previousCar in cars.Keys)
                    if (previousCar.Train is AITrain previousTrain)
                        publishedAiTrains.Add(previousTrain);
                long lastPublication = 0;
                for (int index = 0; index < visibleCars.Count; index++)
                {
                    if (cancellation.IsCancellationRequested)
                        break;
                    TrainCar car = visibleCars[index];
                    try
                    {
                        if (cars.TryGetValue(car, out TrainCarViewer trainCarViewer))
                            newCars.Add(car, trainCarViewer);
                        else
                        {
                            TraceVisual(car, "load-started", loadingTrainTraces);
                            TrainCarViewer loaded = LoadCar(car);
                            newCars.Add(car, loaded);
                            if (loaded != null)
                            {
                                if (car == playerCar && !playerReady)
                                {
                                    loaded.LoadForPlayer();
                                    preparedPlayerCar = playerCar;
                                    playerReady = true;
                                }
                                TraceVisual(car, "model-loaded", loadedTrainTraces);
                                if (!playerReady && car.Train is AITrain)
                                    TraceVisual(car, "player-not-ready", deferredTrainTraces);
                                // The next model may take several seconds. Let the renderer
                                // show the first car of every new AI train immediately. Rate
                                // limit additional cars, so a slow next model cannot delay a
                                // new train that finished inside the 100 ms interval.
                                if (playerReady && car.Train is AITrain aiTrain && index + 1 < visibleCars.Count &&
                                    (!publishedAiTrains.Contains(aiTrain) || lastPublication == 0 ||
                                     Stopwatch.GetElapsedTime(lastPublication) >= IntermediatePublishInterval))
                                {
                                    Cars = VisibleSnapshot(visibleCars, newCars, cars);
                                    foreach (TrainCar publishedCar in newCars.Keys)
                                    {
                                        if (publishedCar.Train is AITrain publishedTrain)
                                            publishedAiTrains.Add(publishedTrain);
                                        TraceVisual(publishedCar, "published", publishedTrainTraces);
                                    }
                                    lastPublication = Stopwatch.GetTimestamp();
                                }
                            }
                        }
                    }
                    catch (Exception error)
                    {
                        Trace.WriteLine(new FileLoadException(car.WagFilePath, error));
                    }
                }
                Cars = newCars;
                foreach (TrainCar car in newCars.Keys)
                    TraceVisual(car, "published", publishedTrainTraces);
                // For cars no longer visible, remove their attached sounds.
                foreach (var car in cars)
                    if (!visibleSet.Contains(car.Key))
                        car.Value.Unload();
            }

            // Ensure the player locomotive has a cab view loaded and anything else they need.
            cars = Cars;
            if (PlayerCar != null && cars.TryGetValue(PlayerCar, out TrainCarViewer value))
            {
                value.LoadForPlayer();
                preparedPlayerCar = PlayerCar;
            }
        }

        private static Dictionary<TrainCar, TrainCarViewer> VisibleSnapshot(
            List<TrainCar> visibleCars,
            Dictionary<TrainCar, TrainCarViewer> loadedCars,
            Dictionary<TrainCar, TrainCarViewer> previousCars)
        {
            var snapshot = new Dictionary<TrainCar, TrainCarViewer>(visibleCars.Count);
            foreach (TrainCar car in visibleCars)
            {
                if (loadedCars.TryGetValue(car, out TrainCarViewer viewer) ||
                    previousCars.TryGetValue(car, out viewer))
                    snapshot.Add(car, viewer);
            }
            return snapshot;
        }

        internal void Mark()
        {
            var cars = Cars;
            foreach (var car in cars.Values)
            {
                car.Mark();
                if (car.LightDrawer != null)
                    car.LightDrawer.Mark();
            }
            CabTextureManager.Mark(Viewer);
        }

        public TrainCarViewer GetViewer(TrainCar car)
        {
            Dictionary<TrainCar, TrainCarViewer> cars = Cars;
            if (cars.TryGetValue(car, out TrainCarViewer value))
                return value;
            Dictionary<TrainCar, TrainCarViewer> newCars = new Dictionary<TrainCar, TrainCarViewer>(cars)
            {
                { car, LoadCar(car) }
            };
            // This will actually race against the loader's Load() call above, but that's okay since the TrainCar
            // we're given here is always the player's locomotive - specifically included in LoadPrep() below.
            Cars = newCars;
            return newCars[car];
        }

        public void LoadPrep()
        {
            var visibleCars = new List<TrainCar>();
            var removeDistance = Viewer.UserSettings.ViewingDistance * 1.5f;
            var playerCar = Viewer.PlayerLocomotive;
            var cameraLocation = Viewer.Camera.CameraWorldLocation;
            visibleCars.Add(playerCar);
            foreach (var train in Viewer.Simulator.Trains)
                foreach (var car in train.Cars)
                    if (car != playerCar && WorldLocation.ApproximateDistance(cameraLocation, car.WorldPosition.WorldLocation) < removeDistance)
                    {
                        visibleCars.Add(car);
                        TraceVisual(car, "selected", selectedTrainTraces);
                        // AI models can be selected while still hidden 1000 m below the track.
                        // Record when the car first reaches roughly the camera's height as well.
                        if (Math.Abs(car.WorldPosition.WorldLocation.Location.Y - cameraLocation.Location.Y) < 150)
                            TraceVisual(car, "scene-height", sceneHeightTrainTraces);
                    }
            // The simulator's train-list order need not match what the player sees.
            // Load nearby cars before distant services when new AI trains appear.
            visibleCars.Sort(1, visibleCars.Count - 1, Comparer<TrainCar>.Create((a, b) =>
                WorldLocation.GetDistanceSquared(cameraLocation, a.WorldPosition.WorldLocation).CompareTo(
                    WorldLocation.GetDistanceSquared(cameraLocation, b.WorldPosition.WorldLocation))));
            VisibleCars = visibleCars;
            PlayerCar = playerCar;
        }

        public void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            var cars = Cars;
            foreach (var car in cars.Values)
            {
                car.PrepareFrame(frame, elapsedTime);
                TraceVisual(car.Car, "frame-prepared", preparedTrainTraces);
            }
            // Do the lights separately for proper alpha sorting
            foreach (var car in cars.Values)
                if (car.LightDrawer != null)
                    car.LightDrawer.PrepareFrame(frame, elapsedTime);
        }

        // Correlate simulation placement with the renderer's first selection, model load and
        // frame for one AI service. Wall time distinguishes a slow renderer from a late spawn.
        private void TraceVisual(TrainCar car, string stage, ConcurrentDictionary<int, byte> recorded)
        {
            if (car?.Train is not AITrain train || recorded.ContainsKey(train.Number) ||
                !tracedAiTrainNumbers.GetOrAdd(train.Number, static number => DiagnosticTrace.AiTrain(number)) ||
                !recorded.TryAdd(train.Number, 0))
                return;

            Trace.TraceInformation("[AiVisual] wallUtc={0:O} simTime={1:F1} train={2} stage={3} car={4} heightM={5:F1}",
                DateTime.UtcNow, Viewer.Simulator.ClockTime, train.Number, stage, car.CarID,
                car.WorldPosition.WorldLocation.Location.Y - Viewer.Camera.CameraWorldLocation.Location.Y);
        }

        private TrainCarViewer LoadCar(TrainCar car)
        {
            if (DiagnosticTrace.LoadMarkers)
                Trace.Write("C");
            TrainCarViewer carViewer =
                car is MSTSDieselLocomotive ? new MSTSDieselLocomotiveViewer(Viewer, car as MSTSDieselLocomotive) :
                car is MSTSElectricLocomotive ? new MSTSElectricLocomotiveViewer(Viewer, car as MSTSElectricLocomotive) :
                car is MSTSSteamLocomotive ? new MSTSSteamLocomotiveViewer(Viewer, car as MSTSSteamLocomotive) :
                car is MSTSLocomotive ? new MSTSLocomotiveViewer(Viewer, car as MSTSLocomotive) :
                car is MSTSWagon ? new MSTSWagonViewer(Viewer, car as MSTSWagon) :
                null;
            return carViewer;
        }
    }
}
