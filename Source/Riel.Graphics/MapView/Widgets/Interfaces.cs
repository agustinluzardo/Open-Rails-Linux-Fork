using Riel.Common.Position;

namespace Riel.Graphics.MapView.Widgets
{
    internal interface IDrawable<T> where T : PointPrimitive
    {
        void Draw(IMapRenderer renderer, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1);
    }
}
