using Microsoft.Xna.Framework;

namespace Riel.Graphics.MapView
{
    public interface IXnaMapShellHost : IMapShellHost
    {
        DrawableGameComponent Component { get; }
    }
}
