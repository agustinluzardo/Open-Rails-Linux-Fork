using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

using Riel.Common;
using Riel.Common.Position;
using Riel.Models.Signalling;
using Riel.Models.Track;
using Riel.Runtime.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Orts.Simulation.Signalling;
using Orts.Simulation.Track;

using TrackItemBase = Riel.Models.Track.TrackItemBase;

namespace Tests.Orts
{
    [TestClass]
    [DoNotParallelize]
    public class SignalEnvironmentTests
    {
        [TestMethod]
        public void UnplacedSpeedpostAfterMergedHeadsDoesNotKeepOutOfRangeSignalIndex()
        {
            SignalTrackItem first = SignalItem(0, 20);
            SignalTrackItem second = SignalItem(1, 20);
            SpeedpostTrackItem unplaced = SpeedItem(2, 25, 40);
            SignalEnvironment environment = BuildEnvironment(true, first, second, unplaced);

            Assert.HasCount(1, environment.Signals);
            // Before the fix the failed speedpost keeps index 1, even though
            // merging the two signal heads leaves only Signals[0]. InsertNode
            // then throws the same List.get_Item exception reported on USA2.
            TrackCircuitSection circuit = InsertItem(environment, unplaced);

            Assert.AreEqual(-1, environment.TrackItemSignalIndex[unplaced.TrackItemIndex]);
            Assert.HasCount(0, circuit.CircuitItems.TrackCircuitSpeedPosts[TrackDirection.Ahead]);
            Assert.HasCount(0, circuit.CircuitItems.TrackCircuitSpeedPosts[TrackDirection.Reverse]);
            Assert.AreEqual(0, environment.TrackItemSignalIndex[first.TrackItemIndex]);
            Assert.AreEqual(0, environment.TrackItemSignalIndex[second.TrackItemIndex]);
            foreach (SignalHead head in environment.Signals[0].SignalHeads)
                Assert.AreSame(environment.Signals[0], head.MainSignal);
        }

        [TestMethod]
        public void UnplacedSpeedpostDoesNotReusePreviousValidSpeedRestriction()
        {
            SpeedpostTrackItem valid = SpeedItem(0, 0, 20);
            SpeedpostTrackItem unplaced = SpeedItem(1, 25, 40);
            SignalEnvironment environment = BuildEnvironment(false, valid, unplaced);
            TrackCircuitSection circuit = InsertItem(environment, unplaced);

            Assert.HasCount(1, environment.Signals);
            Assert.AreEqual(0, environment.TrackItemSignalIndex[valid.TrackItemIndex]);
            Assert.AreEqual(-1, environment.TrackItemSignalIndex[unplaced.TrackItemIndex]);
            Assert.HasCount(0, circuit.CircuitItems.TrackCircuitSpeedPosts[TrackDirection.Ahead]);
            Assert.HasCount(0, circuit.CircuitItems.TrackCircuitSpeedPosts[TrackDirection.Reverse]);
        }

        [TestMethod]
        public void ValidSpeedpostStillMapsToItsOwnCircuitItemAfterHeadMerge()
        {
            SignalTrackItem first = SignalItem(0, 20);
            SignalTrackItem second = SignalItem(1, 20);
            SpeedpostTrackItem valid = SpeedItem(2, 0, 40);
            SignalEnvironment environment = BuildEnvironment(true, first, second, valid);
            TrackCircuitSection circuit = InsertItem(environment, valid);
            Signal speedpost = environment.Signals[environment.TrackItemSignalIndex[valid.TrackItemIndex]];
            TrackCircuitSignalList posts = circuit.CircuitItems.TrackCircuitSpeedPosts[speedpost.TrackDirection.Reverse()];

            Assert.HasCount(2, environment.Signals);
            Assert.AreEqual(SignalCategory.SpeedPost, speedpost.SignalType);
            Assert.AreEqual(valid.TrackItemIndex, speedpost.SignalHeads[0].TDBIndex);
            Assert.HasCount(1, posts);
            Assert.AreSame(speedpost, posts[0].Signal);
            Assert.AreEqual(40, posts[0].SignalLocation, 0.001);
        }

        [TestMethod]
        public void UnplacedSignalRetainsInvalidIndexAfterHeadMerge()
        {
            SignalTrackItem first = SignalItem(0, 20);
            SignalTrackItem second = SignalItem(1, 20);
            SignalTrackItem unplaced = new SignalTrackItem(new WorldLocation(1, 0, 25, 0, 40))
            {
                TrackItemIndex = 2,
                Direction = TrackDirection.Ahead,
            };
            SignalEnvironment environment = BuildEnvironment(true, first, second, unplaced);
            InsertItem(environment, unplaced);

            Assert.HasCount(1, environment.Signals);
            Assert.AreEqual(-1, environment.TrackItemSignalIndex[unplaced.TrackItemIndex]);
        }

