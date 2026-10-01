// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Database
{
    /// <summary>
    /// Realm-backed implementation of <see cref="IModPresetStore"/>.
    /// </summary>
    public class RealmModPresetStore : IModPresetStore
    {
        private readonly RealmAccess realm;

        public RealmModPresetStore(RealmAccess realm)
        {
            this.realm = realm;
        }

        public ModPreset? GetDetached(Guid id) => realm.Run(r => r.Find<ModPreset>(id)?.Detach());

        public List<ModPreset> GetAllUsableDetached() => realm.Run(r =>
            r.All<ModPreset>()
             .Where(p => !p.DeletePending)
             .AsEnumerable()
             .Detach());

        public bool Delete(Guid id) => realm.Write(r =>
        {
            var item = r.Find<ModPreset>(id);

            if (item == null || item.DeletePending)
                return false;

            item.DeletePending = true;
            return true;
        });

        public bool Undelete(Guid id) => realm.Write(r =>
        {
            var item = r.Find<ModPreset>(id);

            if (item == null || !item.DeletePending)
                return false;

            item.DeletePending = false;
            return true;
        });

        public IDisposable Subscribe(Action onChanged) => realm.RegisterForNotifications(
            r => r.All<ModPreset>(),
            (_, _) => onChanged());
    }
}
