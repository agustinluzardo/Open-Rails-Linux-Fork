using Riel.Graphics.Xna;

using Microsoft.Xna.Framework.Graphics;

namespace Riel.Graphics.MapView
{
    internal interface IMapTextCache
    {
        Texture2D GetTextTexture(string message, System.Drawing.Font font, OutlineRenderOptions outlineRenderOptions = null);
    }
}
