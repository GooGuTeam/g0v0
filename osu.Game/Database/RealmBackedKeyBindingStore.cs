// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Input.Bindings;

namespace osu.Game.Database
{
    /// <summary>
    /// Realm-backed implementation of <see cref="IKeyBindingStore"/>.
    /// </summary>
    /// <remarks>
    /// Named to avoid clashing with the existing higher-level
    /// <see cref="osu.Game.Input.RealmKeyBindingStore"/>, which manages databased defaults
    /// on top of raw bindings.
    /// </remarks>
    public class RealmBackedKeyBindingStore : IKeyBindingStore
    {
        private readonly RealmAccess realm;

        public RealmBackedKeyBindingStore(RealmAccess realm)
        {
            this.realm = realm;
        }

        public List<RealmKeyBinding> GetAllDetached() => realm.Run(r =>
            r.All<RealmKeyBinding>()
             .AsEnumerable()
             .Detach());

        public void Add(RealmKeyBinding item) => realm.Write(r => r.Add(item));

        public void Update(Guid id, Action<RealmKeyBinding> update) => realm.Write(r =>
        {
            var item = r.Find<RealmKeyBinding>(id);
            if (item != null)
                update(item);
        });

        public bool Delete(Guid id) => realm.Write(r =>
        {
            var item = r.Find<RealmKeyBinding>(id);

            if (item == null)
                return false;

            r.Remove(item);
            return true;
        });

        public IDisposable Subscribe(Action onChanged) => realm.RegisterForNotifications(
            r => r.All<RealmKeyBinding>(),
            (_, _) => onChanged());
    }
}
