using Riel.Common.DebugInfo;
using Riel.Runtime.Track;

namespace Riel.Graphics.MapView
{
    public interface ITrackNodeInfoContext
    {
        INameValueInformationProvider TrackNodeInfo { get; }

        IMapViewport Viewport { get; }

        IMapHostControl HostControl { get; }

        ToolboxContent Content { get; }

        TrackWorld TrackWorld { get; }
    }
}
