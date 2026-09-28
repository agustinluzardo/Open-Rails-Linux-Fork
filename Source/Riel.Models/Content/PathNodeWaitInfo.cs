using Riel.Common;
using Riel.Common.Position;

using MemoryPack;

namespace Riel.Models.Content
{
    /// <summary>
    /// Specifies waiting/stopping information at a path node, typically used for
    /// timetable-driven station stops.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record PathNodeWaitInfo
    {
        /// <summary>Duration to wait at this node in seconds.</summary>
        public int WaitTime { get; init; }
    }
}
