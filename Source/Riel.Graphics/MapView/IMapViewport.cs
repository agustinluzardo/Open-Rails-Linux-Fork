using Riel.Common.Position;
using Riel.Graphics.MapView.Widgets;

namespace Riel.Graphics.MapView
{
    public interface IMapViewport
    {
        bool InsideScreenArea(PointPrimitive pointPrimitive);

        bool InsideScreenArea(VectorPrimitive vectorPrimitive);

        void SetTrackingPosition(in WorldLocation location);

        void SetTrackingPosition(in PointD location);

        void UpdateScaleToFit(in PointD topLeft, in PointD bottomRight);
    }
}
