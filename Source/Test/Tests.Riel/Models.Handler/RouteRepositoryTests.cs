using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Riel.Common.Info;
using Riel.Models.Content;
using Riel.Models.Handler;
using Riel.Models.Imported.ImportHandler.OpenRails;
using Riel.Models.Imported.ImportHandler.TrainSimulator;
using Riel.Models.Imported.Shim;
using Riel.Models.Settings;
using Riel.Models.Shim;
using Riel.Models.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Riel.Models.Handler
{
    [TestClass]
    public class RouteRepositoryTests
    {
        //    [TestMethod]
        //    public async ValueTask SaveRoute()
        //    {
        //        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD")))
        //            return;
        //        Trace.WriteLine(VersionInfo.FullVersion);

        //        //ProfileModel profileModel = await ProfileModel.None.Get(CancellationToken.None).ConfigureAwait(false);
        //        //FolderModel folder = (await profileModel.GetFolders(CancellationToken.None).ConfigureAwait(false)).GetByName("Demo Model 1");
        //        ////            RouteModelCore route = (await folder.GetRoutes(CancellationToken.None).ConfigureAwait(false)).GetByName("SCE");

        //        //FrozenSet<RouteModelCore> routes = await RouteModelHandler.ExpandRouteModels(folder, CancellationToken.None).ConfigureAwait(false);

        //    }

        //    [TestMethod]
        //    public async ValueTask ExpandRoute()
        //    {
        //        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD")))
        //            return;
        //        //Trace.WriteLine(VersionInfo.FullVersion);

        //        //ProfileModel profileModel = await ProfileModel.None.Get(CancellationToken.None).ConfigureAwait(false);
        //        //FolderModel folder = (await profileModel.GetFolders(CancellationToken.None).ConfigureAwait(false)).GetByName("OR Linia 202");
        //        ////            RouteModelCore route = (await folder.GetRoutes(CancellationToken.None).ConfigureAwait(false)).GetByName("SCE");

        //        //FrozenSet<RouteModelCore> routes = await folder.GetRoutes(CancellationToken.None).ConfigureAwait(false);
        //        //RouteModelCore route = routes.GetByName("Linia202_80s");

        //        //FrozenSet<TimetableModel> timetables = await TimetableModelHandler.ExpandTimetableModels(route, CancellationToken.None).ConfigureAwait(false);

        //    }

        //}
    }
}
