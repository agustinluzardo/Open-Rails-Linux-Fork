using System;

using Riel.Common;
using Riel.Common.Position;
using Riel.Models.Track;

using Microsoft.Xna.Framework;

namespace Riel.Runtime.Track
{
    public abstract record EndNodeBase : PointPrimitive, IIndexedElement
    {
        public float Direction { get; }
        public int TrackNodeIndex { get; }

#pragma warning disable CA1033 // Interface methods should be callable by child types
        int IIndexedElement.Index => TrackNodeIndex;
#pragma warning restore CA1033 // Interface methods should be callable by child types

        protected EndNodeBase(EndNode trackEndNode) : base(trackEndNode?.Location ?? throw new ArgumentNullException(nameof(trackEndNode)))
        {
            TrackNodeIndex = trackEndNode.NodeIndex;
            Direction = MathHelper.WrapAngle(trackEndNode.Direction.Y - MathHelper.PiOver2);
        }

        public bool EndNodeAt(in PointD location)
        {
            return location.DistanceSquared(Location) <= WorldLocation.ProximityTolerance;
        }

    }
}
