using System.Collections.Generic;
using System.Collections.Immutable;

using Riel.Models.Content;
using Riel.Runtime.Track;

namespace Riel.Toolbox.PathEditing
{
    internal sealed class PendingAmbiguousSpanCommit
    {
        public PathModel SourceModel { get; }
        public PathModel TentativeModel { get; }
        public ImmutableArray<int> ChangedNodeIndexes { get; }
        public ImmutableArray<ResolvedPathSpan> AmbiguousSpans { get; }
        public Dictionary<int, int> CandidateSelections { get; }
        public bool ResumeRouteBuilding { get; }

        public PendingAmbiguousSpanCommit(PathModel sourceModel, PathModel tentativeModel,
            ImmutableArray<int> changedNodeIndexes, ImmutableArray<ResolvedPathSpan> ambiguousSpans,
            bool resumeRouteBuilding = false)
        {
            SourceModel = sourceModel;
            TentativeModel = tentativeModel;
            ChangedNodeIndexes = changedNodeIndexes;
            AmbiguousSpans = ambiguousSpans;
            CandidateSelections = new Dictionary<int, int>();
            ResumeRouteBuilding = resumeRouteBuilding;
        }
    }
}
