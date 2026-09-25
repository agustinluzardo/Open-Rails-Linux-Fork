// COPYRIGHT 2020 by the Open Rails project.
//
// This file is part of Open Rails.
//
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.
//
// ===========================================================================================
//      Open Rails Web Server
//      Based on an idea by Dan Reynolds (HighAspect) - 2017-12-21
// ===========================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;

using Microsoft.Xna.Framework;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using Orts.ActivityRunner.Viewer3D.RollingStock;
using FreeTrainSimulator.Runtime;
using Orts.Simulation;
using Orts.Simulation.Commanding;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS;
using Orts.Simulation.RollingStocks.SubSystems.PowerSupplies;

namespace Orts.ActivityRunner.Viewer3D.WebServices
{
    /// <summary>
    /// A static class that contains server creation and helper methods for the
    /// Open Rails web server.
    /// </summary>
    public static class WebServer
    {
        /// <summary>
        /// Create a web server with a single listening address.
        /// </summary>
        /// <param name="url">The URL prefix to listen on.</param>
        /// <param name="path">The root directory to serve static files from.</param>
        /// <returns>The EmbedIO web server instance.</returns>
        public static EmbedIO.WebServer CreateWebServer(string url, string path)
        {
            return CreateWebServer(new string[] { url }, path);
        }

        /// <summary>
        /// Create a web server with multiple listening addresses.
        /// </summary>
        /// <param name="urls">A list of URL prefixes to listen on.</param>
        /// <param name="path">The root directory to serve static files from.</param>
        /// <returns>The EmbedIO web server instance.</returns>
        public static EmbedIO.WebServer CreateWebServer(IEnumerable<string> urls, string path)
        {
            // Viewer is not yet initialized in the GameState object - wait until it is
            while (Program.Viewer == null)
                Thread.Sleep(1000);

            return new EmbedIO.WebServer(o => o
                    .WithUrlPrefixes(urls))
                .WithWebApi("/API", SerializationCallback, m => m
                    .WithController(() => new OrtsApiController(Program.Viewer)))
                .WithStaticFolder("/", path, true);
        }

        /// <remarks>
        /// The Swan serializer used by EmbedIO does not serialize custom classes,
        /// so this callback replaces it with the Newtonsoft serializer.
        /// </remarks>
        private static async Task SerializationCallback(IHttpContext context, object data)
        {
            using (TextWriter text = context.OpenResponseText(new UTF8Encoding()))
            {
                await text.WriteAsync(JsonConvert.SerializeObject(data, new JsonSerializerSettings()
                {
                    Formatting = Formatting.Indented,
                    ContractResolver = new XnaFriendlyResolver()
                })).ConfigureAwait(false);
            }
        }

        internal static async Task<T> DeserializationCallback<T>(IHttpContext context)
        {
            using (TextReader text = context.OpenRequestText())
                return JsonConvert.DeserializeObject<T>(await text.ReadToEndAsync().ConfigureAwait(false));
        }

        /// <summary>
        /// This contract resolver fixes JSON serialization for certain XNA classes.
        /// </summary>
        /// <remarks>
        /// Many thanks to <a href="https://stackoverflow.com/a/44238343">Elliott Darfink of Stack Overflow</a>.
        /// </remarks>
        private sealed class XnaFriendlyResolver : DefaultContractResolver
        {
            protected override JsonContract CreateContract(Type objectType)
            {
                if (objectType == typeof(Rectangle) || objectType == typeof(Point))
                    return CreateObjectContract(objectType);
                return base.CreateContract(objectType);
            }
        }
    }

    /// <summary>
    /// An API controller that serves Open Rails data from an attached Viewer.
    /// </summary>
    internal sealed class OrtsApiController : WebApiController
    {
        /// <summary>
        /// The Viewer to serve train data from.
        /// </summary>
        private readonly Viewer viewer;

        public OrtsApiController(Viewer viewer)
        {
            this.viewer = viewer;
            WebServices.TrainDrivingDisplay.Initialize(viewer);
        }

        private static string GetPosition()
        {
            double latitude;
            double longitude;
            (latitude, longitude) = EarthCoordinates.ConvertWTC(Simulator.Instance.PlayerLocomotive.WorldPosition.WorldLocation);
            return FormattableString.Invariant($"{MathHelper.ToDegrees((float)latitude):F6} {MathHelper.ToDegrees((float)longitude):F6}");
        }

