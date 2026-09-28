using System;
using System.Threading;
using System.Threading.Tasks;

using Riel.Models.Content;
using Riel.Models.Signalling;

namespace Riel.Models.Handler
{
    /// <summary>
    /// Handler for the <see cref="SignalConfigurationModel"/>, which contains the pre-built
    /// signal type and function definitions for a route (derived from the legacy MSTS
    /// <c>sigcfg.dat</c> file).
    /// </summary>
    internal class SignalConfigurationModelHandler : ContentHandlerBase<SignalConfigurationModel>
    {
        public static Task<SignalConfigurationModel> GetCore(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Id;

            if (!modelTaskCache.TryGetValue(key, out Task<SignalConfigurationModel> modelTask) || modelTask.IsFaulted)
            {
                modelTaskCache[key] = modelTask = FromFile(key, routeModel, cancellationToken);
            }

            return modelTask;
        }
    }
}
