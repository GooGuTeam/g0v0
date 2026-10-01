// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Input.Bindings;

namespace osu.Game.Database
{
    /// <summary>
    /// Realm-backed implementation of <see cref="IKeyBindingStore"/>.
    /// </summary>
    /// <remarks>
    /// Named to avoid clashing with the existing higher-level
    /// <see cref="Input.RealmKeyBindingStore"/>, which manages databased defaults
    /// on top of raw bindings.
    /// </remarks>
    public class RealmBackedKeyBindingStore : RealmStore<RealmKeyBinding>, IKeyBindingStore
    {
        public RealmBackedKeyBindingStore(RealmAccess realm)
            : base(realm)
        {
        }
    }
}
