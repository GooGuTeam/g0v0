// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Game.Scoring;

namespace osu.Game.Database
{
    /// <summary>
    /// Realm-backed implementation of <see cref="IScoreStore"/>.
    /// </summary>
    public class RealmScoreStore : RealmSoftDeletableStore<ScoreInfo>, IScoreStore
    {
        public RealmScoreStore(RealmAccess realm)
            : base(realm)
        {
        }

        public ScoreInfo? FindByOnlineId(long onlineId) => Realm.Run(r =>
            r.All<ScoreInfo>()
             .FirstOrDefault(s => !s.DeletePending && s.OnlineID == onlineId)?.Detach());
    }
}
