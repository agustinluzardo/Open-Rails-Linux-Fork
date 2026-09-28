using Riel.Common;
using Riel.Common.Api;

using MemoryPack;

namespace Riel.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class TrackCircuitPositionSaveState : SaveStateBase
    {
        public int TrackCircuitSectionIndex { get; set; }
        public TrackDirection Direction { get; set; }
        public float Offset { get; set; }
        public int RouteListIndex { get; set; }
        public int TrackNodeIndex { get; set; }
        public float DistanceTravelled { get; set; }
    }
}
