// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Scoring;

namespace osu.Game.Database
{
    /// <summary>
    /// Realm-backed implementation of <see cref="IScoreStore"/>.
    /// </summary>
    public class RealmScoreStore : IScoreStore
    {
        private readonly RealmAccess realm;

        public RealmScoreStore(RealmAccess realm)
        {
            this.realm = realm;
        }

        public ScoreInfo? GetDetached(Guid id) => realm.Run(r => r.Find<ScoreInfo>(id)?.Detach());

        public ScoreInfo? FindByOnlineId(long onlineId) => realm.Run(r =>
            r.All<ScoreInfo>()
             .FirstOrDefault(s => !s.DeletePending && s.OnlineID == onlineId)?.Detach());

        public void Add(ScoreInfo item) => realm.Write(r => r.Add(item));

        public bool Delete(Guid id) => realm.Write(r =>
        {
            var item = r.Find<ScoreInfo>(id);

            if (item == null || item.DeletePending)
                return false;

            item.DeletePending = true;
            return true;
        });

        public bool Undelete(Guid id) => realm.Write(r =>
        {
            var item = r.Find<ScoreInfo>(id);

            if (item == null || !item.DeletePending)
                return false;

            item.DeletePending = false;
            return true;
        });

        public IDisposable Subscribe(Action onChanged) => realm.RegisterForNotifications(
            r => r.All<ScoreInfo>(),
            (_, _) => onChanged());
    }
}
