using System.IO;

using Riel.Models.Content;
using Riel.Models.Imported.ImportHandler;
using Riel.Models.Imported.ImportHandler.OpenRails;
using Riel.Models.Settings;

namespace Riel.Models.Imported.Shim
{
    public static class ModelExtensions
    {
        public static string SourceFile(this WeatherModelHeader weatherModel) => weatherModel != null ? Path.Combine(weatherModel.Parent.MstsRouteFolder().WeatherFolder, weatherModel.Tags[WeatherModelHandler.SourceNameKey]) : null;
        public static string SourceFile(this TimetableModel timetableModel) => timetableModel != null ? Path.Combine(timetableModel.Parent.MstsRouteFolder().OpenRailsActivitiesFolder, timetableModel.Tags[TimetableModelHandler.SourceNameKey]) : null;
        public static string SourceFile(this SavePointModel savePointModel) => savePointModel?.Tags[SavePointModelHandler.SourceNameKey];

    }
}
