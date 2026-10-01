// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Game.Collections;

namespace osu.Game.Database
{
    /// <summary>
    /// Realm-backed implementation of <see cref="IBeatmapCollectionStore"/>.
    /// </summary>
    public class RealmBeatmapCollectionStore : RealmStore<BeatmapCollection>, IBeatmapCollectionStore
    {
        public RealmBeatmapCollectionStore(RealmAccess realm)
            : base(realm)
        {
        }

        public BeatmapCollection? FindByName(string name) => Realm.Run(r =>
            r.All<BeatmapCollection>()
             .FirstOrDefault(c => c.Name == name)?.Detach());
    }
}
