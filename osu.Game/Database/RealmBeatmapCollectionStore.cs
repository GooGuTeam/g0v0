// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Collections;

namespace osu.Game.Database
{
    /// <summary>
    /// Realm-backed implementation of <see cref="IBeatmapCollectionStore"/>.
    /// </summary>
    public class RealmBeatmapCollectionStore : IBeatmapCollectionStore
    {
        private readonly RealmAccess realm;

        public RealmBeatmapCollectionStore(RealmAccess realm)
        {
            this.realm = realm;
        }

        public BeatmapCollection? GetDetached(Guid id) => realm.Run(r => r.Find<BeatmapCollection>(id)?.Detach());

        public List<BeatmapCollection> GetAllDetached() => realm.Run(r =>
            r.All<BeatmapCollection>()
             .AsEnumerable()
             .Detach());

        public BeatmapCollection? FindByName(string name) => realm.Run(r =>
            r.All<BeatmapCollection>()
             .FirstOrDefault(c => c.Name == name)?.Detach());

        public void Add(BeatmapCollection item) => realm.Write(r => r.Add(item));

        public void Update(Guid id, Action<BeatmapCollection> update) => realm.Write(r =>
        {
            var item = r.Find<BeatmapCollection>(id);
            if (item != null)
                update(item);
        });

        public bool Delete(Guid id) => realm.Write(r =>
        {
            var item = r.Find<BeatmapCollection>(id);

            if (item == null)
                return false;

            r.Remove(item);
            return true;
        });

        public IDisposable Subscribe(Action onChanged) => realm.RegisterForNotifications(
            r => r.All<BeatmapCollection>(),
            (_, _) => onChanged());
    }
}
