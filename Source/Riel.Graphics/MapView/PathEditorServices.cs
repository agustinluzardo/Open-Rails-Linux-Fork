using Riel.Graphics.MapView.Widgets;
using Riel.Models.Content;
using Riel.Runtime.Track;

namespace Riel.Graphics.MapView
{
    internal sealed class PathEditorServices : IPathEditorServices
    {
        public TrackWorld TrackWorld { get; }

        public PathEditorServices(TrackWorld trackWorld)
        {
            TrackWorld = trackWorld ?? throw new System.ArgumentNullException(nameof(trackWorld));
        }

        public EditorTrainPath CreateEditorTrainPath(PathModel pathModel)
        {
            return pathModel == null ? null : new EditorTrainPath(pathModel, TrackWorld);
        }
    }
}
