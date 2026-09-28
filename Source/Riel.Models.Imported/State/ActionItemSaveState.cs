using Riel.Common;
using Riel.Common.Api;

using MemoryPack;

namespace Riel.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class ActionItemSaveState : SaveStateBase
    {
        public ActionItemType ActionItemType { get; set; }
        public float Distance { get; set; }
        public float MaxSpeedLimit { get; set; }
        public float MaxSpeedSignal { get; set; }
        public float MaxTempSpeedLimit { get; set; }
        public int TrackSectionIndex { get; set; }
        public float RequiredSpeed { get; set; }
        public float ActivateDistance { get; set; }
        public float InsertedDistance { get; set; }
        public int RequestedTablePath { get; set; }
        public AiActionType NextActionType { get; set; }
        public float OriginalMaxTrainSpeed { get; set; }
        public SignalItemSaveState SignalItemSaveState { get; set; }
    }
}
