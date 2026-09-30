// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Scoring;

namespace osu.Game.Database
{
    /// <summary>
    /// Storage for locally stored scores.
    /// </summary>
    public interface IScoreStore : ISoftDeletableStore<ScoreInfo>
    {
        /// <summary>
        /// Find a non-deleted score by its online ID.
        /// </summary>
        ScoreInfo? FindByOnlineId(long onlineId);
    }
}
