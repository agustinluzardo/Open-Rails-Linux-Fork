using System;

using Riel.Models.Content;
using Riel.Runtime.Track;

namespace Riel.Toolbox.PathEditing
{
    internal sealed class PendingPassingBranchCandidate
    {
        public PendingPassingBranchCandidate(PathModel sourceModel, int startNodeIndex, int rejoinNodeIndex, ResolvedPathSpan span)
        {
            SourceModel = sourceModel ?? throw new ArgumentNullException(nameof(sourceModel));
            StartNodeIndex = startNodeIndex;
            RejoinNodeIndex = rejoinNodeIndex;
            Span = span ?? throw new ArgumentNullException(nameof(span));
        }

        public PathModel SourceModel { get; }

        public int StartNodeIndex { get; }

        public int RejoinNodeIndex { get; }

        public ResolvedPathSpan Span { get; }
    }
}
