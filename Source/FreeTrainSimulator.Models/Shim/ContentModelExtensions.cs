using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;

namespace FreeTrainSimulator.Models.Shim
{
    /// <summary>
    /// Extension methods for loading and initializing the top-level <see cref="ContentModel"/>,
    /// and generic collection helpers for searching model arrays by name or identifier.
    /// </summary>
    public static class ContentModelExtensions
    {
        #region Content Model
        public static async Task<ContentModel> Get(this ContentModel _, CancellationToken cancellationToken) => await ContentModelHandler.GetCore(cancellationToken).ConfigureAwait(false) ?? await Setup(_, null, cancellationToken).ConfigureAwait(false);
        public static Task<ContentModel> Setup(this ContentModel _, IEnumerable<(string, string)> folders, CancellationToken cancellationToken) => ContentModelHandler.Setup(folders, cancellationToken);
#if UPGRADECONTENT
        public static bool RefreshRequired(this ContentModel _) => true;
#else
        public static bool RefreshRequired(this ContentModel contentModel)
        {
            if (contentModel == null || contentModel.Version.Compare(ContentModel.MinimumVersion) < 0)
                return true;

            // Persisted PathModel/ActivityModel data depends on importer semantics, not just the
            // MemoryPack shape. An old cache can deserialize perfectly while still containing a
            // path topology produced by buggy FTS import code. Force exactly one rescan whenever
            // that semantic revision changes.
            return contentModel.Tags == null ||
                !contentModel.Tags.TryGetValue(ContentModel.ImportRevisionTag, out string revision) ||
                !string.Equals(revision, ContentModel.ImportRevision, StringComparison.Ordinal);
        }
#endif
#endregion

        #region common extensions
        public static T GetByName<T>(this ImmutableArray<T> models, string name) where T : ModelBase
        {
            return models.Where(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        }

        public static T GetByNameOrFirstByName<T>(this ImmutableArray<T> models, string name) where T : ModelBase
        {
            return models.GetByName(name) ?? models.OrderBy(m => m.Name).FirstOrDefault();
        }

        public static T GetById<T>(this ImmutableArray<T> models, string id) where T : ModelBase
        {
            return models.Where(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        }
        #endregion
    }
}
