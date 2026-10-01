// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using NUnit.Framework;
using osu.Game.Database;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class SqliteRulesetSettingStoreTest
    {
        private string databasePath = null!;

        [SetUp]
        public void SetUp()
        {
            databasePath = Path.Combine(Path.GetTempPath(), "g0v0-test", $"{Guid.NewGuid():N}", "ruleset-settings.db");
        }

        [Test]
        public void TestSetAndGetPersistsAcrossReopen()
        {
            using (var store = new SqliteRulesetSettingStore(databasePath))
                store.SetValue("osu", 0, "key", "value");

            using (var reopened = new SqliteRulesetSettingStore(databasePath))
                Assert.That(reopened.GetValue("osu", 0, "key"), Is.EqualTo("value"));
        }

        [Test]
        public void TestSetReplacesExistingValue()
        {
            using var store = new SqliteRulesetSettingStore(databasePath);

            store.SetValue("osu", 0, "key", "first");
            store.SetValue("osu", 0, "key", "second");

            Assert.That(store.GetValue("osu", 0, "key"), Is.EqualTo("second"));
        }

        [Test]
        public void TestGetAllReturnsOnlyMatchingRulesetAndVariant()
        {
            using var store = new SqliteRulesetSettingStore(databasePath);

            store.SetValue("osu", 0, "a", "1");
            store.SetValue("osu", 0, "b", "2");
            store.SetValue("osu", 4, "a", "variant4");
            store.SetValue("taiko", 0, "a", "taiko");

            var all = store.GetAll("osu", 0);

            Assert.That(all.Count, Is.EqualTo(2));
            Assert.That(all, Does.Contain(("a", "1")));
            Assert.That(all, Does.Contain(("b", "2")));
        }

        [Test]
        public void TestGetMissingReturnsNull()
        {
            using var store = new SqliteRulesetSettingStore(databasePath);

            Assert.That(store.GetValue("osu", 0, "missing"), Is.Null);
        }

        [Test]
        public void TestDelete()
        {
            using var store = new SqliteRulesetSettingStore(databasePath);

            store.SetValue("osu", 0, "key", "value");

            Assert.That(store.Delete("osu", 0, "key"), Is.True);
            Assert.That(store.GetValue("osu", 0, "key"), Is.Null);
            Assert.That(store.Delete("osu", 0, "key"), Is.False);
        }
    }
}
