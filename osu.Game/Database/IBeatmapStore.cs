// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;

namespace osu.Game.Database
{
    /// <summary>
    /// Storage for beatmap sets and their contained beatmaps.
    /// </summary>
    /// <remarks>
    /// All read methods return detached copies which are safe to use on any thread.
    /// </remarks>
    public interface IBeatmapStore
    {
        /// <summary>
        /// Retrieve a beatmap set by primary key.
        /// </summary>
        /// <returns>The detached set, or null if not found.</returns>
        BeatmapSetInfo? GetSetDetached(Guid id);

        /// <summary>
        /// Retrieve a single beatmap by primary key.
        /// </summary>
        /// <returns>The detached beatmap, or null if not found.</returns>
        BeatmapInfo? GetBeatmapDetached(Guid id);

        /// <summary>
        /// Find a beatmap by its MD5 hash, excluding beatmaps in deleted sets.
        /// </summary>
        BeatmapInfo? FindBeatmapByHash(string md5Hash);

        /// <summary>
        /// Find a non-deleted beatmap set by its online ID.
        /// </summary>
        BeatmapSetInfo? FindSetByOnlineId(int onlineId);

        /// <summary>
        /// Retrieve all non-deleted beatmap sets.
        /// Matches the semantics of <c>BeatmapManager.GetAllUsableBeatmapSets()</c>;
        /// callers filter protected sets and empty sets themselves as needed.
        /// </summary>
        List<BeatmapSetInfo> GetAllSetsDetached();

        /// <summary>
        /// Add a new beatmap set.
        /// The passed instance is consumed by the store and must not be used afterwards.
        /// </summary>
        void Add(BeatmapSetInfo item);

        /// <summary>
        /// Soft-delete a beatmap set.
        /// </summary>
        /// <returns>false if the set does not exist or is already deleted.</returns>
        bool Delete(Guid id);

        /// <summary>
        /// Restore a soft-deleted beatmap set.
        /// </summary>
        /// <returns>false if the set does not exist or is not deleted.</returns>
        bool Undelete(Guid id);

        /// <summary>
        /// Subscribe to any change in this store. The callback is an invalidation hint only;
        /// refetch on invocation. Also invoked once for the initial snapshot and after any
        /// backend reset. The invoking thread is backend-defined.
        /// </summary>
        /// <returns>A handle which terminates the subscription when disposed.</returns>
        IDisposable Subscribe(Action onChanged);
    }
}
