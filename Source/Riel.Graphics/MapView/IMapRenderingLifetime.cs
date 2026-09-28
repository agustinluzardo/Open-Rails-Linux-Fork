using Riel.Graphics.DrawableComponents;
using Riel.Graphics.MapView.Shapes;
using Riel.Graphics.Xna;

using Microsoft.Xna.Framework.Graphics;

namespace Riel.Graphics.MapView
{
    internal interface IMapRenderingLifetime
    {
        BasicShapes GetBasicShapes();

        TextShape GetTextShape(SpriteBatch spriteBatch);

        TextTextureRenderer GetTextTextureRenderer();
    }
}
