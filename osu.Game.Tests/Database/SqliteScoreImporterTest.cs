// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Extensions;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.IO.Archives;
using osu.Game.Models;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Users;

namespace osu.Game.Tests.Database
{
    /// <summary>
    /// Integration tests for importing replays through the backend-selectable score store.
    /// The importer is given an explicit model; replay bytes are checked without decoding.
    /// Realm operations run on the framework update thread, matching the other database tests.
    /// </summary>
    [TestFixture(DataStoreBackend.Realm)]
    [TestFixture(DataStoreBackend.SQLite)]
    public partial class SqliteScoreImporterTest : RealmTest
    {
        private static readonly byte[] replay_bytes = Enumerable.Range(0, 512).Select(i => (byte)(i * 37 % 251)).ToArray();

        private static readonly DateTimeOffset score_date = new DateTimeOffset(2026, 10, 22, 12, 34, 56, TimeSpan.FromHours(8));

        private readonly DataStoreBackend backend;

        private Storage storage = null!;
        private RealmAccess realm = null!;
        private RealmRulesetStore rulesets = null!;
        private DataStoreSelector selector = null!;
        private IScoreStore store = null!;
        private ScoreImporter importer = null!;
        private BeatmapInfo beatmap = null!;
        private RulesetInfo ruleset = null!;

        public SqliteScoreImporterTest(DataStoreBackend backend)
        {
            this.backend = backend;
        }

        private void setUp(RealmAccess realmAccess, Storage testStorage)
        {
            // Ensure the ruleset assembly is loaded before the ruleset store enumerates loaded assemblies.
            GC.KeepAlive(typeof(OsuRuleset));

            storage = testStorage;
            realm = realmAccess;
            rulesets = new RealmRulesetStore(realm, storage);
            selector = new DataStoreSelector(realm, storage, backend, rulesets);
            store = selector.GetScoreStore();
            importer = new ScoreImporter(rulesets, () => null!, storage, realm, new DummyAPIAccess(), store);

            (beatmap, ruleset) = seedBeatmap();
        }

        private void tearDown()
        {
            selector.Dispose();
            rulesets.Dispose();
        }

        private void runTest(Action action)
        {
            RunTestWithRealm((realmAccess, testStorage) =>
            {
                setUp(realmAccess, testStorage);

                try
                {
                    action();
                }
                finally
                {
                    tearDown();
                }
            });
        }

        [Test]
        public void TestDuplicateImportReusesExistingScore() => runTest(() =>
        {
            Guid id = Guid.NewGuid();

            Live<ScoreInfo> first = importScore(id);
            Live<ScoreInfo> second = importScore(Guid.NewGuid());

            Assert.That(second.ID, Is.EqualTo(first.ID), "re-importing the same replay must reuse the stored score");
            Assert.That(second.ID, Is.EqualTo(id));
            Assert.That(store.GetAllDetached(), Has.Count.EqualTo(1), "duplicate import must not insert a second row");
            Assert.That(store.GetDetached(id)!.Files, Has.Count.EqualTo(1));
            Assert.That(exportReplay(store.GetDetached(id)!), Is.EqualTo(replay_bytes));
        });

        [Test]
        public void TestDeleteThenReimportRestoresSameScore() => runTest(() =>
        {
            Guid id = Guid.NewGuid();

            importScore(id);

            Assert.That(store.Delete(id), Is.True);
            Assert.That(store.GetDetached(id)!.DeletePending, Is.True);

            Live<ScoreInfo> reimported = importScore(Guid.NewGuid());

            Assert.That(reimported.ID, Is.EqualTo(id), "re-importing a deleted replay must restore the same row");

            ScoreInfo restored = store.GetDetached(id)!;

            Assert.That(restored.DeletePending, Is.False);
            Assert.That(store.GetAllDetached(), Has.Count.EqualTo(1));
            assertStoredScore(restored, id);
            Assert.That(exportReplay(restored), Is.EqualTo(replay_bytes));
        });

        [Test]
        public void TestImportedScoreIsFullyPersisted() => runTest(() =>
        {
            Guid id = Guid.NewGuid();

            importScore(id);

            ScoreInfo stored = store.GetDetached(id)!;

            assertStoredScore(stored, id);
            Assert.That(exportReplay(stored), Is.EqualTo(replay_bytes));
        });

