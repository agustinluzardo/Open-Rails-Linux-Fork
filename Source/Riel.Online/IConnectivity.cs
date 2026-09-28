using MagicOnion;

namespace Riel.Online
{
    public interface IConnectivity: IService<IConnectivity>
    {
        UnaryResult<long> Connect();
    }
}
