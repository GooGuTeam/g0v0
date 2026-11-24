// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.IO;
using Realms;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class LazerStorageTest
    {
        private string directory = null!;
        private SynchronizationContext? originalContext;

        [SetUp]
        public void SetUp()
        {
            // Realm captures the ambient synchronisation context and posts refreshes to it from its file watcher.
            // NUnit replaces that context between tests, so a refresh could outlive the realm it belongs to.
            originalContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);

            directory = Path.Combine(Path.GetTempPath(), $@"lazer-storage-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);

            SynchronizationContext.SetSynchronizationContext(originalContext);
        }

        [Test]
        public void TestNotUsableWithoutDatabase()
        {
            Assert.That(LazerStorage.IsUsableInstall(new DirectoryInfo(directory)), Is.False);

            File.WriteAllText(Path.Combine(directory, OsuGameBase.CLIENT_DATABASE_FILENAME), string.Empty);

            Assert.That(LazerStorage.IsUsableInstall(new DirectoryInfo(directory)), Is.True);
        }

        /// <summary>
        /// An installation which is newer than this build can only be read from a copy, as realm refuses to downgrade a database in place.
        /// </summary>
        [Test]
        public void TestReadInstallationWithNewerSchema()
        {
            assertInstallationReadable(RealmAccess.SCHEMA_VERSION + 5);
        }

        /// <summary>
        /// An installation which is older than this build has to be copied as well, so that its schema can be upgraded without touching it.
        /// </summary>
        [Test]
        public void TestReadInstallationWithOlderSchema()
        {
            assertInstallationReadable(Math.Max(1UL, RealmAccess.SCHEMA_VERSION - 5));
        }

        /// <summary>
        /// A late consumer must not silently create another temporary copy which nothing would clean up.
        /// </summary>
        [Test]
        public void TestCannotOpenAfterDispose()
        {
            createInstallation(RealmAccess.SCHEMA_VERSION + 5);

            var storage = new LazerStorage(directory);
            storage.Dispose();

            Assert.That(() => storage.OpenRealm(), Throws.TypeOf<ObjectDisposedException>());
        }

        private void assertInstallationReadable(ulong schemaVersion)
        {
            string databasePath = createInstallation(schemaVersion);
            byte[] originalContents = File.ReadAllBytes(databasePath);

            using (var storage = new LazerStorage(directory))
            {
                using (Realm realm = storage.OpenRealm())
                    Assert.That(realm.All<BeatmapSetInfo>().Count(), Is.EqualTo(1));
            }

            Assert.That(File.ReadAllBytes(databasePath), Is.EqualTo(originalContents), "the source installation's database must not be modified");
        }

        private string createInstallation(ulong schemaVersion)
        {
            string databasePath = Path.Combine(directory, OsuGameBase.CLIENT_DATABASE_FILENAME);

            using (Realm realm = Realm.GetInstance(new RealmConfiguration(databasePath) { SchemaVersion = schemaVersion }))
            {
                // An explicit transaction rather than realm.Write(): a closure here would capture a variable which is disposed at the end of this scope.
                using (var transaction = realm.BeginWrite())
                {
                    realm.Add(new BeatmapSetInfo());
                    transaction.Commit();
                }
            }

            // Re-open once (read-only) so that any pending writes are flushed before the reference contents are captured.
            using (Realm realm = Realm.GetInstance(new RealmConfiguration(databasePath) { SchemaVersion = schemaVersion, IsReadOnly = true }))
            {
                Assert.That(realm.All<BeatmapSetInfo>().Count(), Is.EqualTo(1));
            }

            return databasePath;
        }
    }
}
