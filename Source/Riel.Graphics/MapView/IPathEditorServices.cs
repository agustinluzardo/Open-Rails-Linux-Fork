using Riel.Graphics.MapView.Widgets;
using Riel.Models.Content;
using Riel.Runtime.Track;

namespace Riel.Graphics.MapView
{
    internal interface IPathEditorServices
    {
        TrackWorld TrackWorld { get; }

        EditorTrainPath CreateEditorTrainPath(PathModel pathModel);
    }
}
