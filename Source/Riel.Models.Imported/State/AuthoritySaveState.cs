using Riel.Common;
using Riel.Common.Api;

using MemoryPack;

namespace Riel.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class AuthoritySaveState : SaveStateBase
    {
        public EndAuthorityType EndAuthorityType { get; set; }
        public int LastReservedSection { get; set; }
        public float Distance { get; set; }
    }
}