        /// <summary>
        /// Determine latitude/longitude position of the current TrainCar
        /// </summary>
        private static LatLonDirection GetLocomotiveLatLonDirection()
        {
            ref readonly WorldPosition worldPosition = ref Simulator.Instance.PlayerLocomotive.WorldPosition;

            double lat;
            double lon;
            (lat, lon) = EarthCoordinates.ConvertWTC(worldPosition.WorldLocation);

            LatLon latLon = new LatLon(
                MathHelper.ToDegrees((float)lat),
                MathHelper.ToDegrees((float)lon));

            float direction = (float)Math.Atan2(worldPosition.XNAMatrix.M13, worldPosition.XNAMatrix.M11);
            float directionDeg = MathHelper.ToDegrees((float)direction);

            if (Simulator.Instance.PlayerLocomotive.Direction == MidpointDirection.Reverse)
            {
                directionDeg += 180.0f;
            }
            if (Simulator.Instance.PlayerLocomotive.Flipped)
            {
                directionDeg += 180.0f;
            }
            if (Simulator.Instance.PlayerLocomotive.UsingRearCab)
            {
                directionDeg += 180.0f;
            }
            while (directionDeg > 360)
            {
                directionDeg -= 360;
            }

            return new LatLonDirection(latLon, directionDeg);
        }

        #region /API/APISAMPLE
        public struct Embedded
        {
            public string Str;
            public int Numb;
        }
        public struct ApiSampleData
        {
            public int intData;
            public string strData;
            public DateTime dateData;
            public Embedded embedded;
            public string[] strArrayData;
        }

        // Call from JavaScript is case-sensitive, with /API prefix, e.g:
        //   hr.open("GET", "/API/APISAMPLE", true);
        [Route(HttpVerbs.Get, "/APISAMPLE")]
        public ApiSampleData ApiSample() => new ApiSampleData()
        {
            intData = 576,
            strData = "Sample String",
            dateData = new DateTime(2018, 1, 1),
            embedded = new Embedded()
            {
                Str = "Embedded String",
                Numb = 123
            },
            strArrayData = new string[5]
            {
                "First member",
                "Second member",
                "Third member",
                "Fourth member",
                "Fifth member"
            }
        };
        #endregion


        #region /API/HUD
        public struct HudApiTable
        {
            public int nRows;
            public int nCols;
            public string[] values;
        }

        public struct HudApiArray
        {
            public int nTables;
            public HudApiTable commonTable;
            public HudApiTable extraTable;
        }

        [Route(HttpVerbs.Get, "/HUD/{pageNo}")]
        // Example URL where pageNo = 3:
        //   "http://localhost:2150/API/HUD/3" returns data in JSON
        // Call from JavaScript is case-sensitive, with /API prefix, e.g:
        //   hr.open("GET", "/API/HUD" + pageNo, true);
        // The name of this method is not significant.
        public HudApiArray ApiHUD(int pageNo)
        {
            var hudApiArray = new HudApiArray()
            {
                nTables = 1,
                commonTable = ApiHUD_ProcessTable(0)
            };

            if (pageNo > 0)
            {
                hudApiArray.nTables = 2;
                hudApiArray.extraTable = ApiHUD_ProcessTable(pageNo);
            }
            return hudApiArray;
        }

        private HudApiTable ApiHUD_ProcessTable(int pageNo)
        {
            return new HudApiTable()
            {
                nRows = 0,
                nCols = 0,
                values = null,
            };
        }
        #endregion


        #region /API/TRACKMONITORDISPLAY
        [Route(HttpVerbs.Get, "/TRACKMONITORDISPLAY")]
        public IEnumerable<TrackMonitorDisplay.ListLabel> TrackMonitorDisplayList() => viewer.TrackMonitorDisplayList();
        #endregion


        #region /API/TRAININFO
        [Route(HttpVerbs.Get, "/TRAININFO")]
        public TrainInfo TrainInfo() => viewer.GetWebTrainInfo();
        #endregion


        #region /API/TRAINDRIVINGDISPLAY
        [Route(HttpVerbs.Get, "/TRAINDRIVINGDISPLAY")]
        public IEnumerable<TrainDrivingDisplay.ListLabel> TrainDrivingDisplay([QueryField] bool normalText) => viewer.TrainDrivingDisplayList(normalText);
        #endregion

        #region /API/TRAINDPUDISPLAY
        [Route(HttpVerbs.Get, "/TRAINDPUDISPLAY")]
        public IEnumerable<TrainDpuDisplay.ListLabel> TrainDpuDisplay([QueryField] bool normalText) => viewer.TrainDpuDisplayList(normalText);
        #endregion

