using Riel.Models.Content;
using Riel.Models.Imported.ImportHandler.TrainSimulator;

namespace Riel.Models.Imported.Shim
{
    public static class ActivityModelExtensions
    {
        public static string SourceFile(this ActivityModelHeader activityModel) => activityModel?.Parent.MstsRouteFolder().ActivityFile(activityModel.Tags[ActivityModelImportHandler.SourceNameKey]);
    }
}
