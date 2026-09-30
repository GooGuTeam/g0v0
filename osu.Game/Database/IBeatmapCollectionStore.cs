// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Collections;

namespace osu.Game.Database
{
    /// <summary>
    /// Storage for user beatmap collections.
    /// </summary>
    public interface IBeatmapCollectionStore : IStore<BeatmapCollection>
    {
        /// <summary>
        /// Find a collection by its (unique) name.
        /// </summary>
        BeatmapCollection? FindByName(string name);
    }
}
