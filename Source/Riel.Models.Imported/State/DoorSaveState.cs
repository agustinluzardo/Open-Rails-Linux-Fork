using Riel.Common;
using Riel.Common.Api;

using MemoryPack;

namespace Riel.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class DoorSaveState : SaveStateBase
    {
        public DoorState DoorState { get; set; }
        public bool Locked { get; set; }
    }
}
