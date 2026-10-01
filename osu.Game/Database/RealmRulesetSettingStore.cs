// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Game.Configuration;

namespace osu.Game.Database
{
    /// <summary>
    /// Realm-backed implementation of <see cref="IRulesetSettingStore"/>.
    /// </summary>
    public class RealmRulesetSettingStore : IRulesetSettingStore
    {
        private readonly RealmAccess realm;

        public RealmRulesetSettingStore(RealmAccess realm)
        {
            this.realm = realm;
        }

        public string? GetValue(string rulesetName, int variant, string key) => realm.Run(r =>
            r.All<RealmRulesetSetting>()
             .FirstOrDefault(s => s.RulesetName == rulesetName && s.Variant == variant && s.Key == key)?.Value);

        public void SetValue(string rulesetName, int variant, string key, string value) => realm.Write(r =>
        {
            var existing = r.All<RealmRulesetSetting>()
                            .FirstOrDefault(s => s.RulesetName == rulesetName && s.Variant == variant && s.Key == key);

            if (existing != null)
            {
                existing.Value = value;
            }
            else
            {
                r.Add(new RealmRulesetSetting
                {
                    RulesetName = rulesetName,
                    Variant = variant,
                    Key = key,
                    Value = value
                });
            }
        });

        public bool Delete(string rulesetName, int variant, string key) => realm.Write(r =>
        {
            var existing = r.All<RealmRulesetSetting>()
                            .FirstOrDefault(s => s.RulesetName == rulesetName && s.Variant == variant && s.Key == key);

            if (existing == null)
                return false;

            r.Remove(existing);
            return true;
        });
    }
}
