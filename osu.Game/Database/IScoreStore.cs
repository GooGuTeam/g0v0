// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
        /// Retrieve a score by primary key.
        /// </summary>
        /// <returns>The detached score, or null if not found.</returns>
        ScoreInfo? GetDetached(Guid id);

        /// <summary>
        /// Find a non-deleted score by its online ID.
        /// </summary>
        ScoreInfo? FindByOnlineId(long onlineId);

        /// <summary>
        /// Add a new score.
        /// The passed instance is consumed by the store and must not be used afterwards.
        /// </summary>
        void Add(ScoreInfo item);

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
