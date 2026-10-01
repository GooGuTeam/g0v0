// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;

namespace osu.Game.Database
{
    /// <summary>
    /// Realm-backed implementation of <see cref="IBeatmapStore"/>.
    /// </summary>
    public class RealmBeatmapStore : IBeatmapStore
    {
        private readonly RealmAccess realm;

        public RealmBeatmapStore(RealmAccess realm)
        {
            this.realm = realm;
        }

        public BeatmapSetInfo? GetSetDetached(Guid id) => realm.Run(r => r.Find<BeatmapSetInfo>(id)?.Detach());

        public BeatmapInfo? GetBeatmapDetached(Guid id) => realm.Run(r => r.Find<BeatmapInfo>(id)?.Detach());

        public BeatmapInfo? FindBeatmapByHash(string md5Hash) => realm.Run(r =>
            r.All<BeatmapInfo>()
             .FirstOrDefault(b => b.BeatmapSet != null && !b.BeatmapSet.DeletePending && b.Hash == md5Hash)?.Detach());

        public BeatmapSetInfo? FindSetByOnlineId(int onlineId) => realm.Run(r =>
            r.All<BeatmapSetInfo>()
             .FirstOrDefault(s => !s.DeletePending && s.OnlineID == onlineId)?.Detach());

        public List<BeatmapSetInfo> GetAllSetsDetached() => realm.Run(r =>
            r.All<BeatmapSetInfo>()
             .Where(s => !s.DeletePending)
             .AsEnumerable()
             .Detach());

        public void Add(BeatmapSetInfo item) => realm.Write(r => r.Add(item));

        public bool Delete(Guid id) => realm.Write(r =>
        {
            var item = r.Find<BeatmapSetInfo>(id);

            if (item == null || item.DeletePending)
                return false;

            item.DeletePending = true;
            return true;
        });

        public bool Undelete(Guid id) => realm.Write(r =>
        {
            var item = r.Find<BeatmapSetInfo>(id);

            if (item == null || !item.DeletePending)
                return false;

            item.DeletePending = false;
            return true;
        });

        public IDisposable Subscribe(Action onChanged) => realm.RegisterForNotifications(
            r => r.All<BeatmapSetInfo>(),
            (_, _) => onChanged());
    }
}
