using System.Collections.Generic;
using System.Linq;

using Riel.Graphics.DrawableComponents;
using Riel.Graphics.MapView.Widgets;
using Riel.Runtime.Track;

using Microsoft.Xna.Framework;

namespace Riel.Graphics.MapView
{
    public sealed class XnaMapInsetHost : IMapInsetHost
    {
        private readonly InsetComponent insetComponent;

        public XnaMapInsetHost(InsetComponent insetComponent)
        {
            this.insetComponent = insetComponent;
        }

        public void UpdateColor(Color color)
        {
            insetComponent?.UpdateColor(color);
        }

        public void SetTrackSegments(IEnumerable<TrackSegmentBase> trackSegments)
        {
            insetComponent?.SetTrackSegments(trackSegments?.OfType<TrackSegment>());
        }
    }
}
