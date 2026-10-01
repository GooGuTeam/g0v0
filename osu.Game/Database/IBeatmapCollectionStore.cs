// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Collections;

namespace osu.Game.Database
{
    /// <summary>
    /// Storage for user beatmap collections.
    /// </summary>
    /// <remarks>
    /// All read methods return detached copies which are safe to use on any thread.
    /// Collections are hard-deleted; they do not support soft deletion.
    /// </remarks>
    public interface IBeatmapCollectionStore
    {
        /// <summary>
        /// Retrieve a collection by primary key.
        /// </summary>
        /// <returns>The detached collection, or null if not found.</returns>
        BeatmapCollection? GetDetached(Guid id);

        /// <summary>
        /// Retrieve all collections.
        /// </summary>
        List<BeatmapCollection> GetAllDetached();

        /// <summary>
        /// Find a collection by its name.
        /// </summary>
        BeatmapCollection? FindByName(string name);

        /// <summary>
        /// Add a new collection.
        /// The passed instance is consumed by the store and must not be used afterwards.
        /// </summary>
        void Add(BeatmapCollection item);

        /// <summary>
        /// Apply a mutation to the stored collection with the given primary key.
        /// </summary>
        void Update(Guid id, Action<BeatmapCollection> update);

        /// <summary>
        /// Permanently delete a collection.
        /// </summary>
        /// <returns>false if the collection does not exist.</returns>
        bool Delete(Guid id);

        /// <summary>
        /// Subscribe to any change in this store. The callback is an invalidation hint only;
        /// refetch on invocation. Also invoked once for the initial snapshot and after any
        /// backend reset. The invoking thread is backend-defined.
        /// </summary>
        /// <returns>A handle which terminates the subscription when disposed.</returns>
        IDisposable Subscribe(Action onChanged);
    }
}
