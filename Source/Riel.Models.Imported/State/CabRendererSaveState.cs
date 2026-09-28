using System.Collections.ObjectModel;

using Riel.Common.Api;

using MemoryPack;

namespace Riel.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class CabRendererSaveState : SaveStateBase
    {
        public Collection<string> ActiveScreens { get; private set; } = new Collection<string>();
    }
}
