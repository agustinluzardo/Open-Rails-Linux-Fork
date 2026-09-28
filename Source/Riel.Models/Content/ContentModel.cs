using System.Collections.Immutable;
using System.Diagnostics;

using Riel.Models.Base;

using MemoryPack;

namespace Riel.Models.Content
{
    /// <summary>
    /// Root model representing the top-level content configuration.
    /// Contains the collection of content installation folders and serves as the entry point
    /// for the model hierarchy. This model has no parent.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("Content", ".content")]
    public sealed partial record ContentModel : ModelBase
    {
        /// <inheritdoc/>
        public override ModelBase Parent => null; // Content is root and does not implement a parent

        /// <summary>
        /// The oldest build whose scanned content is still readable. A cache written by anything
        /// older is thrown away and rescanned.
        /// </summary>
        /// <remarks>
        /// This is compared against the running version, so it has to be stated in the version
        /// line the program actually uses. Riel restarted that line at 0.1.0, and upstream's
        /// 2.0.1-dev.482 left every build below the floor - which does not fail, it just rescans
        /// every route on every command, for ever. Raise this only when the model changes shape.
        /// </remarks>
        [MemoryPackIgnore]
        public const string MinimumVersion = "0.1.0-dev.0";

        /// <summary>
        /// Revision of the MSTS-to-content import semantics. Bump this whenever an importer fix
        /// changes the meaning of persisted route/activity/path data without changing its serialized shape.
        /// Old caches do not contain the revision tag and are therefore rebuilt once.
        /// </summary>
        [MemoryPackIgnore]
        public const string ImportRevision = "1";

        [MemoryPackIgnore]
        public const string ImportRevisionTag = "RielContentImportRevision";

        [MemoryPackIgnore]
        public static ContentModel None { get; } = default(ContentModel);

        [MemoryPackConstructor]
        public ContentModel(ImmutableArray<FolderModel> contentFolders): base(string.Empty, null)
        {
            ContentFolders = contentFolders;
        }

        public ContentModel() : base(string.Empty, null)
        {
        }

        /// <summary>Collection of content installation folders available in this configuration.</summary>
        public ImmutableArray<FolderModel> ContentFolders { get; init; } = ImmutableArray<FolderModel>.Empty;

        public override void Initialize(ModelBase parent)
        {
            if (parent != null)
                Trace.TraceWarning($"Parent initialization for {nameof(ContentModel)} is not supported");

            foreach (FolderModel folder in ContentFolders)
            { 
                folder.Initialize(this);
            }
            base.Initialize(parent);
        }
    }
}
