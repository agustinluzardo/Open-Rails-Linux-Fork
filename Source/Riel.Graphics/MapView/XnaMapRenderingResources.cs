using Riel.Graphics.DrawableComponents;
using Riel.Graphics.MapView.Shapes;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Riel.Graphics.MapView
{
    internal sealed class XnaMapRenderingResources : IMapRenderingResources
    {
        public SpriteBatch SpriteBatch { get; }

        public BasicShapes BasicShapes { get; }

        public TextShape TextShape { get; }

        public XnaMapRenderingResources(IMapRenderingLifetime renderingLifetime, SpriteBatch spriteBatch)
        {
            SpriteBatch = spriteBatch;
            BasicShapes = renderingLifetime.GetBasicShapes();
            TextShape = renderingLifetime.GetTextShape(spriteBatch);
        }
    }
}
