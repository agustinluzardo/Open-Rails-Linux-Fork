using System.Threading.Tasks;

using MagicOnion;

namespace Riel.Online
{
    public interface IMultiplayerHub : IStreamingHub<IMultiplayerHub, IMultiplayerClient>
    {
        ValueTask SendMessageAsync(MultiplayerMessage message);

        ValueTask JoinGameAsync(string userName, string route, string room);
    }
}
