using System.Collections.Immutable;

using Riel.Common;
using Riel.Common.Position;
using Riel.Models.Content;
using Riel.Runtime.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Riel.Runtime.Track
{
    [TestClass]
    public class PathRouteResolutionCacheTests
    {
        [TestMethod]
        public void WhenSameModelInstanceIsResolvedTwiceThenTheCachedResolutionIsReturned()
        {
            PathRouteResolutionCache cache = new PathRouteResolutionCache();
            PathModel pathModel = CreatePath();

            PathRouteResolution first = cache.Resolve(pathModel, null);
            PathRouteResolution second = cache.Resolve(pathModel, null);

            Assert.AreSame(first, second);
        }

        [TestMethod]
        public void WhenModelInstanceChangesThenTheResolutionIsRecomputed()
        {
            PathRouteResolutionCache cache = new PathRouteResolutionCache();
            PathModel pathModel = CreatePath();

            PathRouteResolution first = cache.Resolve(pathModel, null);
            PathRouteResolution second = cache.Resolve(pathModel with { Name = "changed" }, null);

            Assert.AreNotSame(first, second);
        }

        [TestMethod]
        public void WhenCacheIsClearedThenTheResolutionIsRecomputed()
        {
            PathRouteResolutionCache cache = new PathRouteResolutionCache();
            PathModel pathModel = CreatePath();

            PathRouteResolution first = cache.Resolve(pathModel, null);
            cache.Clear();
            PathRouteResolution second = cache.Resolve(pathModel, null);

            Assert.AreNotSame(first, second);
        }

        [TestMethod]
        public void WhenModelIsNullThenResolveReturnsNull()
        {
            PathRouteResolutionCache cache = new PathRouteResolutionCache();

            Assert.IsNull(cache.Resolve(null, null));
        }

        private static PathModel CreatePath()
        {
            return new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    new PathNode(WorldLocation.None) { NodeType = PathNodeType.Start, NextMainNode = 1, NodeIndex = -1 },
                    new PathNode(WorldLocation.None) { NodeType = PathNodeType.End, NextMainNode = -1, NodeIndex = -1 }),
            };
        }
    }
}
