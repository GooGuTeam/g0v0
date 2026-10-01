// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Database
{
    /// <summary>
    /// Realm-backed implementation of <see cref="IModPresetStore"/>.
    /// </summary>
    public class RealmModPresetStore : RealmSoftDeletableStore<ModPreset>, IModPresetStore
    {
        public RealmModPresetStore(RealmAccess realm)
            : base(realm)
        {
        }

        /// <summary>
        /// Adds a preset, re-attaching the <see cref="ModPreset.Ruleset"/> association to the
        /// managed <see cref="RulesetInfo"/> instance so callers may pass detached objects.
        /// </summary>
        public new void Add(ModPreset item) => Realm.Write(r =>
        {
            item.Ruleset = r.Find<RulesetInfo>(item.Ruleset.ShortName)!;
            r.Add(item);
        });
    }
}
