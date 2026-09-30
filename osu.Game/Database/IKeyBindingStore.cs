// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Input.Bindings;

namespace osu.Game.Database
{
    /// <summary>
    /// Storage for key bindings (global and per-ruleset).
    /// </summary>
    public interface IKeyBindingStore : IStore<RealmKeyBinding>
    {
    }
}
