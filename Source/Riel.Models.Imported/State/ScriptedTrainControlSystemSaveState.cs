using System.Buffers;

using Riel.Common.Api;

using MemoryPack;

namespace Riel.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class ScriptedTrainControlSystemSaveState : SaveStateBase
    {
        public string ScriptName { get; set; }
        public ReadOnlySequence<byte> ScriptState { get; set; }
    }
}
