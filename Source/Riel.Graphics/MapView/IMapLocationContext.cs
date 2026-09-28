using Riel.Common.Position;

namespace Riel.Graphics.MapView
{
    public interface IMapLocationContext
    {
        PointD WorldPosition { get; }
    }
}
