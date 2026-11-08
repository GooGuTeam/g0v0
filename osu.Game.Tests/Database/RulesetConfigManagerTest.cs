// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Configuration;

namespace osu.Game.Tests.Database
{
    public class RulesetConfigManagerTest : RealmTest
    {
        [TestCase("FullHD", ViewportMode.Default, "Default")]
        [TestCase("Custom", ViewportMode.Custom, "Custom")]
        public void TestSavedEnumValue(string savedValue, ViewportMode expected, string expectedSavedValue)
        {
            RunTestWithRealm((realm, _) =>
            {
                var ruleset = CreateRuleset();
                var store = new SettingsStore(realm);

                realm.Write(r => r.Add(new RealmRulesetSetting
                {
                    RulesetName = ruleset.ShortName,
                    Variant = 0,
                    Key = TestSetting.ViewportMode.ToString(),
                    Value = savedValue,
                }));

                using (var config = new TestConfigManager(store, ruleset))
                {
                    Assert.That(config.Get<ViewportMode>(TestSetting.ViewportMode), Is.EqualTo(expected));
                    Assert.That(config.Get<bool>(TestSetting.OtherSetting), Is.True);
                    config.Save();
                }

                Assert.That(realm.Run(r => r.All<RealmRulesetSetting>().Single(s => s.Key == TestSetting.ViewportMode.ToString()).Value),
                    Is.EqualTo(expectedSavedValue));

                using (var reloaded = new TestConfigManager(store, ruleset))
                    Assert.That(reloaded.Get<ViewportMode>(TestSetting.ViewportMode), Is.EqualTo(expected));
            });
        }

        private class TestConfigManager : RulesetConfigManager<TestSetting>
        {
            public TestConfigManager(SettingsStore store, RulesetInfo ruleset)
                : base(store, ruleset)
            {
            }

            protected override void InitialiseDefaults()
            {
                SetDefault(TestSetting.ViewportMode, ViewportMode.Default);
                SetDefault(TestSetting.OtherSetting, true);
            }
        }

        private enum TestSetting
        {
            ViewportMode,
            OtherSetting,
        }

        public enum ViewportMode
        {
            Default,
            Custom,
        }
    }
}
