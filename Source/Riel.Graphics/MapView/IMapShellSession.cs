using Microsoft.Xna.Framework;

namespace Riel.Graphics.MapView
{
    public interface IMapShellSession : IMapSession
    {
        IMapShellHost ShellHost { get; }
    }
}
