using System;
using System.Threading;
using System.Threading.Tasks;

using Riel.Models.Content;
using Riel.Models.Handler;
using Riel.Models.Track;

namespace Riel.Models.Shim
{
    /// <summary>
    /// Extension methods for loading the <see cref="TrackSectionModel"/> (track sections, shapes,
    /// and shape-path definitions derived from the legacy MSTS <c>tsection.dat</c>) for a given route.
    /// </summary>
    public static class TrackSectionModelExtensions
    {
        public static async ValueTask<TrackSectionModel> Get(this RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            return await TrackSectionsModelHandler.GetCore(routeModel, cancellationToken).ConfigureAwait(false);
        }
    }
}
