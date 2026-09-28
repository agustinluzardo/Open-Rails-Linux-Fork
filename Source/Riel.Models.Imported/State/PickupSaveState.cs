using Riel.Common.Api;

using MemoryPack;

namespace Riel.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class PickupSaveState : SaveStateBase
    {
        public bool PickupStaticConsist { get; set; }
        public int TrainNumber { get; set; }
        public string TrainName { get; set; }
        public int StationPlatformReference { get; set; }
        public bool Valid { get; set; }

    }
}
