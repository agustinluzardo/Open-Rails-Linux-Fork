using Microsoft.Xna.Framework;

namespace Riel.Graphics.MapView
{
    internal sealed class XnaMapShellHost : IXnaMapShellHost
    {
        private readonly ContentArea contentArea;

        public IMapHostControl HostControl => contentArea;

        public DrawableGameComponent Component => contentArea;

        public XnaMapShellHost(ContentArea contentArea)
        {
            this.contentArea = contentArea;
        }
    }
}
