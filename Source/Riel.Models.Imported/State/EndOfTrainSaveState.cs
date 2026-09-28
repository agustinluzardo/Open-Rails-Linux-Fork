using Riel.Common;
using Riel.Common.Api;

using MemoryPack;

namespace Riel.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class EndOfTrainSaveState : SaveStateBase
    {
        public int DeviceId { get; set; }
        public EndOfTrainState EndOfTrainState { get; set; }
    }
}
