using Riel.Common.Api;

using MemoryPack;

namespace Riel.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class ControlTrailerSaveState : SaveStateBase
    {
        public ControllerSaveState GearboxControllerSaveState { get; set; }
        public int GearBoxIndication { get; set; }
        public int GearIndex { get; set; }
    }
}
