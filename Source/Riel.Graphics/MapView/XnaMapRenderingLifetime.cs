using Riel.Graphics.DrawableComponents;
using Riel.Graphics.MapView.Shapes;
using Riel.Graphics.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Riel.Graphics.MapView
{
    internal sealed class XnaMapRenderingLifetime : IMapRenderingLifetime
    {
        private readonly Game game;
        private readonly TextTextureRenderer textTextureRenderer;
        private readonly BasicShapes basicShapes;

        public XnaMapRenderingLifetime(Game game)
        {
            this.game = game;
            textTextureRenderer = TextTextureRenderer.Create(game);
            basicShapes = BasicShapes.Create(game.GraphicsDevice);
        }

        public BasicShapes GetBasicShapes()
        {
            return basicShapes;
        }

        public TextShape GetTextShape(SpriteBatch spriteBatch)
        {
            return TextShape.Instance(game, spriteBatch);
        }

        public TextTextureRenderer GetTextTextureRenderer()
        {
            return textTextureRenderer;
        }
    }
}
