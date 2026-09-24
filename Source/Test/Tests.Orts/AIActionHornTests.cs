using System;
using System.Reflection;
using System.Runtime.CompilerServices;

using FreeTrainSimulator.Common.Position;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Orts.Simulation.AIs;

namespace Tests.Orts
{
    [TestClass]
    public class AIActionHornTests
    {
        [TestMethod]
        public void GenericLevelCrossingHornAcceptsIntTrackIndex()
        {
            var horn = new AIActionHornRef();
            var train = (AITrain)RuntimeHelpers.GetUninitializedObject(typeof(AITrain));

            MethodInfo method = typeof(AIActionHornRef).GetMethod(
                "CheckGenActions",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("AIActionHornRef.CheckGenActions was not found.");

            // LevelCrossingItem.TrackIndex is Int32 in the migrated track model.
            // Keep the activation condition false so this regression test isolates
            // argument handling and does not need a fully initialized simulator.
            object[] arguments =
            {
                default(WorldLocation),
                train,
                new object[] { 1f, -10f, 123, null },
            };

            object result = method.Invoke(horn, arguments);

            Assert.IsNull(result);
        }
    }
}
