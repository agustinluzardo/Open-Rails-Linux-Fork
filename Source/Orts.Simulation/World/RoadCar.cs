using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Common.Xna;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;

namespace Orts.Simulation.World
{
    public class RoadCar : IWorldPosition
    {
        public const float VisualHeightAdjustment = 0.1f;
        private const float AccelerationFactor = 5;
        private const float BrakingFactor = 5;
        private float speedMax;
        private int nextCrossingIndex;
        private int loggedCrossingIndex = -1;
        private bool loggedHasTrain;

        private WorldPosition position;

        public RoadCarSpawner Spawner { get; }

        public int CarSpawnerListIdx { get; }
        public int Type { get; }
        public float Length { get; }
        public float Travelled { get; private set; }
        public bool IgnoreXRotation { get; }
        public bool CarriesCamera { get; set; }

        public TrackTraveller FrontTraveller { get; private set; }
        public TrackTraveller RearTraveller { get; private set; }
        public float Speed { get; private set; }

        public Vector3 FrontLocation
        {
            get
            {
                return new Vector3(FrontTraveller.Location.Location.X,
                    FrontTraveller.Location.Location.Y + Math.Max(Spawner.GetRoadHeightAdjust(Travelled - Length * 0.25f), 0) + VisualHeightAdjustment,
                    FrontTraveller.Location.Location.Z);
            }
        }
        public Vector3 RearLocation
        {
            get
            {
                WorldLocation location = RearTraveller.Location.NormalizeTo(FrontTraveller.Location.Tile);
                return new Vector3(location.Location.X,
                    location.Location.Y + Math.Max(Spawner.GetRoadHeightAdjust(Travelled + Length * 0.25f), 0) + VisualHeightAdjustment,
                    location.Location.Z);
            }
        }

        public ref readonly WorldPosition WorldPosition
        {
            get
            {
                // TODO: Add 0.1f to Y to put wheels above road. Matching MSTS?
                Vector3 front = FrontLocation;
                Vector3 rear = RearLocation;
                float frontY = front.Y;
                float rearY = rear.Y;
                if (IgnoreXRotation)
                {
                    frontY -= VisualHeightAdjustment;
                    rearY -= VisualHeightAdjustment;
                    if (Math.Abs(frontY - rearY) > 0.01f)
                    {
                        if (frontY > rearY)
                            rearY = frontY;
                        else
                            frontY = rearY;
                    }
                }
                position = new WorldPosition(FrontTraveller.Location.Tile, MatrixExtension.XNAMatrixFromMSTSCoordinates(front.X, frontY, front.Z, rear.X, rearY, rear.Z));
                return ref position;
            }
        }

        public RoadCar(RoadCarSpawner spawner, float averageSpeed, int carSpawnerListIdx)
        {
            Spawner = spawner;
            CarSpawnerListIdx = carSpawnerListIdx;
            Type = StaticRandom.Next() % Simulator.Instance.CarSpawnerLists[this.CarSpawnerListIdx].Count;
            Length = Simulator.Instance.CarSpawnerLists[this.CarSpawnerListIdx][Type].Distance;
            // Front and rear travellers approximate wheel positions at 25% and 75% along vehicle.
            FrontTraveller = spawner.Traveller.Move(Length * 0.15f);
            RearTraveller = spawner.Traveller.Move(Length * 0.85f);
            // Travelled is the center of the vehicle.
            Travelled = Length * 0.50f;
            Speed = speedMax = averageSpeed * (0.75f + (float)StaticRandom.NextDouble() / 2);
            IgnoreXRotation = Simulator.Instance.CarSpawnerLists[this.CarSpawnerListIdx].IgnoreXRotation;
        }

        internal void ResetCrossings()
        {
            nextCrossingIndex = 0;
            loggedCrossingIndex = -1;
        }

