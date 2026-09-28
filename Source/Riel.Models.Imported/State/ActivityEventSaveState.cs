using Riel.Common.Api;

using MemoryPack;

namespace Riel.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class ActivityEventSaveState : SaveStateBase
    {
        public int TimesTriggered { get; set; }
        public bool Enabled { get; set; }
        public int ActivationLevel { get; set; }
    }
}
