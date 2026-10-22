// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Models;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Database;
using osu.Game.Users;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class SqliteScoreStoreTest : RealmTest
    {
        private string databasePath = null!;

        [SetUp]
        public void SetUp()
        {
            databasePath = Path.Combine(Path.GetTempPath(), "g0v0-test", Guid.NewGuid().ToString("N"), "scores.db");
        }

        [Test]
        public void TestAddPersistsAllFieldsAcrossReopen()
        {
            RunTestWithRealm((realm, _) =>
            {
                var (ruleset, beatmap) = createBeatmap(realm);

                ScoreInfo score = createFullScore(ruleset, beatmap);
                ScoreInfo minimal = new ScoreInfo(ruleset: ruleset) { Hash = newHash() };

                using (var store = new SqliteScoreStore(databasePath, realm))
                {
                    store.Add(score);
                    store.Add(minimal);
                    Assert.That(store.GetAllDetached().Count, Is.EqualTo(2));
                }

                using (var reopened = new SqliteScoreStore(databasePath, realm))
                {
                    ScoreInfo? fetched = reopened.GetDetached(score.ID);

                    Assert.That(fetched, Is.Not.Null);
                    assertFullScoreMatches(score, fetched!);

                    Assert.That(fetched!.BeatmapInfo, Is.Not.Null);
                    Assert.That(fetched.BeatmapInfo!.ID, Is.EqualTo(beatmap.ID));
                    Assert.That(fetched.BeatmapInfo.Hash, Is.EqualTo(beatmap.Hash));
                    Assert.That(fetched.Ruleset.CreateInstance(), Is.TypeOf<OsuRuleset>());
                    Assert.That(fetched.Mods.Select(mod => mod.Acronym), Is.EquivalentTo(new[] { "DT" }));
                    Assert.That(fetched.Statistics[HitResult.Great], Is.EqualTo(300));
                    Assert.That(fetched.MaximumStatistics[HitResult.Great], Is.EqualTo(360));

                    List<ScoreInfo> all = reopened.GetAllDetached();

                    Assert.That(all.Count, Is.EqualTo(2));
                    Assert.That(all.Single(s => s.ID == score.ID).BeatmapInfo, Is.Not.Null);

                    ScoreInfo? minimalFetched = reopened.GetDetached(minimal.ID);

                    Assert.That(minimalFetched, Is.Not.Null);
                    Assert.That(minimalFetched!.PP, Is.Null);
                    Assert.That(minimalFetched.LegacyTotalScore, Is.Null);
                    Assert.That(minimalFetched.BeatmapInfo, Is.Null);
                    Assert.That(minimalFetched.Files, Is.Empty);
                    Assert.That(minimalFetched.Pauses, Is.Empty);
                }
            });
        }

        [Test]
        public void TestUpdatePersistsMutationsAndRollsBackOnException()
        {
            RunTestWithRealm((realm, _) =>
            {
                var (ruleset, beatmap) = createBeatmap(realm);
                ScoreInfo score = createFullScore(ruleset, beatmap);

                string updatedStatistics = JsonConvert.SerializeObject(new Dictionary<HitResult, int> { [HitResult.Great] = 1 });

                using (var store = new SqliteScoreStore(databasePath, realm))
                {
                    store.Add(score);

                    store.Update(score.ID, stored =>
                    {
                        stored.TotalScore = 42;
                        stored.TotalScoreWithoutMods = 41;
                        stored.TotalScoreVersion = 7;
                        stored.Accuracy = 0.5;
                        stored.RankInt = (int)ScoreRank.A;
                        stored.BackgroundReprocessingFailed = true;
                        stored.PP = null;
                        stored.MaximumStatisticsJson = updatedStatistics;

                        stored.Files.Clear();
                        stored.Pauses.Clear();
                        stored.Pauses.Add(99);
                    });

                    ScoreInfo? updated = store.GetDetached(score.ID);

                    Assert.That(updated, Is.Not.Null);
                    Assert.That(updated!.TotalScore, Is.EqualTo(42));
                    Assert.That(updated.PP, Is.Null);

                    bool invoked = false;

                    store.Update(Guid.NewGuid(), _ => invoked = true);
                    Assert.That(invoked, Is.False);

                    Assert.Throws<InvalidOperationException>(() => store.Update(score.ID, stored =>
                    {
                        stored.TotalScore = 1;
                        throw new InvalidOperationException("rollback");
                    }));
                }

                using (var reopened = new SqliteScoreStore(databasePath, realm))
                {
                    ScoreInfo? fetched = reopened.GetDetached(score.ID);

                    Assert.That(fetched, Is.Not.Null);
                    Assert.That(fetched!.TotalScore, Is.EqualTo(42));
                    Assert.That(fetched.TotalScoreWithoutMods, Is.EqualTo(41));
                    Assert.That(fetched.TotalScoreVersion, Is.EqualTo(7));
                    Assert.That(fetched.Accuracy, Is.EqualTo(0.5));
                    Assert.That(fetched.RankInt, Is.EqualTo((int)ScoreRank.A));
                    Assert.That(fetched.BackgroundReprocessingFailed, Is.True);
                    Assert.That(fetched.PP, Is.Null);
                    Assert.That(fetched.MaximumStatisticsJson, Is.EqualTo(updatedStatistics));
                    Assert.That(fetched.Files, Is.Empty);
                    Assert.That(fetched.Pauses, Is.EqualTo(new[] { 99 }));
                }
            });
        }

        [Test]
        public void TestSoftDeleteUndeleteAndLookups()
        {
            RunTestWithRealm((realm, _) =>
            {
                var (ruleset, beatmap) = createBeatmap(realm);

                ScoreInfo scoreA = createFullScore(ruleset, beatmap);
                scoreA.OnlineID = 100;
                scoreA.LegacyOnlineID = 200;

                ScoreInfo scoreB = createFullScore(ruleset, beatmap);
                scoreB.OnlineID = 101;
                scoreB.LegacyOnlineID = 201;

                ScoreInfo deletedDuplicate = createFullScore(ruleset, beatmap);
                deletedDuplicate.OnlineID = 102;
                deletedDuplicate.LegacyOnlineID = 202;
                deletedDuplicate.Hash = scoreA.Hash;
                deletedDuplicate.DeletePending = true;

                using var store = new SqliteScoreStore(databasePath, realm);

                store.Add(scoreA);
                store.Add(scoreB);
                store.Add(deletedDuplicate);

                // Deleted rows are included in hash lookups, with active rows preferred.
                Assert.That(store.FindByHash(scoreA.Hash)?.ID, Is.EqualTo(scoreA.ID));
                Assert.That(store.FindByOnlineId(100)?.ID, Is.EqualTo(scoreA.ID));
                Assert.That(store.FindByLegacyOnlineId(200)?.ID, Is.EqualTo(scoreA.ID));
                Assert.That(store.FindByLegacyOnlineId(202), Is.Null);

                Assert.That(store.FindByHash(string.Empty), Is.Null);
                Assert.That(store.GetForBeatmap(string.Empty), Is.Empty);

                Assert.That(store.Delete(scoreA.ID), Is.True);
                Assert.That(store.Delete(scoreA.ID), Is.False);

                Assert.That(store.FindByHash(scoreA.Hash)?.ID, Is.EqualTo(scoreA.ID));
                Assert.That(store.FindByHash(scoreA.Hash)!.DeletePending, Is.True);
                Assert.That(store.FindByOnlineId(100), Is.Null);
                Assert.That(store.FindByLegacyOnlineId(200), Is.Null);

                Assert.That(store.GetForBeatmap(beatmap.Hash).Select(s => s.ID), Is.EquivalentTo(new[] { scoreB.ID }));

                List<ScoreInfo> all = store.GetAllDetached();

                Assert.That(all.Count, Is.EqualTo(3));
                Assert.That(all.Single(s => s.ID == scoreA.ID).DeletePending, Is.True);
                Assert.That(all.Single(s => s.ID == scoreB.ID).DeletePending, Is.False);
                Assert.That(all.Single(s => s.ID == deletedDuplicate.ID).DeletePending, Is.True);

                Assert.That(store.Undelete(scoreA.ID), Is.True);
                Assert.That(store.Undelete(scoreA.ID), Is.False);
                Assert.That(store.FindByHash(scoreA.Hash)?.ID, Is.EqualTo(scoreA.ID));
                Assert.That(store.GetForBeatmap(beatmap.Hash).Count, Is.EqualTo(2));
            });
        }

        [Test]
        public void TestBeatmapRelationsAreResolvedFromLiveRealm()
        {
            RunTestWithRealm((realm, _) =>
            {
                var (ruleset, beatmap) = createBeatmap(realm);
                ScoreInfo score = createFullScore(ruleset, beatmap);

                using var store = new SqliteScoreStore(databasePath, realm);
                store.Add(score);

                ScoreInfo initial = store.GetDetached(score.ID)!;

                Assert.That(initial.BeatmapInfo, Is.Not.Null);
                Assert.That(initial.BeatmapInfo!.DifficultyName, Is.EqualTo("Insane"));

                Guid beatmapSetId = realm.Run(r => r.Find<BeatmapInfo>(beatmap.ID)!.BeatmapSet!.ID);

                realm.Write(r => r.Find<BeatmapInfo>(beatmap.ID)!.DifficultyName = "Edited");

                Assert.That(store.GetDetached(score.ID)!.BeatmapInfo!.DifficultyName, Is.EqualTo("Edited"));

                realm.Write(r => r.Find<BeatmapSetInfo>(beatmapSetId)!.DeletePending = true);

                ScoreInfo withDeletedSet = store.GetDetached(score.ID)!;

                Assert.That(withDeletedSet.BeatmapInfo, Is.Null);
                Assert.That(withDeletedSet.BeatmapHash, Is.EqualTo(beatmap.Hash));
                Assert.That(store.GetForBeatmap(beatmap.Hash).Single().BeatmapInfo, Is.Null);

                realm.Write(r => r.Find<BeatmapSetInfo>(beatmapSetId)!.DeletePending = false);

                Assert.That(store.GetDetached(score.ID)!.BeatmapInfo, Is.Not.Null);
                Assert.That(store.GetForBeatmap(beatmap.Hash).Single().BeatmapInfo!.Hash, Is.EqualTo(beatmap.Hash));
            });
        }

        [Test]
        public void TestSubscriptionNotifiesOnMutation()
        {
            RunTestWithRealm((realm, _) =>
            {
                var (ruleset, beatmap) = createBeatmap(realm);
                ScoreInfo score = createFullScore(ruleset, beatmap);

                using var store = new SqliteScoreStore(databasePath, realm);
                int notifications = 0;

                using (store.Subscribe(() => notifications++))
                {
                    Assert.That(notifications, Is.EqualTo(1));

                    store.Add(score);
                    Assert.That(notifications, Is.EqualTo(2));

                    store.Update(score.ID, stored => stored.TotalScore = 5);
                    Assert.That(notifications, Is.EqualTo(3));

                    store.Delete(score.ID);
                    Assert.That(notifications, Is.EqualTo(4));

                    Assert.That(store.Delete(score.ID), Is.False);
                    Assert.That(notifications, Is.EqualTo(4));

                    store.Undelete(score.ID);
                    Assert.That(notifications, Is.EqualTo(5));
                }

                store.Delete(score.ID);
                Assert.That(notifications, Is.EqualTo(5));
            });
        }

        [Test]
        public void TestRealmStoreAddResolvesExistingRelations()
        {
            RunTestWithRealm((realm, _) =>
            {
                var (ruleset, beatmap) = createBeatmap(realm);

                string existingFileHash = newHash();
                realm.Write(r => r.Add(new RealmFile { Hash = existingFileHash }));

                var store = new RealmScoreStore(realm);

                RulesetInfo detachedRuleset = realm.Run(r => r.Find<RulesetInfo>(ruleset.ShortName)!.Detach());
                BeatmapInfo detachedBeatmap = realm.Run(r => r.Find<BeatmapInfo>(beatmap.ID)!.Detach());

                var score = new ScoreInfo(detachedBeatmap, detachedRuleset) { Hash = newHash() };
                score.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = existingFileHash }, "replay.osr"));

                Assert.DoesNotThrow(() => store.Add(score));

                Assert.That(score.IsManaged, Is.True);
                Assert.That(score.Ruleset.IsManaged, Is.True);
                Assert.That(score.BeatmapInfo, Is.Not.Null);
                Assert.That(score.BeatmapInfo!.IsManaged, Is.True);
                Assert.That(score.Files.Single().File.IsManaged, Is.True);

                Assert.That(realm.Run(r => r.All<RulesetInfo>().Count()), Is.EqualTo(1));
                Assert.That(realm.Run(r => r.All<BeatmapInfo>().Count()), Is.EqualTo(1));
                Assert.That(realm.Run(r => r.All<RealmFile>().Count()), Is.EqualTo(1));

                var missingBeatmap = new BeatmapInfo(detachedRuleset) { Hash = newHash() };
                var unresolvedScore = new ScoreInfo(missingBeatmap, detachedRuleset) { Hash = newHash() };

                Assert.DoesNotThrow(() => store.Add(unresolvedScore));
                Assert.That(unresolvedScore.BeatmapInfo, Is.Null);
                Assert.That(unresolvedScore.BeatmapHash, Is.EqualTo(missingBeatmap.Hash));
            });
        }

        private static (RulesetInfo ruleset, BeatmapInfo beatmap) createBeatmap(RealmAccess realm)
        {
            var ruleset = new RulesetInfo("osu", "osu!", typeof(OsuRuleset).AssemblyQualifiedName!, 0)
            {
                Available = true,
                LastAppliedDifficultyVersion = 42
            };

            var beatmap = new BeatmapInfo(ruleset, new BeatmapDifficulty(), new BeatmapMetadata
            {
                Title = "Test",
                Artist = "Test"
            })
            {
                DifficultyName = "Insane",
                Hash = newHash()
            };

            var set = new BeatmapSetInfo();
            set.Beatmaps.Add(beatmap);
            beatmap.BeatmapSet = set;

            realm.Write(r => r.Add(set));

            return (realm.Run(r => r.Find<RulesetInfo>(ruleset.ShortName)!.Detach()),
                realm.Run(r => r.Find<BeatmapInfo>(beatmap.ID)!.Detach()));
        }

        private static ScoreInfo createFullScore(RulesetInfo ruleset, BeatmapInfo beatmap)
        {
            var score = new ScoreInfo(beatmap, ruleset, new RealmUser
            {
                OnlineID = 42,
                Username = "tester",
                CountryCode = CountryCode.JP
            })
            {
                ClientVersion = "2026.1.0",
                Hash = newHash(),
                TotalScore = 987654,
                TotalScoreWithoutMods = 900000,
                TotalScoreVersion = 123,
                LegacyTotalScore = 456789,
                BackgroundReprocessingFailed = false,
                MaxCombo = 1234,
                Accuracy = 0.9876,
                Date = new DateTimeOffset(2026, 10, 22, 12, 34, 56, TimeSpan.FromHours(8)),
                PP = 234.5,
                OnlineID = 1234567,
                LegacyOnlineID = 7654321,
                RankInt = (int)ScoreRank.SH,
                Combo = 432,
                IsLegacyScore = true
            };

            score.Mods = new Mod[] { new OsuModDoubleTime() };

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
            score.Pauses.Add(2000);

            score.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = newHash() }, "replay.osr"));

            return score;
        }

        private static void assertFullScoreMatches(ScoreInfo expected, ScoreInfo actual)
        {
            Assert.Multiple(() =>
            {
                Assert.That(actual.ID, Is.EqualTo(expected.ID));
                Assert.That(actual.BeatmapHash, Is.EqualTo(expected.BeatmapHash));
                Assert.That(actual.ClientVersion, Is.EqualTo(expected.ClientVersion));
                Assert.That(actual.Hash, Is.EqualTo(expected.Hash));
                Assert.That(actual.DeletePending, Is.EqualTo(expected.DeletePending));
                Assert.That(actual.TotalScore, Is.EqualTo(expected.TotalScore));
                Assert.That(actual.TotalScoreWithoutMods, Is.EqualTo(expected.TotalScoreWithoutMods));
                Assert.That(actual.TotalScoreVersion, Is.EqualTo(expected.TotalScoreVersion));
                Assert.That(actual.LegacyTotalScore, Is.EqualTo(expected.LegacyTotalScore));
                Assert.That(actual.BackgroundReprocessingFailed, Is.EqualTo(expected.BackgroundReprocessingFailed));
                Assert.That(actual.MaxCombo, Is.EqualTo(expected.MaxCombo));
                Assert.That(actual.Accuracy, Is.EqualTo(expected.Accuracy));
                Assert.That(actual.Date, Is.EqualTo(expected.Date));
                Assert.That(actual.Date.Offset, Is.EqualTo(expected.Date.Offset));
                Assert.That(actual.PP, Is.EqualTo(expected.PP));
                Assert.That(actual.OnlineID, Is.EqualTo(expected.OnlineID));
                Assert.That(actual.LegacyOnlineID, Is.EqualTo(expected.LegacyOnlineID));
                Assert.That(actual.RankInt, Is.EqualTo(expected.RankInt));
                Assert.That(actual.Combo, Is.EqualTo(expected.Combo));
                Assert.That(actual.IsLegacyScore, Is.EqualTo(expected.IsLegacyScore));

                Assert.That(actual.Ruleset.ShortName, Is.EqualTo(expected.Ruleset.ShortName));
                Assert.That(actual.Ruleset.Name, Is.EqualTo(expected.Ruleset.Name));
                Assert.That(actual.Ruleset.InstantiationInfo, Is.EqualTo(expected.Ruleset.InstantiationInfo));
                Assert.That(actual.Ruleset.OnlineID, Is.EqualTo(expected.Ruleset.OnlineID));
                Assert.That(actual.Ruleset.LastAppliedDifficultyVersion, Is.EqualTo(expected.Ruleset.LastAppliedDifficultyVersion));
                Assert.That(actual.Ruleset.Available, Is.EqualTo(expected.Ruleset.Available));

                Assert.That(actual.RealmUser.OnlineID, Is.EqualTo(expected.RealmUser.OnlineID));
                Assert.That(actual.RealmUser.Username, Is.EqualTo(expected.RealmUser.Username));
                Assert.That(actual.RealmUser.CountryString, Is.EqualTo(expected.RealmUser.CountryString));

                Assert.That(actual.ModsJson, Is.EqualTo(expected.ModsJson));
                Assert.That(actual.StatisticsJson, Is.EqualTo(expected.StatisticsJson));
                Assert.That(actual.MaximumStatisticsJson, Is.EqualTo(expected.MaximumStatisticsJson));

                Assert.That(actual.Files.Count, Is.EqualTo(1));
                Assert.That(actual.Files[0].File.Hash, Is.EqualTo(expected.Files[0].File.Hash));
                Assert.That(actual.Files[0].Filename, Is.EqualTo(expected.Files[0].Filename));

                Assert.That(actual.Pauses, Is.EqualTo(expected.Pauses));
            });
        }

        private static string newHash() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    }
}
