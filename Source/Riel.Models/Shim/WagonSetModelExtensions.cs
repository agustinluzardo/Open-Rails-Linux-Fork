using System.Collections.Immutable;

using Riel.Models.Content;
using Riel.Models.Handler;

namespace Riel.Models.Shim
{
    /// <summary>
    /// Extension methods for wagon-set model collections, providing a sentinel "Any Locomotive"
    /// reference for UI selection lists.
    /// </summary>
    public static class WagonSetModelExtensions
    {
        public static WagonReferenceModel Any(this ImmutableArray<WagonSetModel> _) => WagonReferenceHandler.LocomotiveAny;
    }
}
