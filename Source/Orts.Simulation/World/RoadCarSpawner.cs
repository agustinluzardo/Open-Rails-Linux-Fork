using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts.Models;

namespace Orts.Simulation.World
{
    public class RoadCarSpawner
    {
        internal static readonly bool TraceCrossings = System.Environment.GetEnvironmentVariable("RIEL_TRACE_ROAD_CROSSINGS") == "1";
        private double lastSpawnedTime;
        private double nextSpawnTime;
        private int crossingRevision = -1;

        public const float StopDistance = 10;

        internal const float RampLength = 2;
        internal const float TrackHalfWidth = 1;
        internal const float TrackMergeDistance = 7; // Must be >= 2 * (RampLength + TrackHalfWidth).
        internal const float TrackRailHeight = 0.275f;
        internal const float TrainRailHeightMaximum = 1;

        public CarSpawnerObject CarSpawnerObj { get; }

        // THREAD SAFETY:
        //   All accesses must be done in local variables. No modifications to the objects are allowed except by
        //   assignment of a new instance (possibly cloned and then modified).
        public List<RoadCar> Cars { get; } = new List<RoadCar>();
        // Level crossing which interact with this spawner. Distances are used for speed curves and the list must be sorted by distance from spawner.
        public List<RoadCarCrossing> Crossings { get; private set; } = new List<RoadCarCrossing>();

        public TrackTraveller Traveller { get; private set; }
        public float Length { get; private set; }

        public RoadCarSpawner(in WorldPosition position, CarSpawnerObject carSpawnerObj)
        {
            Debug.Assert(TrackMergeDistance >= 2 * (RampLength + TrackHalfWidth), "TrackMergeDistance is less than 2 * (RampLength + TrackHalfWidth); vertical inconsistencies will occur at close, but not merged, tracks.");
            CarSpawnerObj = carSpawnerObj;

            if (RuntimeDataResolver.Instance.TrackWorld.RoadDatabase == null || Simulator.Instance.CarSpawnerLists == null)
                throw new InvalidOperationException("RoadCarSpawner requires a RDB and CARSPAWN.DAT");

            int start = CarSpawnerObj.TrackItemIds.RoadDbItems.Count > 0 ? CarSpawnerObj.TrackItemIds.RoadDbItems[0] : -1;
            int end = CarSpawnerObj.TrackItemIds.RoadDbItems.Count > 1 ? CarSpawnerObj.TrackItemIds.RoadDbItems[1] : -1;
            ImmutableArray<FreeTrainSimulator.Models.Track.TrackItemBase> trItems = RuntimeDataResolver.Instance.TrackWorld.RoadDatabase.TrackItems;
            ref readonly WorldLocation startLocation = ref trItems[start].Location;
            ref readonly WorldLocation endLocation = ref trItems[end].Location;

            TrackTraveller? spawnerTraveller = TrackTraveller.InitializeTraveller(startLocation, TrackDirection.Ahead, TrackDataBaseType.Road);
            if (spawnerTraveller.HasValue)
            {
                Traveller = spawnerTraveller.Value;
                Length = Traveller.DistanceTo(endLocation) ?? -1f;
                if (Length < 0)
                {
                    Traveller = Traveller.Reverse();
                    Length = Traveller.DistanceTo(endLocation) ?? -1f;
                    if (Length < 0)
                        Trace.TraceWarning("{0} car spawner {1} doesn't have connected road route between {2} and {3}", position, carSpawnerObj.UiD, startLocation, endLocation);
                }
            }
            else
            {
                Trace.TraceWarning("{0} car spawner {1} could not find road track near {2}", position, carSpawnerObj.UiD, startLocation);
            }

            RefreshCrossings();
        }