        private static SignalTrackItem SignalItem(int index, float distance) =>
            new SignalTrackItem(new WorldLocation(1, 0, 0, 0, distance))
            {
                TrackItemIndex = index,
                Direction = TrackDirection.Ahead,
            };

        private static SpeedpostTrackItem SpeedItem(int index, float lateralOffset, float distance) =>
            new SpeedpostTrackItem(new WorldLocation(1, 0, lateralOffset, 0, distance))
            {
                TrackItemIndex = index,
                SpeedValue = 40,
                SpeedpostType = SpeedpostType.Passenger | SpeedpostType.Metric,
            };

        private static SignalEnvironment BuildEnvironment(bool mergeHeads, params TrackItemBase[] items)
        {
            SignalTypeRegistry registry = SignalTypeRegistry.Initialize();
            WorldLocation start = new WorldLocation(1, 0, 0, 0, 0);
            WorldLocation end = new WorldLocation(1, 0, 0, 0, 100);
            VectorSectionNode section = new VectorSectionNode(start, start.Tile, Vector3.Zero, end) { NodeIndex = 1 };
            VectorNode node = new VectorNode(start, start.Tile, end)
            {
                NodeIndex = 1,
                VectorSections = ImmutableArray.Create(section),
            };
            ImmutableArray<int>.Builder references = ImmutableArray.CreateBuilder<int>();
            foreach (TrackItemBase item in items)
                references.Add(item.TrackItemIndex);
            TrackDatabase database = new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, node),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), new TrackNodeConnectorIndex()),
                TrackItems = ImmutableArray.Create(items),
                TrackItemSelectors = ImmutableDictionary<int, TrackItemIndex>.Empty.Add(1,
                    new TrackItemIndex { TrackItems = references.ToImmutable() }),
            };
            Invoke(database, "OnSerializing");
            Invoke(database, "OnSerialized");
            TrackWorld.Initialize(null, new TrackModel { TrackDatabase = database }, new TrackSectionModel
            {
                TrackSections = ImmutableDictionary<int, TrackSection>.Empty.Add(1,
                    new TrackSection { SectionIndex = 1, Gauge = 1.435f, Length = 100 }),
            });

            // Exercise the actual scanner, merge and circuit insertion without
            // requiring a complete simulator, route installation or graphics device.
            SignalEnvironment environment = (SignalEnvironment)RuntimeHelpers.GetUninitializedObject(typeof(SignalEnvironment));
            SetField(environment, "trackDatabase", database);
            SetField(environment, "<TrackItemSignalIndex>k__BackingField", new Dictionary<int, int>());
            SetField(environment, "<OrtsSignalTypeCount>k__BackingField", registry.FunctionCount);
            ConcurrentBag<SignalWorldInfo> worldSignals = new ConcurrentBag<SignalWorldInfo>();
            if (mergeHeads)
            {
                SignalWorldInfo worldSignal = (SignalWorldInfo)RuntimeHelpers.GetUninitializedObject(typeof(SignalWorldInfo));
                SetField(worldSignal, "<HeadReference>k__BackingField", new Dictionary<int, int> { [0] = 0, [1] = 1 });
                worldSignals.Add(worldSignal);
            }
            Invoke(environment, "BuildSignalList", new Dictionary<int, int>(), worldSignals);
            return environment;
        }

        private static TrackCircuitSection InsertItem(SignalEnvironment environment, TrackItemBase item)
        {
            TrackCircuitSection circuit = new TrackCircuitSection(1)
            {
                CircuitItems = new TrackCircuitItems(environment.OrtsSignalTypeCount),
            };
            typeof(TrackCircuitSection).GetProperty("Length").SetValue(circuit, 100f);
            TrackTraveller traveller = TrackTraveller.InitializeTraveller(new WorldLocation(1, 0, 0, 0, 0),
                TrackWorld.Instance.TrackDatabase.TrackNodes[1] as VectorNode).Value;
            Invoke(environment, "InsertNode", circuit, item, traveller, 1, new[] { -1f, -1f },
                new Dictionary<int, int>(), new Dictionary<int, CrossOverInfo>());
            return circuit;
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static object Invoke(object target, string name, params object[] arguments)
        {
            try
            {
                return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
