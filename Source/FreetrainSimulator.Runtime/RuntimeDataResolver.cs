using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Models.Signalling;
using FreeTrainSimulator.Models.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Runtime
{
    public class RuntimeDataResolver
    {
        public RouteModel RouteData { get; }
        public TrackSectionModel TrackSections { get; }
        public SignalConfigurationModel SignalConfiguration { get; }
        public Track.TrackWorld TrackWorld { get; }
        public bool MetricUnits { get; }
        public IRuntimeReferenceResolver RuntimeReferenceResolver { get; }

        public static RuntimeDataResolver Instance => GameService<RuntimeDataResolver>.Instance;

        public static RuntimeDataResolver GameInstance(Game game) => GameService<RuntimeDataResolver>.Get(game);

        public static async Task Initialize(RouteModel route, bool metricUnits, IRuntimeReferenceResolver runtimeReferenceResolver, CancellationToken cancellationToken)
        {
            TrackSectionModel trackSectionModel = await route.GetTrackSectionModel(cancellationToken).ConfigureAwait(false);
            TrackModel trackModel = await route.GetTrackModel(cancellationToken).ConfigureAwait(false);
            SignalConfigurationModel signalConfigurationModel = await route.GetSignalConfigurationModel(cancellationToken).ConfigureAwait(false);

            string routeName = string.IsNullOrEmpty(route?.Name) ? route?.Id : route.Name;
            if (trackSectionModel == null)
                throw new RouteDataUnavailableException(
                    $"The track sections of route '{routeName}' could not be loaded. They come from GLOBAL/tsection.dat, and from the route's own tsection.dat where it has one; " +
                    "one of them is missing or could not be read. Scanning the content again lists the file and the reason.");
            if (trackModel == null)
                throw new RouteDataUnavailableException(
                    $"The track database of route '{routeName}' could not be loaded: its .tdb file is missing or could not be read, or uses track pieces the installed tsection.dat lacks. " +
                    "Scanning the content again lists the file and the reason.");

            Track.TrackWorld trackWorld = Track.TrackWorld.Initialize(null, trackModel, trackSectionModel);

            _ = GameService<RuntimeDataResolver>.Set(null, new RuntimeDataResolver(route, trackSectionModel, signalConfigurationModel, trackWorld, metricUnits, runtimeReferenceResolver));
        }

        protected RuntimeDataResolver(RouteModel route, TrackSectionModel trackSectionModel, SignalConfigurationModel signalConfiguration,
            Track.TrackWorld trackWorld, bool useMetricUnits, IRuntimeReferenceResolver runtimeReferenceResolver)
        {
            RouteData = route;
            TrackSections = trackSectionModel;
            SignalConfiguration = signalConfiguration;
            TrackWorld = trackWorld;
            MetricUnits = useMetricUnits;
            RuntimeReferenceResolver = runtimeReferenceResolver;
        }

        protected RuntimeDataResolver()
        { }
    }
}
