// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Scoring;

namespace osu.Game.Database
{
    /// <summary>
    /// Storage for locally stored scores.
    /// </summary>
    /// <remarks>
    /// All read methods return detached copies which are safe to use on any thread.
    /// </remarks>
    public interface IScoreStore
    {
        /// <summary>
        /// The directory name, relative to the game storage, in which this backend keeps replay files.
        /// </summary>
        string FileStorageDirectory { get; }

        /// <summary>
        /// Retrieve a score by primary key.
        /// </summary>
        /// <returns>The detached score, or null if not found.</returns>
        ScoreInfo? GetDetached(Guid id);

        /// <summary>
        /// Retrieve all scores, including those pending deletion.
        /// </summary>
        List<ScoreInfo> GetAllDetached();

        /// <summary>
        /// Find a score by its replay hash, including scores pending deletion.
        /// Active scores are preferred over deleted ones.
        /// </summary>
        /// <param name="hash">The SHA-256 hash of the replay file. An empty hash never matches.</param>
        ScoreInfo? FindByHash(string hash);

        /// <summary>
        /// Find a non-deleted score by its online ID.
        /// </summary>
        ScoreInfo? FindByOnlineId(long onlineId);

        /// <summary>
        /// Find a non-deleted score by its legacy online ID.
        /// </summary>
        ScoreInfo? FindByLegacyOnlineId(long onlineId);

        /// <summary>
        /// Retrieve all non-deleted scores set against the beatmap with the given hash.
        /// </summary>
        /// <param name="beatmapHash">The SHA-256 hash of the beatmap. An empty hash never matches.</param>
        List<ScoreInfo> GetForBeatmap(string beatmapHash);

        /// <summary>
        /// Add a new score.
        /// The passed instance is consumed by the store and must not be used afterwards.
        /// </summary>
        void Add(ScoreInfo item);

        /// <summary>
        /// Apply an in-place mutation to the stored score with the given primary key, if it exists.
        /// </summary>
        /// <remarks>
        /// The mutation is persisted atomically; if it throws, the stored score is left unchanged.
        /// </remarks>
        void Update(Guid id, Action<ScoreInfo> update);

        /// <summary>
        /// Soft-delete a score.
        /// </summary>
        /// <returns>false if the score does not exist or is already deleted.</returns>
        bool Delete(Guid id);

        /// <summary>
        /// Restore a soft-deleted score.
        /// </summary>
        /// <returns>false if the score does not exist or is not deleted.</returns>
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
