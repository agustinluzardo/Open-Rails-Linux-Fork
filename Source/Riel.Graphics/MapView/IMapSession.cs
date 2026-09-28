using System;

namespace Riel.Graphics.MapView
{
    public interface IMapSession : IDisposable
    {
        IMapRenderer Renderer { get; }

        IMapViewport Viewport { get; }

        IMapHostControl HostControl { get; }
    }
}
