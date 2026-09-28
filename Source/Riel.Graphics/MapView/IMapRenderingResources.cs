using Riel.Graphics.DrawableComponents;
using Riel.Graphics.MapView.Shapes;

using Microsoft.Xna.Framework.Graphics;

namespace Riel.Graphics.MapView
{
    internal interface IMapRenderingResources
    {
        SpriteBatch SpriteBatch { get; }

        BasicShapes BasicShapes { get; }

        TextShape TextShape { get; }
    }
}
