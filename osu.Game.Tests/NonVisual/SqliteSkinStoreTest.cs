// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.Models;
using osu.Game.Skinning;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class SqliteSkinStoreTest : RealmTest
    {
        private string databasePath = null!;

        [SetUp]
        public void SetUp()
        {
            databasePath = Path.Combine(Path.GetTempPath(), "g0v0-test", $"{Guid.NewGuid():N}", "skins.db");
        }

        [Test]
        public void TestAddPersistsAllFieldsAcrossReopen()
        {
            SkinInfo skin = createSkin();

            using (var store = new SqliteSkinStore(databasePath))
            {
                Assert.That(store.FileStorageDirectory, Is.EqualTo("skin-files"));

                store.Add(skin);

                assertSkinMatches(skin, store.GetDetached(skin.ID));
                Assert.That(store.GetAllDetached().Select(s => s.ID), Is.EqualTo(new[] { skin.ID }));
                Assert.That(store.GetAllUsableDetached().Select(s => s.ID), Is.EqualTo(new[] { skin.ID }));
                Assert.That(store.FindByHash(skin.Hash)?.ID, Is.EqualTo(skin.ID));
                Assert.That(store.GetDetached(Guid.NewGuid()), Is.Null);
            }

            using (var reopened = new SqliteSkinStore(databasePath))
            {
                assertSkinMatches(skin, reopened.GetDetached(skin.ID));
                Assert.That(reopened.GetAllDetached().Count, Is.EqualTo(1));
            }
        }

        [Test]
        public void TestUpdateReplacesFilesAndPreservesID()
        {
            SkinInfo skin = createSkin();
            Guid originalId = skin.ID;
            Guid attemptedId = Guid.NewGuid();

            void assertUpdated(SkinInfo? updated)
            {
                Assert.That(updated, Is.Not.Null);

                Assert.Multiple(() =>
                {
                    Assert.That(updated!.ID, Is.EqualTo(originalId));
                    Assert.That(updated.Name, Is.EqualTo("updated name"));
                    Assert.That(updated.Creator, Is.EqualTo("updated creator"));
                    Assert.That(updated.InstantiationInfo, Is.EqualTo("updated instantiation"));
                    Assert.That(updated.Hash, Is.EqualTo("updated hash"));
                    Assert.That(updated.Protected, Is.True);
                    Assert.That(updated.DeletePending, Is.False);
                    Assert.That(updated.Files.Select(describeFile), Is.EqualTo(new[]
                    {
                        "first.png:first",
                        "second.png:second",
                        "duplicate.png:first"
                    }));
                });
            }

            using (var store = new SqliteSkinStore(databasePath))
            {
                store.Add(skin);

                store.Update(originalId, stored =>
                {
                    // Attempting to change the primary key must not re-key the stored entry.
                    stored.ID = attemptedId;
                    stored.Name = "updated name";
                    stored.Creator = "updated creator";
                    stored.InstantiationInfo = "updated instantiation";
                    stored.Hash = "updated hash";
                    stored.Protected = true;

                    stored.Files.Clear();
                    stored.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = "first" }, "first.png"));
                    stored.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = "second" }, "second.png"));
                    // Duplicate content hash under a different filename must be preserved.
                    stored.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = "first" }, "duplicate.png"));
                });

                assertUpdated(store.GetDetached(originalId));
                Assert.That(store.GetDetached(attemptedId), Is.Null);
                Assert.That(store.GetAllDetached().Count, Is.EqualTo(1));
                Assert.That(store.FindByHash("updated hash")?.ID, Is.EqualTo(originalId));
                Assert.That(store.FindByHash(skin.Hash), Is.Null);
            }

            using (var reopened = new SqliteSkinStore(databasePath))
            {
                assertUpdated(reopened.GetDetached(originalId));
                Assert.That(reopened.GetDetached(attemptedId), Is.Null);
            }
        }

        [Test]
        public void TestUpdateRollsBackOnException()
        {
            SkinInfo skin = createSkin();

            using (var store = new SqliteSkinStore(databasePath))
            {
                store.Add(skin);

                Assert.Throws<InvalidOperationException>(() => store.Update(skin.ID, stored =>
                {
                    stored.Name = "rolled back";
                    stored.Hash = "rolled back";
                    stored.Files.Clear();
                    throw new InvalidOperationException("rollback");
                }));

                assertSkinMatches(skin, store.GetDetached(skin.ID));

                bool invoked = false;
                store.Update(Guid.NewGuid(), _ => invoked = true);
                Assert.That(invoked, Is.False);
                Assert.That(store.GetAllDetached().Count, Is.EqualTo(1));
            }

            using (var reopened = new SqliteSkinStore(databasePath))
                assertSkinMatches(skin, reopened.GetDetached(skin.ID));
        }

        [Test]
        public void TestProtectedSkinsCannotBeDeleted()
        {
            SkinInfo protectedSkin = createSkin("protected skin", isProtected: true);
            SkinInfo normalSkin = createSkin("normal skin");

            using var store = new SqliteSkinStore(databasePath);

            store.Add(protectedSkin);
            store.Add(normalSkin);

            Assert.That(store.Delete(protectedSkin.ID), Is.False);
            Assert.That(store.GetDetached(protectedSkin.ID)!.DeletePending, Is.False);
            Assert.That(store.GetAllUsableDetached().Select(s => s.ID), Is.EqualTo(new[] { normalSkin.ID }));

            Assert.That(store.Delete(normalSkin.ID), Is.True);
            Assert.That(store.Delete(normalSkin.ID), Is.False);
            Assert.That(store.GetDetached(normalSkin.ID)!.DeletePending, Is.True);
            Assert.That(store.GetAllUsableDetached(), Is.Empty);

            Assert.That(store.Undelete(normalSkin.ID), Is.True);
            Assert.That(store.Undelete(normalSkin.ID), Is.False);
            Assert.That(store.GetAllUsableDetached().Select(s => s.ID), Is.EqualTo(new[] { normalSkin.ID }));

            Assert.That(store.Delete(Guid.NewGuid()), Is.False);
            Assert.That(store.Undelete(Guid.NewGuid()), Is.False);
        }

        [Test]
        public void TestDuplicateHashesArePreservedAndLookupPrefersActive()
        {
            SkinInfo active = createSkin("active");
            active.Hash = "shared hash";

            SkinInfo activeDuplicate = createSkin("active duplicate");
            activeDuplicate.Hash = active.Hash;

            SkinInfo deleted = createSkin("deleted");
            deleted.Hash = active.Hash;
            deleted.DeletePending = true;

            SkinInfo duplicateFiles = createSkin("duplicate files");
            string duplicateFileHash = duplicateFiles.Files[0].File.Hash;
            duplicateFiles.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = duplicateFileHash }, "copy.ini"));

            using (var store = new SqliteSkinStore(databasePath))
            {
                store.Add(active);
                store.Add(activeDuplicate);
                store.Add(deleted);
                store.Add(duplicateFiles);

                Assert.That(store.FindByHash(active.Hash)?.ID, Is.EqualTo(active.ID));
                Assert.That(store.GetAllDetached().Count, Is.EqualTo(4));

                // Removing the first active entry falls back to the other active entry with the same hash.
                Assert.That(store.Delete(active.ID), Is.True);
                Assert.That(store.FindByHash(active.Hash)?.ID, Is.EqualTo(activeDuplicate.ID));

                // With every entry deleted, a deleted entry is still returned for import reuse.
                Assert.That(store.Delete(activeDuplicate.ID), Is.True);
                SkinInfo? deletedMatch = store.FindByHash(active.Hash);
                Assert.That(deletedMatch, Is.Not.Null);
                Assert.That(deletedMatch!.DeletePending, Is.True);

                Assert.That(store.Undelete(active.ID), Is.True);
                Assert.That(store.FindByHash(active.Hash)?.ID, Is.EqualTo(active.ID));

                Assert.That(store.FindByHash(string.Empty), Is.Null);
                Assert.That(store.FindByHash("missing hash"), Is.Null);

                Assert.That(store.GetDetached(duplicateFiles.ID)!.Files.Select(describeFile), Is.EqualTo(new[]
                {
                    $"skin.ini:{duplicateFileHash}",
                    $"hitcircle.png:{duplicateFiles.Files[1].File.Hash}",
                    $"copy.ini:{duplicateFileHash}"
                }));
            }

            using (var reopened = new SqliteSkinStore(databasePath))
            {
                List<SkinInfo> all = reopened.GetAllDetached();

                Assert.That(all.Count, Is.EqualTo(4));

                SkinInfo fetched = all.Single(s => s.ID == duplicateFiles.ID);

                Assert.That(fetched.Files.Select(f => f.File.Hash).Count(h => h == duplicateFileHash), Is.EqualTo(2));
                Assert.That(fetched.Files[2].Filename, Is.EqualTo("copy.ini"));
            }
        }

        [Test]
        public void TestSubscriptionNotifiesOnMutation()
        {
            SkinInfo skin = createSkin();
            SkinInfo protectedSkin = createSkin("protected skin", isProtected: true);

            using var store = new SqliteSkinStore(databasePath);

            int notifications = 0;

            using (store.Subscribe(() => notifications++))
            {
                Assert.That(notifications, Is.EqualTo(1));

                store.Add(skin);
                Assert.That(notifications, Is.EqualTo(2));

                store.Add(protectedSkin);
                Assert.That(notifications, Is.EqualTo(3));

                store.Update(skin.ID, stored => stored.Name = "renamed");
                Assert.That(notifications, Is.EqualTo(4));

                Assert.That(store.Delete(protectedSkin.ID), Is.False);
                Assert.That(notifications, Is.EqualTo(4));

                store.Delete(skin.ID);
                Assert.That(notifications, Is.EqualTo(5));

                Assert.That(store.Delete(skin.ID), Is.False);
                Assert.That(notifications, Is.EqualTo(5));

                store.Undelete(skin.ID);
                Assert.That(notifications, Is.EqualTo(6));
            }

            store.Delete(skin.ID);
            Assert.That(notifications, Is.EqualTo(6));
        }

        [Test]
        public void TestRealmStoreResolvesFilesAndRejectsProtectedDeletion()
        {
            RunTestWithRealm((realm, _) =>
            {
                realm.Write(r =>
                {
                    r.Add(new RealmFile { Hash = "existing-file" });
                    r.Add(new RealmFile { Hash = "second-existing-file" });
                });

                var skin = new SkinInfo("realm skin", "realm creator", string.Empty) { Hash = "realm hash" };
                skin.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = "existing-file" }, "skin.ini"));
                skin.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = "second-existing-file" }, "hitcircle.png"));

                var store = new RealmSkinStore(realm);

                Assert.That(store.FileStorageDirectory, Is.EqualTo("files"));

                store.Add(skin);

                Assert.That(skin.IsManaged, Is.True);

                realm.Run(r =>
                {
                    SkinInfo? stored = r.Find<SkinInfo>(skin.ID);

                    Assert.That(stored, Is.Not.Null);
                    Assert.That(stored!.Files.Select(describeFile), Is.EqualTo(new[]
                    {
                        "skin.ini:existing-file",
                        "hitcircle.png:second-existing-file"
                    }));
                    Assert.That(stored.Files[0].File.IsManaged, Is.True);
                    Assert.That(stored.Files[1].File.IsManaged, Is.True);
                    Assert.That(r.All<RealmFile>().Count(), Is.EqualTo(2));
                });

                Assert.That(store.FindByHash("realm hash")?.ID, Is.EqualTo(skin.ID));

                store.Update(skin.ID, stored => stored.Protected = true);

                Assert.That(store.Delete(skin.ID), Is.False);
                Assert.That(store.GetDetached(skin.ID)!.DeletePending, Is.False);
                Assert.That(store.GetAllUsableDetached().Any(s => s.ID == skin.ID), Is.False);

                store.Update(skin.ID, stored => stored.Protected = false);

                Assert.That(store.Delete(skin.ID), Is.True);
                Assert.That(store.GetAllUsableDetached().Any(s => s.ID == skin.ID), Is.False);
                Assert.That(store.GetAllDetached().Any(s => s.ID == skin.ID), Is.True);

                Assert.That(store.Undelete(skin.ID), Is.True);
                Assert.That(store.GetAllUsableDetached().Any(s => s.ID == skin.ID), Is.True);
            });
        }

        private static SkinInfo createSkin(string name = "test skin", bool isProtected = false)
        {
            var skin = new SkinInfo(name, "test creator", "osu.Game.Skinning.LegacySkin, osu.Game")
            {
                Hash = newHash(),
                Protected = isProtected
            };

            skin.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = newHash() }, "skin.ini"));
            skin.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = newHash() }, "hitcircle.png"));

            return skin;
        }

        private static void assertSkinMatches(SkinInfo expected, SkinInfo? actual)
        {
            Assert.That(actual, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(actual!.ID, Is.EqualTo(expected.ID));
                Assert.That(actual.Name, Is.EqualTo(expected.Name));
                Assert.That(actual.Creator, Is.EqualTo(expected.Creator));
                Assert.That(actual.InstantiationInfo, Is.EqualTo(expected.InstantiationInfo));
                Assert.That(actual.Hash, Is.EqualTo(expected.Hash));
                Assert.That(actual.Protected, Is.EqualTo(expected.Protected));
                Assert.That(actual.DeletePending, Is.EqualTo(expected.DeletePending));
                Assert.That(actual.Files.Select(describeFile), Is.EqualTo(expected.Files.Select(describeFile)));
            });
        }

        private static string describeFile(RealmNamedFileUsage usage) => $"{usage.Filename}:{usage.File.Hash}";

        private static string newHash() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    }
}
