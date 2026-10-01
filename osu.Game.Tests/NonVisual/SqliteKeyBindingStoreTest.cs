// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Game.Database;
using osu.Game.Input;
using osu.Game.Input.Bindings;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class SqliteKeyBindingStoreTest
    {
        private string databasePath = null!;

        [SetUp]
        public void SetUp()
        {
            databasePath = Path.Combine(Path.GetTempPath(), "g0v0-test", $"{Guid.NewGuid():N}", "key-bindings.db");
        }

        [Test]
        public void TestAddPersistsAcrossReopen()
        {
            var binding = new RealmKeyBinding(GlobalAction.Back, new KeyCombination(InputKey.Escape))
            {
                ID = Guid.NewGuid()
            };

            using (var store = new SqliteKeyBindingStore(databasePath))
                store.Add(binding);

            using (var reopened = new SqliteKeyBindingStore(databasePath))
            {
                var all = reopened.GetAllDetached();

                Assert.That(all.Count, Is.EqualTo(1));
                Assert.That(all[0].ID, Is.EqualTo(binding.ID));
                Assert.That(all[0].RulesetName, Is.Null);
                Assert.That(all[0].Variant, Is.Null);
                Assert.That(all[0].ActionInt, Is.EqualTo((int)GlobalAction.Back));
                Assert.That(all[0].KeyCombinationString, Is.EqualTo(new KeyCombination(InputKey.Escape).ToString()));
            }
        }

        [Test]
        public void TestUpdatePersistsNewKeyCombination()
        {
            var binding = new RealmKeyBinding(GlobalAction.Back, new KeyCombination(InputKey.Escape));
            Guid id = binding.ID;

            using (var store = new SqliteKeyBindingStore(databasePath))
            {
                store.Add(binding);
                store.Update(id, b => b.KeyCombination = new KeyCombination(InputKey.Z));
            }

            using (var reopened = new SqliteKeyBindingStore(databasePath))
            {
                var updated = reopened.GetAllDetached().Single();
                Assert.That(updated.ID, Is.EqualTo(id));
                Assert.That(updated.KeyCombinationString, Is.EqualTo(new KeyCombination(InputKey.Z).ToString()));
            }
        }

        [Test]
        public void TestDeleteReturnsFalseWhenMissing()
        {
            using var store = new SqliteKeyBindingStore(databasePath);

            var binding = new RealmKeyBinding(GlobalAction.Back, new KeyCombination(InputKey.Escape));
            store.Add(binding);

            Assert.That(store.Delete(binding.ID), Is.True);
            Assert.That(store.Delete(binding.ID), Is.False);
            Assert.That(store.GetAllDetached(), Is.Empty);
        }

        [Test]
        public void TestSubscriptionFiresInitiallyOnWriteAndNotAfterDisposal()
        {
            using var store = new SqliteKeyBindingStore(databasePath);

            int fired = 0;
            var subscription = store.Subscribe(() => fired++);

            Assert.That(fired, Is.EqualTo(1), "initial snapshot notification");

            store.Add(new RealmKeyBinding(GlobalAction.Back, new KeyCombination(InputKey.Escape)));
            Assert.That(fired, Is.EqualTo(2), "notification after write");

            store.Update(store.GetAllDetached().Single().ID, b => b.KeyCombination = new KeyCombination(InputKey.Z));
            Assert.That(fired, Is.EqualTo(3), "notification after update");

            subscription.Dispose();

            store.Delete(store.GetAllDetached().Single().ID);
            Assert.That(fired, Is.EqualTo(3), "no notification after subscription disposal");
        }

        [Test]
        public void TestGetAllDetachedReturnsIndependentInstances()
        {
            using var store = new SqliteKeyBindingStore(databasePath);

            var binding = new RealmKeyBinding(GlobalAction.Back, new KeyCombination(InputKey.Escape));
            store.Add(binding);

            var first = store.GetAllDetached().Single();
            var second = store.GetAllDetached().Single();

            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(first.ID, Is.EqualTo(binding.ID));

            first.KeyCombinationString = "MutatedOutsideStore";
            Assert.That(store.GetAllDetached().Single().KeyCombinationString, Is.EqualTo(new KeyCombination(InputKey.Escape).ToString()));
        }
    }
}
