using System.Collections.Immutable;
using System.Threading.Tasks;

using Riel.Models.Content;
using Riel.Runtime.Track;

namespace Riel.Toolbox.PathEditing
{
    internal interface ITrainPathToolingContext
    {
        bool UseMetricUnits { get; }

        TrackWorld TrackWorld { get; }

        Task<ImmutableArray<PathModelHeader>> GetPaths();

        /// <summary>
        /// Forces revalidation of every path for the current route against the current track, persisting the
        /// updated validity flags, and returns the refreshed path headers.
        /// </summary>
        Task<ImmutableArray<PathModelHeader>> ValidateAllPaths();
    }
}
