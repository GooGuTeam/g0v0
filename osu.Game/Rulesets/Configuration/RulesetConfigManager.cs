// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Extensions;
using osu.Game.Configuration;
using osu.Game.Database;

namespace osu.Game.Rulesets.Configuration
{
    public abstract class RulesetConfigManager<TLookup> : ConfigManager<TLookup>, IRulesetConfigManager, IRulesetConfigSnapshotSource
        where TLookup : struct, Enum
    {
        private readonly RealmAccess realm;

        private readonly int variant;

        private List<RealmRulesetSetting> databasedSettings = new List<RealmRulesetSetting>();

        private readonly string rulesetName;

        protected RulesetConfigManager(SettingsStore store, RulesetInfo ruleset, int? variant = null)
        {
            realm = store?.Realm;

            rulesetName = ruleset.ShortName;

            this.variant = variant ?? 0;

            Load();

            InitialiseDefaults();
        }

        public IReadOnlyDictionary<string, string> CreateSnapshot() => ConfigStore.ToDictionary(
            pair => pair.Key.ToString(), pair => pair.Value.ToString(CultureInfo.InvariantCulture));

        public void ApplySnapshot(IReadOnlyDictionary<string, string> snapshot)
        {
            if (realm != null)
                throw new InvalidOperationException(@"Snapshots may only be applied to detached ruleset configurations.");

            foreach (var pair in snapshot)
            {
                if (!Enum.TryParse(pair.Key, out TLookup key) || !ConfigStore.TryGetValue(key, out var bindable) || bindable is not IParseable parseable)
                    throw new InvalidOperationException($@"Unknown ruleset setting: {pair.Key}");

                parseable.Parse(pair.Value, CultureInfo.InvariantCulture);
            }
        }

        protected override void PerformLoad()
        {
            if (realm != null)
            {
                // As long as RulesetConfigCache exists, there is no need to subscribe to realm events.
                databasedSettings = realm.Realm.All<RealmRulesetSetting>().Where(b => b.RulesetName == rulesetName && b.Variant == variant).ToList();
            }
        }

        private readonly HashSet<TLookup> pendingWrites = new HashSet<TLookup>();

        protected override bool PerformSave()
        {
            TLookup[] changed;

            lock (pendingWrites)
            {
                changed = pendingWrites.ToArray();
                pendingWrites.Clear();
            }

            if (!changed.Any())
                return true;

            realm?.Write(r =>
            {
                foreach (var c in changed)
                {
                    var setting = r.All<RealmRulesetSetting>().First(s => s.RulesetName == rulesetName && s.Variant == variant && s.Key == c.ToString());

                    setting.Value = ConfigStore[c].ToString(CultureInfo.InvariantCulture);
                }
            });

            return true;
        }

        protected override void AddBindable<TBindable>(TLookup lookup, Bindable<TBindable> bindable)
        {
            base.AddBindable(lookup, bindable);

            var setting = databasedSettings.Find(s => s.Key == lookup.ToString());

            if (setting != null)
            {
                bindable.Parse(setting.Value, CultureInfo.InvariantCulture);
            }
            else
            {
                setting = new RealmRulesetSetting
                {
                    Key = lookup.ToString(),
                    Value = bindable.ToString(CultureInfo.InvariantCulture),
                    RulesetName = rulesetName,
                    Variant = variant,
                };

                realm?.Realm.Write(() => realm.Realm.Add(setting));

                databasedSettings.Add(setting);
            }

            bindable.ValueChanged += _ =>
            {
                lock (pendingWrites)
                    pendingWrites.Add(lookup);
            };
        }
    }
}
