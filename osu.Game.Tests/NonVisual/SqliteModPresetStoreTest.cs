// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class SqliteModPresetStoreTest
    {
        private string databasePath = null!;
        private RulesetInfo osuRuleset = null!;
        private TestRulesetStore rulesetStore = null!;

        [SetUp]
        public void SetUp()
        {
            databasePath = Path.Combine(Path.GetTempPath(), "g0v0-test", $"{Guid.NewGuid():N}", "mod-presets.db");
            osuRuleset = new OsuRuleset().RulesetInfo;
            rulesetStore = new TestRulesetStore(osuRuleset);
        }

        [Test]
        public void TestAddPersistsAcrossReopen()
        {
            var preset = new ModPreset
            {
                ID = Guid.NewGuid(),
                Name = "test preset",
                Description = "a description",
                Ruleset = osuRuleset,
                Mods = Array.Empty<Mod>()
            };

            using (var store = new SqliteModPresetStore(databasePath, rulesetStore))
                store.Add(preset);

            using (var reopened = new SqliteModPresetStore(databasePath, rulesetStore))
            {
                var all = reopened.GetAllDetached();

                Assert.That(all.Count, Is.EqualTo(1));
                Assert.That(all[0].ID, Is.EqualTo(preset.ID));
                Assert.That(all[0].Name, Is.EqualTo("test preset"));
                Assert.That(all[0].Description, Is.EqualTo("a description"));
                Assert.That(all[0].Ruleset.ShortName, Is.EqualTo("osu"));
                Assert.That(all[0].DeletePending, Is.False);
            }
        }

        [Test]
        public void TestUpdatePersistsName()
        {
            var preset = new ModPreset
            {
                Name = "before",
                Ruleset = osuRuleset,
                Mods = Array.Empty<Mod>()
            };
            Guid id = preset.ID;

            using (var store = new SqliteModPresetStore(databasePath, rulesetStore))
            {
                store.Add(preset);
                store.Update(id, p => p.Name = "after");
            }

            using (var reopened = new SqliteModPresetStore(databasePath, rulesetStore))
                Assert.That(reopened.GetAllDetached().Single().Name, Is.EqualTo("after"));
        }

        [Test]
        public void TestSoftDeleteAndUndelete()
        {
            var preset = new ModPreset
            {
                Ruleset = osuRuleset,
                Mods = Array.Empty<Mod>()
            };
            Guid id = preset.ID;

            using (var store = new SqliteModPresetStore(databasePath, rulesetStore))
            {
                store.Add(preset);

                Assert.That(store.GetAllUsableDetached().Count, Is.EqualTo(1));
                Assert.That(store.Delete(id), Is.True);
                Assert.That(store.GetAllUsableDetached().Count, Is.EqualTo(0));
                Assert.That(store.GetAllDetached().Count, Is.EqualTo(1));
                Assert.That(store.GetAllDetached().Single().DeletePending, Is.True);

                Assert.That(store.Undelete(id), Is.True);
                Assert.That(store.GetAllUsableDetached().Count, Is.EqualTo(1));
            }
        }

        [Test]
        public void TestSubscribeNotifiesOnMutation()
        {
            using var store = new SqliteModPresetStore(databasePath, rulesetStore);

            int notifications = 0;
            using var subscription = store.Subscribe(() => notifications++);

            // Subscribe fires an initial snapshot notification.
            Assert.That(notifications, Is.EqualTo(1));

            store.Add(new ModPreset { Ruleset = osuRuleset, Mods = Array.Empty<Mod>() });
            Assert.That(notifications, Is.EqualTo(2));
        }

        [Test]
        public void TestReturnedInstancesAreDetached()
        {
            var preset = new ModPreset
            {
                Name = "original",
                Ruleset = osuRuleset,
                Mods = Array.Empty<Mod>()
            };

            using var store = new SqliteModPresetStore(databasePath, rulesetStore);
            store.Add(preset);

            var fetched = store.GetAllDetached().Single();
            fetched.Name = "mutated";

            Assert.That(store.GetAllDetached().Single().Name, Is.EqualTo("original"));
        }

        private sealed class TestRulesetStore : RulesetStore
        {
            public TestRulesetStore(params RulesetInfo[] rulesets)
            {
                AvailableRulesets = rulesets;
            }

            public override IEnumerable<RulesetInfo> AvailableRulesets { get; }
        }
    }
}
