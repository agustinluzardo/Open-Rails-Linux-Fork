namespace Riel.Online
{
    public interface IMultiplayerClient
    {
        void OnReceiveMessage(MultiplayerMessage message);
    }
}