        public void Update(in ElapsedTime elapsedTime)
        {
            List<RoadCarCrossing> crossings = Spawner.Crossings;

            // Keep monitoring a crossing until the whole vehicle has physically cleared it.
            // The old Open Rails-era shortcut also discarded a crossing when the car merely became
            // "too close to stop". With the migrated crossing update order that can happen just before
            // HasTrain becomes true, so the car permanently forgets a barrier which is already lowering.
            while (nextCrossingIndex < crossings.Count)
            {
                RoadCarCrossing crossing = crossings[nextCrossingIndex];
                bool sameGroupAsPrevious = nextCrossingIndex > 0
                    && crossing.Item.CrossingGroup != null
                    && crossing.Item.CrossingGroup == crossings[nextCrossingIndex - 1].Item.CrossingGroup;
                bool rearClearedCrossing = Travelled - Length / 2 > crossing.Distance + RoadCarSpawner.TrackHalfWidth;

                if (!sameGroupAsPrevious && !rearClearedCrossing)
                    break;

                nextCrossingIndex++;
            }

            if (RoadCarSpawner.TraceCrossings && nextCrossingIndex < crossings.Count)
            {
                RoadCarCrossing next = crossings[nextCrossingIndex];
                bool hasTrain = next.Item.CrossingGroup?.HasTrain == true;
                if (next.Distance - Travelled < 150 &&
                    (loggedCrossingIndex != nextCrossingIndex || loggedHasTrain != hasTrain))
                {
                    Trace.TraceInformation("[RoadCrossing] spawner={0} car={1} next={2} item={3} distance={4:F1} front={5:F1} group={6} HasTrain={7}",
                        Spawner.CarSpawnerObj.UiD, Type, nextCrossingIndex, next.Item.TrackItemId,
                        next.Distance, Travelled + Length / 2, next.Item.CrossingGroup?.GetHashCode(), hasTrain);
                    loggedCrossingIndex = nextCrossingIndex;
                    loggedHasTrain = hasTrain;
                }
            }

            // Calculate all the distances to items we need to stop at (level crossings, other cars).
            List<float> stopDistances = new List<float>();
            for (int i = nextCrossingIndex; i < crossings.Count; i++)
            {
                if (crossings[i].Item.CrossingGroup != null && crossings[i].Item.CrossingGroup.HasTrain)
                {
                    float carFront = Travelled + Length / 2;

                    // Once the front has entered the crossing, keep moving so the vehicle clears the rails.
                    // Otherwise stop at the normal stop line. If the gates activate late and that line is
                    // already behind the car, stop immediately instead of driving through the closed crossing.
                    if (carFront < crossings[i].Distance)
                    {
                        float stopLine = crossings[i].Distance - RoadCarSpawner.StopDistance;
                        stopDistances.Add(Math.Max(stopLine, carFront));
                    }
                    break;
                }
            }
            // TODO: Maybe optimise this?
            List<RoadCar> cars = Spawner.Cars;
            int spawnerIndex = cars.IndexOf(this);
            if (spawnerIndex > 0)
            {
                if (!cars[spawnerIndex - 1].CarriesCamera)
                    stopDistances.Add(cars[spawnerIndex - 1].Travelled - cars[spawnerIndex - 1].Length / 2);
                else
                    stopDistances.Add(cars[spawnerIndex - 1].Travelled - cars[spawnerIndex - 1].Length * 0.65f - 4 - cars[spawnerIndex - 1].Speed * 0.5f);
            }

            // Calculate whether we're too close to the minimum stopping distance (and need to slow down) or going too slowly (and need to speed up).
            var stopDistance = stopDistances.Count > 0 ? stopDistances.Min() - Travelled - Length / 2 : float.MaxValue;
            var slowingDistance = BrakingFactor * Length;
            if (stopDistance <= 0)
                Speed = 0;
            else if (stopDistance < slowingDistance)
                Speed = speedMax * (float)Math.Sin((Math.PI / 2) * (stopDistance / slowingDistance));
            else if (Speed < speedMax)
                Speed = (float)Math.Min(Speed + AccelerationFactor / Length * elapsedTime.ClockSeconds, speedMax);
            else if (Speed > speedMax)
                Speed = (float)Math.Max(Speed - AccelerationFactor / Length * elapsedTime.ClockSeconds * 2, speedMax);

            double distance = elapsedTime.ClockSeconds * Speed;
            Travelled += (float)distance;
            FrontTraveller = FrontTraveller.Move((float)distance);
            RearTraveller = RearTraveller.Move((float)distance);
        }

        public void ChangeSpeed(float speed)
        {
            if (speed > 0)
            {
                if (speedMax < Spawner.CarSpawnerObj.CarAverageSpeed * 1.25f)
                    speedMax = Math.Min(speedMax + speed * 2, Spawner.CarSpawnerObj.CarAverageSpeed * 1.25f);
            }
            else if (speed < 0)
            {
                if (speedMax > Spawner.CarSpawnerObj.CarAverageSpeed * 0.25f)
                    speedMax = Math.Max(speedMax + speed * 2, Spawner.CarSpawnerObj.CarAverageSpeed * 0.25f);
            }
        }
    }
}