        // Note: to see the JSON, use "localhost:2150/API/CABCONTROLS" - Beware: case matters
        // Note: to run the webpage, use "localhost:2150/CabControls/index.html" - case doesn't matter
        // or use "localhost:2150/CabControls/"
        // Do not use "localhost:2150/CabControls/"
        // as that will return the webpage, but the path will be "/" not "/CabControls/ and the appropriate scripts will not be loaded.

        #region /API/CABCONTROLS
        [Route(HttpVerbs.Get, "/CABCONTROLS")]
        public IEnumerable<ControlValue> CabControls()
        {
            return ((MSTSLocomotiveViewer)viewer.PlayerLocomotiveViewer).GetWebControlValueList();
        }
        #endregion


        #region /API/ACTIVITYEVENTS
        public sealed class ActivityEventsData
        {
            public string Name { get; init; }
            public string Description { get; init; }
            public string Briefing { get; init; }
            public ActivityEventFeed.Entry[] Events { get; init; }
        }

        [Route(HttpVerbs.Get, "/ACTIVITYEVENTS")]
        public ActivityEventsData ActivityEvents()
        {
            var activity = viewer.Simulator.ActivityModel;
            return new ActivityEventsData
            {
                Name = activity?.Name ?? string.Empty,
                Description = activity?.Description ?? string.Empty,
                Briefing = activity?.Briefing ?? string.Empty,
                Events = viewer.ActivityEventFeed.Snapshot(),
            };
        }
        #endregion

        #region /API/TRAINCAROPERATIONS
        public sealed class TrainCarOperationInfo
        {
            public int Index { get; init; }
            public string CarId { get; init; }
            public string Kind { get; init; }
            public bool HandbrakeAvailable { get; init; }
            public bool HandbrakeOn { get; init; }
            public bool PowerAvailable { get; init; }
            public bool PowerOn { get; init; }
            public bool MuAvailable { get; init; }
            public bool MuConnected { get; init; }
            public bool BatteryAvailable { get; init; }
            public bool BatteryOn { get; init; }
            public bool EtsAvailable { get; init; }
            public bool EtsConnected { get; init; }
            public bool FrontBrakeHoseConnected { get; init; }
            public bool FrontAngleCockOpen { get; init; }
            public bool RearAngleCockOpen { get; init; }
            public bool BleedAvailable { get; init; }
            public bool BleedOpen { get; init; }
            public bool CanUncoupleAfter { get; init; }
        }

        public sealed class TrainCarOperationRequest
        {
            public int Index { get; set; }
            public string Action { get; set; }
        }

        [Route(HttpVerbs.Get, "/TRAINCAROPERATIONS")]
        public IEnumerable<TrainCarOperationInfo> TrainCarOperations()
        {
            Train train = viewer.PlayerTrain;
            if (train == null)
                return Array.Empty<TrainCarOperationInfo>();

            return train.Cars.Select((car, index) => DescribeCar(car, index, train.Cars.Count)).ToArray();
        }

        [Route(HttpVerbs.Post, "/TRAINCAROPERATIONS")]
        public async Task<IEnumerable<TrainCarOperationInfo>> SetTrainCarOperation()
        {
            TrainCarOperationRequest request = await HttpContext.GetRequestDataAsync<TrainCarOperationRequest>(
                WebServer.DeserializationCallback<TrainCarOperationRequest>).ConfigureAwait(false);

            Train train = viewer.PlayerTrain;
            if (train == null || request == null || request.Index < 0 || request.Index >= train.Cars.Count)
                return TrainCarOperations();

            TrainCar car = train.Cars[request.Index];
            MSTSWagon wagon = car as MSTSWagon;
            MSTSLocomotive locomotive = car as MSTSLocomotive;

            switch (request.Action)
            {
                case "handbrake" when wagon != null && wagon.HandBrakePresent:
                    _ = new WagonHandbrakeCommand(viewer.Log, wagon, car.BrakeSystem.HandbrakePercent == 0);
                    break;
                case "power" when locomotive != null:
                    _ = new PowerCommand(viewer.Log, locomotive, !locomotive.LocomotivePowerSupply.MainPowerSupplyOn);
                    break;
                case "mu" when locomotive != null:
                    _ = new ToggleMUCommand(viewer.Log, locomotive, locomotive.RemoteControlGroup == RemoteControlGroup.Unconnected);
                    break;
                case "battery" when wagon?.PowerSupply != null:
                    _ = new ToggleBatterySwitchCommand(viewer.Log, wagon, !wagon.PowerSupply.BatterySwitch.On);
                    break;
                case "ets" when wagon?.PowerSupply != null:
                    _ = new ConnectElectricTrainSupplyCableCommand(viewer.Log, wagon, !wagon.PowerSupply.FrontElectricTrainSupplyCableConnected);
                    break;
                case "hose" when wagon != null:
                    _ = new WagonBrakeHoseConnectCommand(viewer.Log, wagon, !wagon.BrakeSystem.FrontBrakeHoseConnected);
                    break;
                case "front-angle" when wagon != null:
                    _ = new ToggleAngleCockACommand(viewer.Log, wagon, !wagon.BrakeSystem.AngleCockAOpen);
                    break;
                case "rear-angle" when wagon != null:
                    _ = new ToggleAngleCockBCommand(viewer.Log, wagon, !wagon.BrakeSystem.AngleCockBOpen);
                    break;
                case "bleed" when wagon != null && car.BrakeSystem is SingleTransferPipe:
                    _ = new ToggleBleedOffValveCommand(viewer.Log, wagon, !wagon.BrakeSystem.BleedOffValveOpen);
                    break;
                case "uncouple" when request.Index < train.Cars.Count - 1 && !viewer.Simulator.TimetableMode:
                    _ = new UncoupleCommand(viewer.Log, request.Index);
                    break;
            }

            return TrainCarOperations();
        }