        private void RefreshCrossings()
        {
            LevelCrossings levelCrossings = Simulator.Instance.LevelCrossings;
            int revision = levelCrossings.RegistrationRevision;
            if (revision == crossingRevision)
                return;

            // Resolve crossings by reachability along the actual road path instead of relying on
            // TrackItemSelectors encountered while walking node-by-node.
            List<(float Distance, LevelCrossingItem Item)> reachableCrossings = new List<(float, LevelCrossingItem)>();
            float crossingSearchDistance = Length > 0 ? Length + TrackMergeDistance : float.MaxValue;

            foreach (LevelCrossingItem crossingItem in levelCrossings.RoadCrossingItems.Values.Where(item => item.CrossingGroup != null))
            {
                float distance = crossingItem.DistanceTo(Traveller, crossingSearchDistance);
                if (distance >= 0 && (Length <= 0 || distance <= Length + TrackMergeDistance))
                    reachableCrossings.Add((distance, crossingItem));
            }

            // Some legacy routes contain a working rail crossing/world barrier but an RDB crossing
            // item which is absent, malformed or cannot be snapped by the migrated road model. In
            // that case the original road-car code has no stop point at all, so every car drives
            // through even though the barrier is active. Recover one stop point per crossing group
            // by projecting the rail crossing's world position onto this road route.
            HashSet<LevelCrossing> representedGroups = reachableCrossings
                .Where(crossing => crossing.Item.CrossingGroup != null)
                .Select(crossing => crossing.Item.CrossingGroup)
                .ToHashSet();

            int recoveredCrossingGroups = 0;
            foreach (IGrouping<LevelCrossing, LevelCrossingItem> crossingGroup in levelCrossings.TrackCrossingItems.Values
                .Where(item => item.CrossingGroup != null && !representedGroups.Contains(item.CrossingGroup))
                .GroupBy(item => item.CrossingGroup))
            {
                float bestDistance = float.MaxValue;
                LevelCrossingItem bestItem = null;

                foreach (LevelCrossingItem railItem in crossingGroup)
                {
                    TrackTraveller? projectedRoadTraveller = TrackTraveller.InitializeTraveller(
                        railItem.Location, TrackDirection.Ahead, TrackDataBaseType.Road);
                    if (!projectedRoadTraveller.HasValue)
                        continue;

                    float? routeDistance = Traveller.DistanceTo(projectedRoadTraveller.Value, crossingSearchDistance);
                    if (!routeDistance.HasValue || routeDistance.Value < 0 ||
                        (Length > 0 && routeDistance.Value > Length + TrackMergeDistance) ||
                        routeDistance.Value >= bestDistance)
                        continue;

                    bestDistance = routeDistance.Value;
                    bestItem = railItem;
                }

                if (bestItem != null)
                {
                    reachableCrossings.Add((bestDistance, bestItem));
                    representedGroups.Add(crossingGroup.Key);
                    recoveredCrossingGroups++;
                }
            }

            List<RoadCarCrossing> newCrossings = reachableCrossings
                .OrderBy(crossing => crossing.Distance)
                .Select(crossing => new RoadCarCrossing(crossing.Item, crossing.Distance, float.NaN))
                .ToList();

            // Existing cars may have already passed some of these crossings; each car
            // recomputes its next index from its current position on the next update.
            Crossings = newCrossings;
            foreach (RoadCar car in Cars)
                car.ResetCrossings();
            crossingRevision = revision;

            if (TraceCrossings)
                Trace.TraceInformation("[RoadCrossing] spawner={0} revision={1} length={2:F1} stops={3} [{4}]",
                    CarSpawnerObj.UiD, revision, Length, Crossings.Count,
                    string.Join("; ", Crossings.Select(c => $"item={c.Item.TrackItemId} distance={c.Distance:F1} group={c.Item.CrossingGroup?.GetHashCode()}")));

            if (recoveredCrossingGroups > 0)
                Trace.TraceInformation("[RoadCarSpawner] {0}: recovered {1} level-crossing group(s) from rail geometry; total stops={2}",
                    CarSpawnerObj.UiD, recoveredCrossingGroups, Crossings.Count);
            else if (TraceCrossings && Crossings.Count == 0 && revision > 0)
                Trace.TraceWarning("[RoadCarSpawner] {0}: no reachable level crossings; roadItems={1}, railItems={2}, length={3:F1}m",
                    CarSpawnerObj.UiD, levelCrossings.RoadCrossingItems.Count,
                    levelCrossings.TrackCrossingItems.Count, Length);
        }

