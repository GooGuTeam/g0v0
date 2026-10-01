// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Platform;
using osu.Game.Rulesets;

namespace osu.Game.Database
{
    /// <summary>
    /// Creates the backing implementation for domain stores based on the selected backend.
    /// This is the single seam through which new domain stores must be selected;
    /// the same switch gains cases as further stores become backend-selectable.
    /// </summary>
    public class DataStoreSelector : IDisposable
    {
        private const string key_bindings_database = @"key-bindings.db";
        private const string mod_presets_database = @"mod-presets.db";
        private const string ruleset_settings_database = @"ruleset-settings.db";

        private readonly RealmAccess realm;
        private readonly Storage? storage;
        private readonly RulesetStore? rulesets;
        private readonly DataStoreBackend backend;

        private SqliteKeyBindingStore? sqliteKeyBindingStore;
        private SqliteModPresetStore? sqliteModPresetStore;
        private SqliteRulesetSettingStore? sqliteRulesetSettingStore;

        public DataStoreSelector(RealmAccess realm, Storage? storage, DataStoreBackend backend, RulesetStore? rulesets = null)
        {
            this.realm = realm;
            this.storage = storage;
            this.rulesets = rulesets;
            this.backend = backend;
        }

        /// <summary>
        /// The selected backend.
        /// </summary>
        public DataStoreBackend Backend => backend;

        /// <summary>
        /// Resolve the key binding store for the selected backend.
        /// The returned instance is owned by this selector and must not be disposed by callers.
        /// </summary>
        public IKeyBindingStore GetKeyBindingStore() => backend switch
        {
            DataStoreBackend.Realm => new RealmBackedKeyBindingStore(realm),
            DataStoreBackend.SQLite => sqliteKeyBindingStore ??= new SqliteKeyBindingStore(getDatabasePath(key_bindings_database)),
            _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, null)
        };

        /// <summary>
        /// Resolve the mod preset store for the selected backend.
        /// The returned instance is owned by this selector and must not be disposed by callers.
        /// </summary>
        public IModPresetStore GetModPresetStore() => backend switch
        {
            DataStoreBackend.Realm => new RealmModPresetStore(realm),
            DataStoreBackend.SQLite => sqliteModPresetStore ??= new SqliteModPresetStore(getDatabasePath(mod_presets_database), getRulesetStore()),
            _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, null)
        };

        /// <summary>
        /// Resolve the ruleset setting store for the selected backend.
        /// The returned instance is owned by this selector and must not be disposed by callers.
        /// </summary>
        public IRulesetSettingStore GetRulesetSettingStore() => backend switch
        {
            DataStoreBackend.Realm => new RealmRulesetSettingStore(realm),
            DataStoreBackend.SQLite => sqliteRulesetSettingStore ??= new SqliteRulesetSettingStore(getDatabasePath(ruleset_settings_database)),
            _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, null)
        };

        private RulesetStore getRulesetStore()
        {
            if (rulesets == null)
                throw new InvalidOperationException($"A {nameof(RulesetStore)} is required to use the {nameof(DataStoreBackend.SQLite)} backend for mod presets.");

            return rulesets;
        }

        private string getDatabasePath(string filename)
        {
            if (storage == null)
                throw new InvalidOperationException($"A {nameof(Storage)} is required to use the {nameof(DataStoreBackend.SQLite)} backend.");

            return storage.GetFullPath(filename);
        }

        public void Dispose()
        {
            sqliteKeyBindingStore?.Dispose();
            sqliteModPresetStore?.Dispose();
            sqliteRulesetSettingStore?.Dispose();
        }
    }
}
