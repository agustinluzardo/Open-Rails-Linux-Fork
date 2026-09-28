using Microsoft.Xna.Framework;

namespace Riel.Graphics.MapView
{
    public interface IMapShellHost
    {
        IMapHostControl HostControl { get; }
    }
}
