using Riel.Common.Api;

using MemoryPack;

namespace Riel.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class CommandSwitchSaveState : SaveStateBase
    {
        public bool CommandSwitch { get; set; }
        public bool CommandButtonOn { get; set; }
        public bool CommandButtonOff { get; set; }
        public bool State { get; set; }
    }
}
