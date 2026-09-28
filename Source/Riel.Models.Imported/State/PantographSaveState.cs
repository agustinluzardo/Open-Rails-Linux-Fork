using Riel.Common;
using Riel.Common.Api;

using MemoryPack;

namespace Riel.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class PantographSaveState : SaveStateBase
    {
        public PantographState PantographState { get; set; }
        public double Time { get; set; }
        public double Delay { get; set; }
    }
}