        private TrainCarOperationInfo DescribeCar(TrainCar car, int index, int count)
        {
            MSTSWagon wagon = car as MSTSWagon;
            MSTSLocomotive locomotive = car as MSTSLocomotive;
            return new TrainCarOperationInfo
            {
                Index = index,
                CarId = car.CarID,
                Kind = locomotive == null ? wagon?.WagonType.ToString() ?? car.GetType().Name : locomotive.EngineType.ToString(),
                HandbrakeAvailable = wagon?.HandBrakePresent == true,
                HandbrakeOn = car.BrakeSystem.HandbrakePercent > 0,
                PowerAvailable = locomotive is MSTSElectricLocomotive or MSTSDieselLocomotive,
                PowerOn = locomotive?.LocomotivePowerSupply?.MainPowerSupplyOn == true,
                MuAvailable = locomotive is MSTSElectricLocomotive or MSTSDieselLocomotive,
                MuConnected = locomotive != null && locomotive.RemoteControlGroup != RemoteControlGroup.Unconnected,
                BatteryAvailable = wagon?.PowerSupply != null,
                BatteryOn = wagon?.PowerSupply?.BatterySwitch.On == true,
                EtsAvailable = count > 1 && wagon?.PowerSupply != null,
                EtsConnected = wagon?.PowerSupply?.FrontElectricTrainSupplyCableConnected == true,
                FrontBrakeHoseConnected = car.BrakeSystem.FrontBrakeHoseConnected,
                FrontAngleCockOpen = car.BrakeSystem.AngleCockAOpen,
                RearAngleCockOpen = car.BrakeSystem.AngleCockBOpen,
                BleedAvailable = car.BrakeSystem is SingleTransferPipe,
                BleedOpen = wagon?.BrakeSystem.BleedOffValveOpen == true,
                CanUncoupleAfter = index < count - 1 && !viewer.Simulator.TimetableMode,
            };
        }
        #endregion

        #region /API/TIME
        [Route(HttpVerbs.Get, "/TIME")]
        public double Time()
        {
            return viewer.Simulator.ClockTime;
        }
        #endregion

        #region /API/MAP/INIT
        [Route(HttpVerbs.Get, "/MAP/INIT")]
        public InfoApiMap ApiMapInfo() => GetApiMapInfo(viewer);
        #endregion

        public static InfoApiMap GetApiMapInfo(Viewer viewer)
        {
            InfoApiMap infoApiMap = new InfoApiMap(viewer.PlayerLocomotive.PowerSupply as ILocomotivePowerSupply);
            infoApiMap.AddTrackNodesToPointsOnApiMap(RuntimeDataResolver.Instance.TrackWorld.TrackDatabase);
            infoApiMap.AddTrackItemsToPointsOnApiMap(RuntimeDataResolver.Instance.TrackWorld.TrackDatabase.TrackItems);
            return infoApiMap;
        }

        #region /API/MAP
        [Route(HttpVerbs.Get, "/MAP")]
        public LatLonDirection LatLonDirection() => GetLocomotiveLatLonDirection();
        #endregion
    }
}
