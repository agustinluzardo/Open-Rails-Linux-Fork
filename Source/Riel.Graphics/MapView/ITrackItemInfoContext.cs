using Riel.Common.DebugInfo;
using Riel.Runtime.Track;

namespace Riel.Graphics.MapView
{
    public interface ITrackItemInfoContext
    {
        INameValueInformationProvider TrackItemInfo { get; }

        IMapViewport Viewport { get; }

        TrackWorld TrackWorld { get; }
    }
}
