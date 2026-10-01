// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Database
{
    /// <summary>
    /// Realm-backed implementation of <see cref="IDataStore"/>, adapting the existing
    /// <see cref="RealmAccess"/>.
    /// </summary>
    public class RealmDataStore : IDataStore
    {
        public IBeatmapStore Beatmaps { get; }
        public IScoreStore Scores { get; }
        public ISkinStore Skins { get; }
        public IBeatmapCollectionStore Collections { get; }
        public IKeyBindingStore KeyBindings { get; }
        public IRulesetSettingStore RulesetSettings { get; }
        public IModPresetStore ModPresets { get; }

        public RealmDataStore(RealmAccess realm)
        {
            Beatmaps = new RealmBeatmapStore(realm);
            Scores = new RealmScoreStore(realm);
            Skins = new RealmSkinStore(realm);
            Collections = new RealmBeatmapCollectionStore(realm);
            KeyBindings = new RealmBackedKeyBindingStore(realm);
            RulesetSettings = new RealmRulesetSettingStore(realm);
            ModPresets = new RealmModPresetStore(realm);
        }
    }
}
