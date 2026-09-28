using System.Collections.ObjectModel;

using Riel.Common.Api;

using MemoryPack;

namespace Riel.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class AiPathSaveState : SaveStateBase
    {
#pragma warning disable CA2227 // Collection properties should be read only
        public Collection<AiPathNodeSaveState> AiPathNodeSaveStates { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
        public string ExpectedPath { get; set; }
    }
}
