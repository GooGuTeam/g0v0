// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Skinning;

namespace osu.Game.Database
{
    /// <summary>
    /// Realm-backed implementation of <see cref="ISkinStore"/>.
    /// </summary>
    public class RealmSkinStore : ISkinStore
    {
        private readonly RealmAccess realm;

        public RealmSkinStore(RealmAccess realm)
        {
            this.realm = realm;
        }

        public SkinInfo? GetDetached(Guid id) => realm.Run(r => r.Find<SkinInfo>(id)?.Detach());

        public List<SkinInfo> GetAllUsableDetached() => realm.Run(r =>
            r.All<SkinInfo>()
             .Where(s => !s.DeletePending && !s.Protected)
             .AsEnumerable()
             .Detach());

        public bool Delete(Guid id) => realm.Write(r =>
        {
            var item = r.Find<SkinInfo>(id);

            if (item == null || item.DeletePending)
                return false;

            item.DeletePending = true;
            return true;
        });

        public bool Undelete(Guid id) => realm.Write(r =>
        {
            var item = r.Find<SkinInfo>(id);

            if (item == null || !item.DeletePending)
                return false;

            item.DeletePending = false;
            return true;
        });

        public IDisposable Subscribe(Action onChanged) => realm.RegisterForNotifications(
            r => r.All<SkinInfo>(),
            (_, _) => onChanged());
    }
}
