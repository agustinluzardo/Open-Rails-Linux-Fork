using Riel.Common;
using Riel.Common.Api;

using MemoryPack;

namespace Riel.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class TriggerActivationSaveState : SaveStateBase
    {
        public int ActivatedTrain { get; set; }
        public TriggerActivationType ActivationType { get; set; }
        public int PlatformId { get; set; }
        public string ActivatedTrainName { get; set; }
    }
}
