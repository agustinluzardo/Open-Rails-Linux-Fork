using System.Collections.Generic;

using Riel.Runtime.Track;

using Microsoft.Xna.Framework;

namespace Riel.Graphics.MapView
{
    public interface IMapInsetHost
    {
        void UpdateColor(Color color);

        void SetTrackSegments(IEnumerable<TrackSegmentBase> trackSegments);
    }
}
