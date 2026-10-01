// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Skinning;

namespace osu.Game.Database
{
    /// <summary>
    /// Realm-backed implementation of <see cref="ISkinStore"/>.
    /// </summary>
    public class RealmSkinStore : RealmSoftDeletableStore<SkinInfo>, ISkinStore
    {
        public RealmSkinStore(RealmAccess realm)
            : base(realm)
        {
        }

        public override List<SkinInfo> GetAllUsableDetached() => Realm.Run(r =>
            r.All<SkinInfo>()
             .Where(s => !s.DeletePending && !s.Protected)
             .AsEnumerable()
             .Detach());
    }
}
