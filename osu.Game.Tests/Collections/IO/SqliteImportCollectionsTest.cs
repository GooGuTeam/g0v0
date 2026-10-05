// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.IO.Legacy;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Collections.IO
{
    /// <summary>
    /// Verifies legacy stable collection imports against the SQLite-backed collection store,
    /// particularly the merge-by-name and hash de-duplication behaviour of the importer.
    /// </summary>
    [TestFixture]
    public class SqliteImportCollectionsTest
    {
        private string databasePath = null!;
        private SqliteBeatmapCollectionStore store = null!;
        private LegacyCollectionImporter importer = null!;

        [SetUp]
        public void SetUp()
        {
            databasePath = Path.Combine(Path.GetTempPath(), "g0v0-test", $"{Guid.NewGuid():N}", "beatmap-collections.db");
            store = new SqliteBeatmapCollectionStore(databasePath);
            importer = new LegacyCollectionImporter(store);
        }

        [TearDown]
        public void TearDown()
        {
            store.Dispose();
        }

        [Test]
        public async Task TestImport()
        {
            await importTestResource();

            var collections = store.GetAllDetached();

            Assert.That(collections.Count, Is.EqualTo(2));
            Assert.That(collections.Single(c => c.Name == "First").BeatmapMD5Hashes.Count, Is.EqualTo(1));
            Assert.That(collections.Single(c => c.Name == "Second").BeatmapMD5Hashes.Count, Is.EqualTo(12));
        }

        [Test]
        public async Task TestReimportMergesByNameAndDeduplicatesHashes()
        {
            await importTestResource();
            await importTestResource();

            var collections = store.GetAllDetached();

            Assert.That(collections.Count, Is.EqualTo(2), "re-importing must merge into existing collections");
            Assert.That(collections.Single(c => c.Name == "First").BeatmapMD5Hashes.Count, Is.EqualTo(1), "existing hashes must not be duplicated");
            Assert.That(collections.Single(c => c.Name == "Second").BeatmapMD5Hashes.Count, Is.EqualTo(12), "existing hashes must not be duplicated");
        }

        [Test]
        public async Task TestImportMergesNewHashesIntoExistingCollection()
        {
            await importTestResource();

            Guid originalId = store.FindByName("First")!.ID;
            int secondHashCount = store.FindByName("Second")!.BeatmapMD5Hashes.Count;

            using (var stream = createDatabaseStream("First", "new-hash-1", "new-hash-2"))
                await importer.Import(stream);

            var first = store.FindByName("First");

            Assert.That(first, Is.Not.Null);
            Assert.That(first!.ID, Is.EqualTo(originalId), "existing collection must be merged into rather than replaced");
            Assert.That(first.BeatmapMD5Hashes.Count, Is.EqualTo(3));
            Assert.That(first.BeatmapMD5Hashes, Contains.Item("new-hash-1"));
            Assert.That(first.BeatmapMD5Hashes, Contains.Item("new-hash-2"));

            Assert.That(store.FindByName("Second")!.BeatmapMD5Hashes.Count, Is.EqualTo(secondHashCount));
            Assert.That(store.GetAllDetached().Count, Is.EqualTo(2));
        }

        [Test]
        public async Task TestImportAddsUnknownCollection()
        {
            await importTestResource();

            using (var stream = createDatabaseStream("Third", "hash-a", "hash-b"))
                await importer.Import(stream);

            var third = store.FindByName("Third");

            Assert.That(third, Is.Not.Null);
            Assert.That(third!.BeatmapMD5Hashes, Is.EqualTo(new[] { "hash-a", "hash-b" }));
            Assert.That(store.GetAllDetached().Count, Is.EqualTo(3));
        }

        private async Task importTestResource()
        {
            using (var stream = TestResources.OpenResource("Collections/collections.db"))
                await importer.Import(stream);
        }

        /// <summary>
        /// Creates an in-memory legacy collection database stream in the format the importer reads.
        /// </summary>
        private static MemoryStream createDatabaseStream(string collectionName, params string[] hashes)
        {
            var stream = new MemoryStream();

            using (var writer = new SerializationWriter(stream, true))
            {
                writer.Write(20200907); // version (ignored by the importer)
                writer.Write(1); // collection count
                writer.Write(collectionName);
                writer.Write(hashes.Length);

                foreach (string hash in hashes)
                    writer.Write(hash);
            }

            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }
    }
}