        [Test]
        public void TestScoreIsPersistedOnlyToSelectedBackend() => runTest(() =>
        {
            Guid id = Guid.NewGuid();

            importScore(id);

            Assert.That(store.GetDetached(id), Is.Not.Null);

            if (backend == DataStoreBackend.SQLite)
            {
                Assert.That(realm.Run(r => r.All<ScoreInfo>().Count()), Is.Zero, "the SQLite backend must not write scores to realm");
                Assert.That(realm.Run(r => r.All<RealmFile>().Count()), Is.Zero, "the SQLite backend must not register replay files in realm");
            }
            else
            {
                Assert.That(realm.Run(r => r.All<ScoreInfo>().AsEnumerable().Count(s => s.ID == id)), Is.EqualTo(1), "the realm backend must persist imported scores to realm");
                Assert.That(realm.Run(r => r.All<RealmFile>().Count()), Is.EqualTo(1), "the realm backend must register replay files in realm");
            }
        });

        [Test]
        public void TestReopenRetainsScoreAndReplayFiles() => runTest(() =>
        {
            Guid id = Guid.NewGuid();

            importScore(id);

            reopenStore();

            ScoreInfo reloaded = store.GetDetached(id)!;

            assertStoredScore(reloaded, id);
            Assert.That(exportReplay(reloaded), Is.EqualTo(replay_bytes));
        });

        [Test]
        public void TestInvalidModSetIsNotPersisted() => runTest(() =>
        {
            Guid id = Guid.NewGuid();

            using var archive = new ByteArrayArchiveReader(replay_bytes, "replay.osr");

            // NoFail and SuddenDeath are mutually exclusive, so population must reject the score.
            var score = createScore(id, new Mod[] { new OsuModNoFail(), new OsuModSuddenDeath() });

            Assert.Throws<InvalidOperationException>(() => importer.ImportModel(score, archive));

            Assert.That(store.GetDetached(id), Is.Null);
            Assert.That(store.GetAllDetached(), Is.Empty);
            Assert.That(realm.Run(r => r.All<ScoreInfo>().Count()), Is.Zero);
        });

        [Test]
        public void TestCancelledImportIsNotPersisted() => runTest(() =>
        {
            if (backend != DataStoreBackend.SQLite)
                Assert.Ignore($"{nameof(ScoreImporter)} only observes the cancellation token on the SQLite import path.");

            Guid id = Guid.NewGuid();

            using var archive = new ByteArrayArchiveReader(replay_bytes, "replay.osr");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.Throws<OperationCanceledException>(() => importer.ImportModel(createScore(id), archive, cancellationToken: cancellation.Token));

            Assert.That(store.GetDetached(id), Is.Null);
            Assert.That(store.GetAllDetached(), Is.Empty);
            Assert.That(realm.Run(r => r.All<ScoreInfo>().Count()), Is.Zero);
        });

        private Live<ScoreInfo> importScore(Guid id, IEnumerable<Mod>? mods = null)
        {
            using var archive = new ByteArrayArchiveReader(replay_bytes, "replay.osr");

            Live<ScoreInfo>? imported = importer.ImportModel(createScore(id, mods), archive);

            Assert.That(imported, Is.Not.Null, "the score should have been imported");
            return imported!;
        }

        private void reopenStore()
        {
            selector.Dispose();

            selector = new DataStoreSelector(realm, storage, backend, rulesets);
            store = selector.GetScoreStore();
        }

        private byte[] exportReplay(ScoreInfo score)
        {
            var exporter = new LegacyScoreExporter(storage, store.FileStorageDirectory);

            using var output = new MemoryStream();
            exporter.ExportToStream(score, output, null);
            return output.ToArray();
        }

