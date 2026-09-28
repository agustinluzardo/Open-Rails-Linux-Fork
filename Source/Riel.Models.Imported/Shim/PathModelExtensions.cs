using Riel.Models.Content;
using Riel.Models.Imported.ImportHandler.TrainSimulator;

namespace Riel.Models.Imported.Shim
{
    public static class PathModelExtensions
    {
        public static string SourceFile(this PathModelHeader pathModel) => pathModel?.Parent.MstsRouteFolder().PathFile(pathModel.Tags[PathModelImportHandler.SourceNameKey]);
    }
}
