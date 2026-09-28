namespace Riel.Graphics.MapView
{
    public interface IMapSessionComposer
    {
        IMapSession Compose(MapSessionRequest request);
    }
}
