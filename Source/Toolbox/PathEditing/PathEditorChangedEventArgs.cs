using System;

using Riel.Runtime.Track;

namespace Riel.Toolbox.PathEditing
{
    public class PathEditorChangedEventArgs : EventArgs
    {
        public TrainPathBase Path { get; }

        public PathEditorChangedEventArgs(TrainPathBase path)
        {
            Path = path;
        }
    }
}
