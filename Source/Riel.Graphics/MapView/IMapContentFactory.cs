using Riel.Common.Input;

using Microsoft.Xna.Framework;

namespace Riel.Graphics.MapView
{
    public interface IMapContentFactory
    {
        ToolboxContent CreateToolboxContent(Game game, MouseInputGameComponent mouseInputGameComponent, IMapInsetHost insetHost = null, IMapTextureHelperHost textureHelperHost = null);

        DispatcherContent CreateDispatcherContent(Game game, MouseInputGameComponent mouseInputGameComponent, IMapInsetHost insetHost = null, IMapTextureHelperHost textureHelperHost = null);
    }
}
