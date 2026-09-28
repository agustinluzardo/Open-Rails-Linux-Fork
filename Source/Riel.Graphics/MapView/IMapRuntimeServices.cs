using Riel.Runtime;
using Riel.Runtime.Track;

namespace Riel.Graphics.MapView
{
    internal interface IMapRuntimeServices
    {
        RuntimeDataResolver RuntimeData { get; }

        TrackWorld TrackWorld { get; }

        string RouteName { get; }

        bool UseMetricUnits { get; }
    }
}
