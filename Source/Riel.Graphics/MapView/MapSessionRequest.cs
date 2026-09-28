namespace Riel.Graphics.MapView
{
    public sealed record MapSessionRequest(
        ContentBase Content,
        IMapInsetHost InsetHost,
        IMapTextureHelperHost TextureHelperHost);
}