        public void Update(in ElapsedTime elapsedTime)
        {
            RefreshCrossings();
            foreach (RoadCar car in Cars)
                car.Update(elapsedTime);

            lastSpawnedTime += elapsedTime.ClockSeconds;
            if (Length > 0 && lastSpawnedTime >= nextSpawnTime && (Cars.Count == 0 || Cars[^1].Travelled > Cars[^1].Length))
            {
                Cars.Add(new RoadCar(this, CarSpawnerObj.CarAverageSpeed, CarSpawnerObj.CarSpawnerListIndex));

                lastSpawnedTime = 0;
                nextSpawnTime = CarSpawnerObj.CarFrequency * (0.75 + StaticRandom.NextDouble() / 2);
            }

            Cars.RemoveAll(car => car.Travelled >= Length);

            List<RoadCarCrossing> crossings = Crossings;
            if (crossings.Any(c => float.IsNaN(c.TrackHeight)))
            {
                Crossings.Clear();
                Crossings.AddRange(crossings.Select(c =>
                {
                    if (!float.IsNaN(c.TrackHeight) || !Simulator.Instance.LevelCrossings.RoadToTrackCrossingItems.TryGetValue(c.Item, out LevelCrossingItem value))
                        return c;
                    float height = value.Location.Location.Y + TrackRailHeight - c.Item.Location.Location.Y;
                    return new RoadCarCrossing(c.Item, c.Distance, height <= TrainRailHeightMaximum ? height : 0);
                }));
            }
        }

        internal float GetRoadHeightAdjust(float distance)
        {
            List<RoadCarCrossing> crossings = Crossings;
            for (var i = 0; i < crossings.Count; i++)
            {
                // Crossing is too far down the path, we can quit.
                if (distance <= crossings[i].DistanceAdjust1)
                    break;
                if (!float.IsNaN(crossings[i].TrackHeight))
                {
                    // Location is approaching a track.
                    if (crossings[i].DistanceAdjust1 <= distance && distance <= crossings[i].DistanceAdjust2)
                        return MathHelper.Lerp(0, crossings[i].TrackHeight, (distance - crossings[i].DistanceAdjust1) / RampLength);
                    // Location is crossing a track.
                    if (crossings[i].DistanceAdjust2 <= distance && distance <= crossings[i].DistanceAdjust3)
                        return crossings[i].TrackHeight;
                    // Crossings are close enough to count as joined.
                    if (i + 1 < crossings.Count && !float.IsNaN(crossings[i + 1].TrackHeight) && crossings[i + 1].Distance - crossings[i].Distance < TrackMergeDistance)
                    {
                        // Location is between two crossing tracks.
                        if (crossings[i].DistanceAdjust3 <= distance && distance <= crossings[i + 1].DistanceAdjust2)
                            return MathHelper.Lerp(crossings[i].TrackHeight, crossings[i + 1].TrackHeight, (distance - crossings[i].DistanceAdjust3) / (crossings[i + 1].DistanceAdjust2 - crossings[i].DistanceAdjust3));
                    }
                    else
                    {
                        // Location is passing a track.
                        if (crossings[i].DistanceAdjust3 <= distance && distance <= crossings[i].DistanceAdjust4)
                            return MathHelper.Lerp(crossings[i].TrackHeight, 0, (distance - crossings[i].DistanceAdjust3) / RampLength);
                    }
                }
            }
            return 0;
        }
    }
}
