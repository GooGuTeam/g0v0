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
    public class RealmBeatmapStore : RealmSoftDeletableStore<BeatmapSetInfo>, IBeatmapStore
    {
        public RealmBeatmapStore(RealmAccess realm)
            : base(realm)
        {
        }

        public BeatmapSetInfo? GetSetDetached(Guid id) => GetDetached(id);

        public List<BeatmapSetInfo> GetAllSetsDetached() => GetAllUsableDetached();

        public BeatmapInfo? GetBeatmapDetached(Guid id) => Realm.Run(r => r.Find<BeatmapInfo>(id)?.Detach());

        public BeatmapInfo? FindBeatmapByHash(string md5Hash) => Realm.Run(r =>
            r.All<BeatmapInfo>()
             .FirstOrDefault(b => b.BeatmapSet != null && !b.BeatmapSet.DeletePending && b.Hash == md5Hash)?.Detach());

        public BeatmapSetInfo? FindSetByOnlineId(int onlineId) => Realm.Run(r =>
            r.All<BeatmapSetInfo>()
             .FirstOrDefault(s => !s.DeletePending && s.OnlineID == onlineId)?.Detach());
    }
}
