// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using NUnit.Framework;
using osu.Game.Collections;
using osu.Game.Database;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class SqliteBeatmapCollectionStoreTest
    {
        private string databasePath = null!;
        private SqliteBeatmapCollectionStore store = null!;

        [SetUp]
        public void SetUp()
        {
            databasePath = Path.Combine(Path.GetTempPath(), "g0v0-test", $"{Guid.NewGuid():N}", "beatmap-collections.db");
            store = new SqliteBeatmapCollectionStore(databasePath);
        }

        [TearDown]
        public void TearDown()
        {
            store.Dispose();
        }

        [Test]
        public void TestAddAndGet()
        {
            var collection = new BeatmapCollection("favourites", new[] { "hash1", "hash2" });
            store.Add(collection);

            BeatmapCollection? fetched = store.GetDetached(collection.ID);

            Assert.That(fetched, Is.Not.Null);
            Assert.That(fetched!.Name, Is.EqualTo("favourites"));
            Assert.That(fetched.BeatmapMD5Hashes, Is.EqualTo(new[] { "hash1", "hash2" }));
            Assert.That(fetched.LastModified, Is.EqualTo(collection.LastModified).Within(TimeSpan.FromSeconds(1)));
        }

        [Test]
        public void TestFindByName()
        {
            var collection = new BeatmapCollection("jump maps");
            store.Add(collection);

            Assert.That(store.FindByName("jump maps")?.ID, Is.EqualTo(collection.ID));
            Assert.That(store.FindByName("missing"), Is.Null);
        }

        [Test]
        public void TestUpdate()
        {
            var collection = new BeatmapCollection("old name");
            store.Add(collection);

            store.Update(collection.ID, c =>
            {
                c.Name = "new name";
                c.BeatmapMD5Hashes.Add("hash3");
            });

            BeatmapCollection? fetched = store.GetDetached(collection.ID);
            Assert.That(fetched?.Name, Is.EqualTo("new name"));
            Assert.That(fetched?.BeatmapMD5Hashes, Contains.Item("hash3"));

            store.Update(Guid.NewGuid(), c => c.Name = "ghost");
        }

        [Test]
        public void TestDelete()
        {
            var collection = new BeatmapCollection("to delete");
            store.Add(collection);

            Assert.That(store.Delete(collection.ID), Is.True);
            Assert.That(store.GetDetached(collection.ID), Is.Null);
            Assert.That(store.Delete(collection.ID), Is.False);
        }

        [Test]
        public void TestSubscribeNotifiesOnMutations()
        {
            int notifications = 0;
            using var subscription = store.Subscribe(() => notifications++);

            Assert.That(notifications, Is.EqualTo(1)); // initial snapshot

            var collection = new BeatmapCollection("sub test");
            store.Add(collection);
            Assert.That(notifications, Is.EqualTo(2));

            store.Update(collection.ID, c => c.Name = "renamed");
            Assert.That(notifications, Is.EqualTo(3));

            store.Delete(collection.ID);
            Assert.That(notifications, Is.EqualTo(4));

            subscription.Dispose();
            var another = new BeatmapCollection("after unsubscribe");
            store.Add(another);
            Assert.That(notifications, Is.EqualTo(4)); // no further notifications
        }

        [Test]
        public void TestEmptyHashListRoundTrips()
        {
            var collection = new BeatmapCollection("empty");
            store.Add(collection);

            BeatmapCollection? fetched = store.GetDetached(collection.ID);
            Assert.That(fetched, Is.Not.Null);
            Assert.That(fetched!.BeatmapMD5Hashes, Is.Empty);
        }
    }
}
