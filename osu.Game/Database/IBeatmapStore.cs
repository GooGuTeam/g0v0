// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Beatmaps;

namespace osu.Game.Database
{
    /// <summary>
    /// Storage for beatmap sets and their contained beatmaps.
    /// </summary>
    public interface IBeatmapStore : ISoftDeletableStore<BeatmapSetInfo>
    {
        /// <summary>
        /// Run a read query over all beatmaps (across all sets) and return its detached result.
        /// </summary>
        TResult QueryBeatmaps<TResult>(Func<IQueryable<BeatmapInfo>, TResult> query);

        /// <summary>
        /// Retrieve a single beatmap by primary key, detached from the backing store.
        /// </summary>
        BeatmapInfo? GetBeatmapDetached(Guid id);

        /// <summary>
        /// Find a non-deleted beatmap by its MD5 hash.
        /// </summary>
        BeatmapInfo? FindBeatmapByHash(string md5Hash);

        /// <summary>
        /// Find a non-deleted beatmap set by its online ID.
        /// </summary>
        BeatmapSetInfo? FindSetByOnlineId(int onlineId);
    }
}