        private void assertStoredScore(ScoreInfo stored, Guid expectedId)
        {
            string replayHash;

            using (var replayStream = new MemoryStream(replay_bytes))
                replayHash = replayStream.ComputeSHA2Hash();

            BeatmapInfo? linkedBeatmap = stored.BeatmapInfo;

            Assert.That(linkedBeatmap, Is.Not.Null, "the stored score should link to the seeded beatmap");
            BeatmapInfo resolvedBeatmap = linkedBeatmap!;

            Assert.Multiple(() =>
            {
                Assert.That(stored.ID, Is.EqualTo(expectedId));
                Assert.That(stored.DeletePending, Is.False);
                Assert.That(stored.Hash, Is.EqualTo(replayHash));
                Assert.That(stored.BeatmapHash, Is.EqualTo(beatmap.Hash));

                Assert.That(resolvedBeatmap.ID, Is.EqualTo(beatmap.ID));
                Assert.That(resolvedBeatmap.Hash, Is.EqualTo(beatmap.Hash));

                Assert.That(stored.Ruleset.ShortName, Is.EqualTo(OsuRuleset.SHORT_NAME));
                Assert.That(stored.Ruleset.CreateInstance(), Is.TypeOf<OsuRuleset>());

                Assert.That(stored.RealmUser.OnlineID, Is.EqualTo(APIUser.SYSTEM_USER_ID));
                Assert.That(stored.RealmUser.Username, Is.EqualTo("tester"));
                Assert.That(stored.RealmUser.CountryCode, Is.EqualTo(CountryCode.JP));

                Assert.That(stored.ClientVersion, Is.EqualTo("2026.1022.0"));
                Assert.That(stored.TotalScore, Is.EqualTo(987654));
                Assert.That(stored.TotalScoreWithoutMods, Is.EqualTo(900000));
                Assert.That(stored.TotalScoreVersion, Is.EqualTo(LegacyScoreEncoder.LATEST_VERSION));
                Assert.That(stored.LegacyTotalScore, Is.EqualTo(456789));
                Assert.That(stored.MaxCombo, Is.EqualTo(1234));
                Assert.That(stored.Accuracy, Is.EqualTo(0.9876).Within(1e-9));
                Assert.That(stored.Combo, Is.EqualTo(432));
                Assert.That(stored.RankInt, Is.EqualTo((int)ScoreRank.SH));
                Assert.That(stored.IsLegacyScore, Is.True);
                Assert.That(stored.OnlineID, Is.EqualTo(1234567));
                Assert.That(stored.LegacyOnlineID, Is.EqualTo(7654321));
                Assert.That(stored.PP, Is.EqualTo(234.5));
                Assert.That(stored.Date, Is.EqualTo(score_date));

                Assert.That(stored.Mods.Select(m => m.Acronym), Does.Contain("DT"));

                Assert.That(stored.Files, Has.Count.EqualTo(1));
                Assert.That(stored.Files[0].Filename, Is.EqualTo("replay.osr"));
                Assert.That(stored.Files[0].File.Hash, Is.EqualTo(replayHash));

                Assert.That(stored.Pauses, Is.EqualTo(new[] { 1000 }));
            });
        }

        private (BeatmapInfo beatmap, RulesetInfo ruleset) seedBeatmap()
        {
            string beatmapHash = newHash();

            realm.Write(r =>
            {
                RulesetInfo liveRuleset = r.Find<RulesetInfo>(OsuRuleset.SHORT_NAME)
                                          ?? throw new InvalidOperationException("the osu! ruleset was not seeded into realm");

                var set = new BeatmapSetInfo();

                var beatmapInfo = new BeatmapInfo(liveRuleset, new BeatmapDifficulty(), new BeatmapMetadata
                {
                    Title = "Test",
                    Artist = "Test"
                })
                {
                    DifficultyName = "Insane",
                    Hash = beatmapHash
                };

                set.Beatmaps.Add(beatmapInfo);
                beatmapInfo.BeatmapSet = set;

                r.Add(set);
            });

            return (realm.Run(r => r.All<BeatmapInfo>().First(b => b.Hash == beatmapHash).Detach()),
                realm.Run(r => r.Find<RulesetInfo>(OsuRuleset.SHORT_NAME)!.Clone()));
        }

        private ScoreInfo createScore(Guid id, IEnumerable<Mod>? mods = null)
        {
            var score = new ScoreInfo(beatmap, ruleset, new RealmUser
            {
                OnlineID = APIUser.SYSTEM_USER_ID,
                Username = "tester",
                CountryCode = CountryCode.JP
            })
            {
                ID = id,
                ClientVersion = "2026.1022.0",
                TotalScore = 987654,
                TotalScoreWithoutMods = 900000,
                TotalScoreVersion = LegacyScoreEncoder.LATEST_VERSION,
                LegacyTotalScore = 456789,
                MaxCombo = 1234,
                Accuracy = 0.9876,
                Combo = 432,
                Date = score_date,
                PP = 234.5,
                OnlineID = 1234567,
                LegacyOnlineID = 7654321,
                RankInt = (int)ScoreRank.SH,
                IsLegacyScore = true
            };

            score.Mods = (mods ?? new Mod[] { new OsuModDoubleTime() }).ToArray();

            score.Statistics = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = 300,
                [HitResult.Miss] = 2
            };
            score.StatisticsJson = JsonConvert.SerializeObject(score.Statistics);

            score.MaximumStatistics = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = 360
            };
            score.MaximumStatisticsJson = JsonConvert.SerializeObject(score.MaximumStatistics);

            score.Pauses.Add(1000);

            return score;
        }

        private static string newHash() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    }
}
