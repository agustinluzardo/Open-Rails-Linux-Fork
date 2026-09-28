using Riel.Graphics.Xna;

namespace Riel.Graphics.MapView
{
    public interface IMapTextureHelperHost
    {
        void Enable(IMapBaseOverlayContext overlayContext);

        void Disable();
    }
}
