using System;
using System.Collections.Generic;

using Riel.Common.Position;

namespace Riel.Runtime.Track
{
    public record TrackSegmentSection : TrackSegmentSectionBase<TrackSegmentBase>
    {
        public TrackSegmentSection(int trackNodeIndex, IEnumerable<TrackSegmentBase> trackSegments) :
            base(trackNodeIndex, trackSegments)
        {
        }

        protected override TrackSegmentBase CreateItem(in PointD start, in PointD end)
        {
            throw new NotImplementedException();
        }

        protected override TrackSegmentBase CreateItem(TrackSegmentBase source)
        {
            throw new NotImplementedException();
        }

        protected override TrackSegmentBase CreateItem(TrackSegmentBase source, in PointD start, in PointD end)
        {
            throw new NotImplementedException();
        }
    }
}
