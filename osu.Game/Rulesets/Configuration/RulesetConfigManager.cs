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
    public abstract class RulesetConfigManager<TLookup> : ConfigManager<TLookup>, IRulesetConfigManager
        where TLookup : struct, Enum
    {
        private readonly IRulesetSettingStore settingStore;

        private readonly int variant;

        private List<(string Key, string Value)> databasedSettings = new List<(string, string)>();

        private readonly string rulesetName;

        protected RulesetConfigManager(SettingsStore store, RulesetInfo ruleset, int? variant = null)
        {
            settingStore = store?.Settings;

            rulesetName = ruleset.ShortName;

            this.variant = variant ?? 0;

            Load();

            InitialiseDefaults();
        }

        protected override void PerformLoad()
        {
            if (settingStore != null)
            {
                // As long as RulesetConfigCache exists, there is no need to subscribe to change events.
                databasedSettings = settingStore.GetAll(rulesetName, variant);
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

            if (settingStore != null)
            {
                foreach (var c in changed)
                    settingStore.SetValue(rulesetName, variant, c.ToString(), ConfigStore[c].ToString(CultureInfo.InvariantCulture));
            }

            return true;
        }

        protected override void AddBindable<TBindable>(TLookup lookup, Bindable<TBindable> bindable)
        {
            base.AddBindable(lookup, bindable);

            string key = lookup.ToString();
            var setting = databasedSettings.Find(s => s.Key == key);

            if (setting.Key != null)
            {
                bindable.Parse(setting.Value, CultureInfo.InvariantCulture);
            }
            else
            {
                string value = bindable.ToString(CultureInfo.InvariantCulture);

                settingStore?.SetValue(rulesetName, variant, key, value);

                databasedSettings.Add((key, value));
            }

            bindable.ValueChanged += _ =>
            {
                lock (pendingWrites)
                    pendingWrites.Add(lookup);
            };
        }
    }
}
