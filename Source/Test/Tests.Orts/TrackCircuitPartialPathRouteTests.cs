using FreeTrainSimulator.Common;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Orts.Simulation.Track;

namespace Tests.Orts
{
    [TestClass]
    public class TrackCircuitPartialPathRouteTests
    {
        [TestMethod]
        public void BackwardLookupReturnsNearestPreviousOccurrence()
        {
            TrackCircuitPartialPathRoute route = BuildRoute(10, 20, 10, 30, 10);

            Assert.AreEqual(2, route.GetRouteIndexBackward(10, 4));
            Assert.AreEqual(4, route.GetRouteIndexBackward(10, route.Count));
            Assert.AreEqual(0, route.GetRouteIndexBackward(10, 1));
            Assert.AreEqual(-1, route.GetRouteIndexBackward(20, 1));
        }

        [TestMethod]
        public void ForwardLookupStillReturnsFirstOccurrenceAtOrAfterStart()
        {
            TrackCircuitPartialPathRoute route = BuildRoute(10, 20, 10, 30, 10);

            Assert.AreEqual(0, route.GetRouteIndex(10, 0));
            Assert.AreEqual(2, route.GetRouteIndex(10, 1));
            Assert.AreEqual(4, route.GetRouteIndex(10, 3));
        }

        private static TrackCircuitPartialPathRoute BuildRoute(params int[] sectionIndices)
        {
            TrackCircuitPartialPathRoute route = new TrackCircuitPartialPathRoute();
            int lastSectionIndex = -1;

            foreach (int sectionIndex in sectionIndices)
            {
                TrackCircuitSection section = new TrackCircuitSection(sectionIndex);
                route.Add(new TrackCircuitRouteElement(section, TrackDirection.Ahead, lastSectionIndex));
                lastSectionIndex = sectionIndex;
            }

            return route;
        }
    }
}
