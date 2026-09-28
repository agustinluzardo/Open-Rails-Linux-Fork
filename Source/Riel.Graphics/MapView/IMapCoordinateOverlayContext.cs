using Riel.Common.Position;

using Microsoft.Xna.Framework;

namespace Riel.Graphics.MapView
{
    internal interface IMapCoordinateOverlayContext
    {
        PointD ScreenToWorldCoordinates(in Point screenLocation);
    }
}
