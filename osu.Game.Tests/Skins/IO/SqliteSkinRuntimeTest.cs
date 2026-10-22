// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Extensions;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Models;
using osu.Game.Skinning;
using osu.Game.Skinning.Components;
using osu.Game.Tests.Resources;
using osuTK;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Writers.Zip;

namespace osu.Game.Tests.Skins.IO
{
    /// <summary>
    /// End-to-end coverage for skins backed by <see cref="SqliteSkinStore"/> through a real headless game.
    /// <see cref="ImportSkinTest"/> intentionally remains Realm-only coverage.
    /// </summary>
    [TestFixture]
    public class SqliteSkinRuntimeTest : ImportTest
    {
        private const string file_storage_directory = "skin-files";

        [Test]
        public async Task TestImportRenameExportAndReopen()
        {
            byte[] imageBytes = loadResourceBytes("Textures/test-image.png");
            Guid skinId;
            string hostName;

            using (var host = new SqliteHeadlessGameHost(bypassCleanupOnDispose: true))
            {
                hostName = host.Name;

                try
                {
                    var osu = LoadOsuIntoHost(host);
                    var skinManager = osu.Dependencies.Get<SkinManager>();
                    Storage storage = osu.Dependencies.Get<Storage>();

                    Assert.That(skinManager.Store, Is.TypeOf<SqliteSkinStore>());

                    var imported = await importSkin(osu, "sqlite runtime", "tester", ("runtime.png", imageBytes));
                    skinId = imported.ID;

                    imported.PerformRead(s =>
                    {
                        Assert.That(s.Name, Is.EqualTo("sqlite runtime"));
                        Assert.That(s.Creator, Is.EqualTo("tester"));
                        Assert.That(s.Files.Select(f => f.Filename), Is.EquivalentTo(new[] { "skin.ini", "runtime.png" }));
                        Assert.That(s.Hash, Is.Not.Empty);
                    });

                    skinManager.CurrentSkinInfo.Value = imported;

                    Assert.That(skinManager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(skinId));
                    Assert.That(skinManager.CurrentSkin.Value, Is.TypeOf<LegacySkin>());
                    Assert.That(hasTexture(host, skinManager.CurrentSkin.Value, "runtime"), Is.True);

                    skinManager.Rename(imported, "renamed runtime");

                    imported.PerformRead(s =>
                    {
                        Assert.That(s.Name, Is.EqualTo("renamed runtime"));
                        Assert.That(Encoding.UTF8.GetString(readStoredFile(storage, s, "skin.ini")), Does.Contain("Name: renamed runtime"));
                        Assert.That(readStoredFile(storage, s, "runtime.png"), Is.EqualTo(imageBytes));
                    });

                    // The SQLite skin must not be visible to Realm, and Realm's file cleanup must not
                    // delete content that only the SQLite store references.
                    osu.Realm.Run(r => Assert.That(r.Find<SkinInfo>(skinId), Is.Null));

                    new RealmFileStore(osu.Realm, storage).Cleanup();

                    osu.Realm.Run(r => Assert.That(r.Find<SkinInfo>(skinId), Is.Null));
                    imported.PerformRead(s => Assert.That(readStoredFile(storage, s, "runtime.png"), Is.EqualTo(imageBytes)));

                    using (var exportStream = new MemoryStream())
                    {
                        await new LegacySkinExporter(storage, file_storage_directory).ExportToStreamAsync(imported, exportStream);

                        var exportedFiles = readArchive(exportStream.ToArray());

                        Assert.That(exportedFiles.Keys, Is.EquivalentTo(new[] { "skin.ini", "runtime.png" }));
                        Assert.That(Encoding.UTF8.GetString(exportedFiles["skin.ini"]), Does.Contain("Name: renamed runtime"));
                        Assert.That(exportedFiles["runtime.png"], Is.EqualTo(imageBytes));
                    }
                }
                finally
                {
                    host.Exit();
                }
            }

            // Reuse the first host's storage and its persisted configuration.
            using (HeadlessGameHost host = new TestRunHeadlessGameHost(hostName))
            {
                try
                {
                    var osu = LoadOsuIntoHost(host);
                    var skinManager = osu.Dependencies.Get<SkinManager>();

                    Assert.That(skinManager.Store, Is.TypeOf<SqliteSkinStore>());

                    SkinInfo? stored = skinManager.Store.GetDetached(skinId);

                    Assert.That(stored, Is.Not.Null);
                    Assert.That(stored!.Name, Is.EqualTo("renamed runtime"));

                    Live<SkinInfo>? reloaded = skinManager.Query(s => s.ID == skinId);

                    Assert.That(reloaded, Is.Not.Null);
                    skinManager.CurrentSkinInfo.Value = reloaded!;

                    Assert.That(skinManager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(skinId));
                    Assert.That(hasTexture(host, skinManager.CurrentSkin.Value, "runtime"), Is.True);
                    Assert.That(skinManager.CurrentSkin.Value.Configuration.SkinInfo.Name, Is.EqualTo("renamed runtime"));
                }
                finally
                {
                    host.Exit();
                }
            }
        }

        [Test]
        public void TestAddFileSaveLayoutAndMappingRefresh()
        {
            using (var host = new SqliteHeadlessGameHost())
            {
                try
                {
                    var osu = LoadOsuIntoHost(host);
                    var skinManager = osu.Dependencies.Get<SkinManager>();
                    Storage storage = osu.Dependencies.Get<Storage>();

                    Assert.That(skinManager.Store, Is.TypeOf<SqliteSkinStore>());

                    skinManager.CurrentSkinInfo.Value = skinManager.DefaultClassicSkin.SkinInfo;

                    Assert.That(skinManager.EnsureMutableSkin(), Is.True);

                    Skin skin = skinManager.CurrentSkin.Value;
                    Live<SkinInfo> live = skin.SkinInfo;

                    byte[] imageBytes = loadResourceBytes("Textures/test-image.png");
                    string imageHash = computeHash(imageBytes);

                    // Mirrors the SkinEditor drag-and-drop flow: the store-backed live is read to get
                    // a snapshot and SkinManager.AddFile persists the new mapping.
                    live.PerformRead(s =>
                    {
                        using var contents = new MemoryStream(imageBytes);
                        skinManager.AddFile(s, contents, "runtime.png");
                    });

                    live.PerformRead(s =>
                    {
                        RealmNamedFileUsage? added = s.GetFile("runtime.png");

                        Assert.That(added, Is.Not.Null);
                        Assert.That(added!.File.Hash, Is.EqualTo(imageHash));
                        Assert.That(readStoredFile(storage, s, "runtime.png"), Is.EqualTo(imageBytes));
                    });

                    // The store subscription backing the skin's resource lookup must refresh immediately.
                    Assert.That(hasTexture(host, skin, "runtime"), Is.True);

                    skin.LayoutInfos[GlobalSkinnableContainers.MainHUDComponents] = new SkinLayoutInfo
                    {
                        DrawableInfo =
                        {
                            ["global"] = new[]
                            {
                                new SerialisedDrawableInfo
                                {
                                    Type = typeof(TextElement),
                                    Position = new Vector2(12, 34),
                                },
                            },
                        },
                    };

                    Assert.That(skinManager.Save(skin), Is.True);

                    live.PerformRead(s =>
                    {
                        Assert.That(s.GetFile("skininfo.json"), Is.Not.Null);
                        Assert.That(s.GetFile("MainHUDComponents.json"), Is.Not.Null);
                        Assert.That(readStoredFile(storage, s, "runtime.png"), Is.EqualTo(imageBytes));
                    });

                    Skin reloadedSkin = skinManager.GetSkin(live.Value);

                    Assert.That(reloadedSkin.LayoutInfos.TryGetValue(GlobalSkinnableContainers.MainHUDComponents, out SkinLayoutInfo? layout), Is.True);
                    Assert.That(layout!.TryGetDrawableInfo(null, out SerialisedDrawableInfo[]? drawables), Is.True);
                    Assert.That(drawables!, Has.Length.EqualTo(1));
                    Assert.That(drawables![0].Type, Is.EqualTo(typeof(TextElement)));
                    Assert.That(drawables[0].Position, Is.EqualTo(new Vector2(12, 34)));
                    Assert.That(hasTexture(host, reloadedSkin, "runtime"), Is.True);

                    // Content isolation: Realm cleanup must not touch SQLite-owned skin files or metadata.
                    osu.Realm.Run(r => Assert.That(r.Find<SkinInfo>(live.ID), Is.Null));

                    new RealmFileStore(osu.Realm, storage).Cleanup();

                    live.PerformRead(s => Assert.That(readStoredFile(storage, s, "runtime.png"), Is.EqualTo(imageBytes)));
                    Assert.That(hasTexture(host, skinManager.GetSkin(live.Value), "runtime"), Is.True);

                    // The store must remain usable through a second connection while the game is running.
                    string databasePath = storage.GetFullPath("skins.db");
                    string expectedLayoutHash = live.PerformRead(s => s.GetFile("MainHUDComponents.json")!.File.Hash);

                    using (var reopenedStore = new SqliteSkinStore(databasePath))
                    {
                        SkinInfo? reopened = reopenedStore.GetDetached(live.ID);

                        Assert.That(reopened, Is.Not.Null);
                        Assert.That(reopened!.Files.Select(f => f.Filename), Is.EquivalentTo(new[]
                        {
                            "skin.ini",
                            "runtime.png",
                            "skininfo.json",
                            "MainHUDComponents.json",
                        }));
                        Assert.That(reopened.GetFile("MainHUDComponents.json")!.File.Hash, Is.EqualTo(expectedLayoutHash));

                        reopenedStore.Update(live.ID, s => s.Name = "runtime store reopened");

                        Assert.That(skinManager.Store.GetDetached(live.ID)!.Name, Is.EqualTo("runtime store reopened"));
                    }
                }
                finally
                {
                    host.Exit();
                }
            }
        }

        [Test]
        public async Task TestDeleteRestoreAndDuplicateImport()
        {
            using (var host = new SqliteHeadlessGameHost())
            {
                try
                {
                    var osu = LoadOsuIntoHost(host);
                    var skinManager = osu.Dependencies.Get<SkinManager>();

                    Assert.That(skinManager.Store, Is.TypeOf<SqliteSkinStore>());

                    byte[] imageBytes = loadResourceBytes("Textures/test-image.png");

                    var imported = await importSkin(osu, "duplicate runtime", "tester", ("runtime.png", imageBytes));
                    Guid skinId = imported.ID;

                    SkinInfo detached = imported.PerformRead(s => s.Detach());

                    Assert.That(skinManager.Delete(detached), Is.True);
                    Assert.That(skinManager.Store.GetDetached(skinId)!.DeletePending, Is.True);
                    Assert.That(skinManager.Store.GetAllUsableDetached().Any(s => s.ID == skinId), Is.False);
                    Assert.That(skinManager.Delete(detached), Is.False);

                    // Re-importing identical content must reuse and restore the soft-deleted row.
                    var reimported = await importSkin(osu, "duplicate runtime", "tester", ("runtime.png", imageBytes));

                    Assert.That(reimported.ID, Is.EqualTo(skinId));
                    reimported.PerformRead(s =>
                    {
                        Assert.That(s.DeletePending, Is.False);
                        Assert.That(s.Name, Is.EqualTo("duplicate runtime"));
                    });
                    Assert.That(skinManager.Store.GetAllDetached().Count(s => s.ID == skinId), Is.EqualTo(1));

                    // The explicit undelete path must also work after the row was deleted again.
                    SkinInfo restored = reimported.PerformRead(s => s.Detach());

                    Assert.That(skinManager.Delete(restored), Is.True);
                    skinManager.Undelete(restored);

                    Assert.That(skinManager.Store.GetDetached(skinId)!.DeletePending, Is.False);
                    Assert.That(skinManager.Store.GetAllUsableDetached().Any(s => s.ID == skinId), Is.True);

                    skinManager.CurrentSkinInfo.Value = reimported;

                    Assert.That(skinManager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(skinId));
                    Assert.That(hasTexture(host, skinManager.CurrentSkin.Value, "runtime"), Is.True);
                }
                finally
                {
                    host.Exit();
                }
            }
        }

        [Test]
        public async Task TestExternalEditUpdatesSameSkin()
        {
            using (var host = new SqliteHeadlessGameHost())
            {
                try
                {
                    var osu = LoadOsuIntoHost(host);
                    var skinManager = osu.Dependencies.Get<SkinManager>();
                    Storage storage = osu.Dependencies.Get<Storage>();

                    Assert.That(skinManager.Store, Is.TypeOf<SqliteSkinStore>());

                    byte[] originalImageBytes = loadResourceBytes("Textures/test-image.png");
                    byte[] replacementImageBytes = loadResourceBytes("special-skin/scorebar-bg.png");
                    byte[] extraImageBytes = loadResourceBytes("special-skin/scorebar-marker.png");

                    var imported = await importSkin(osu, "external runtime", "tester", ("runtime.png", originalImageBytes));
                    Guid skinId = imported.ID;
                    string originalHash = imported.PerformRead(s => s.Hash);

                    ExternalEditOperation<SkinInfo> edit = await skinManager.BeginExternalEditing(imported.PerformRead(s => s.Detach()));

                    try
                    {
                        await File.WriteAllTextAsync(Path.Combine(edit.MountedPath, "skin.ini"), generateSkinIni("external edited", "editor"), new UTF8Encoding(false));
                        await File.WriteAllBytesAsync(Path.Combine(edit.MountedPath, "runtime.png"), replacementImageBytes);
                        await File.WriteAllBytesAsync(Path.Combine(edit.MountedPath, "extra.png"), extraImageBytes);

                        Live<SkinInfo>? updated = await edit.Finish();

                        Assert.That(updated, Is.Not.Null);
                        Assert.That(updated!.ID, Is.EqualTo(skinId));

                        updated.PerformRead(s =>
                        {
                            Assert.That(s.Name, Is.EqualTo("external edited"));
                            Assert.That(s.Creator, Is.EqualTo("editor"));
                            Assert.That(s.Hash, Is.Not.EqualTo(originalHash));
                            Assert.That(s.Files.Select(f => f.Filename), Is.EquivalentTo(new[] { "skin.ini", "runtime.png", "extra.png" }));
                            Assert.That(readStoredFile(storage, s, "runtime.png"), Is.EqualTo(replacementImageBytes));
                            Assert.That(readStoredFile(storage, s, "extra.png"), Is.EqualTo(extraImageBytes));
                        });

                        Skin reloadedSkin = skinManager.GetSkin(updated.Value);

                        Assert.That(reloadedSkin.Configuration.SkinInfo.Name, Is.EqualTo("external edited"));
                        Assert.That(hasTexture(host, reloadedSkin, "extra"), Is.True);
                        Assert.That(skinManager.Store.GetDetached(skinId), Is.Not.Null);
                    }
                    finally
                    {
                        try
                        {
                            if (Directory.Exists(edit.MountedPath))
                                Directory.Delete(edit.MountedPath, true);
                        }
                        catch
                        {
                            // Best-effort cleanup only.
                        }
                    }
                }
                finally
                {
                    host.Exit();
                }
            }
        }

        private static async Task<Live<SkinInfo>> importSkin(OsuGameBase osu, string name, string author, params (string Filename, byte[] Contents)[] files)
        {
            using MemoryStream osk = createOsk(name, author, files);

            var skinManager = osu.Dependencies.Get<SkinManager>();
            Live<SkinInfo> imported = await skinManager.Import(new ImportTask(osk, $"{name}.osk"));

            Assert.That(imported, Is.Not.Null);
            return imported;
        }

        private static MemoryStream createOsk(string name, string author, params (string Filename, byte[] Contents)[] files)
        {
            var stream = new MemoryStream();

            using (var archive = ZipArchive.CreateArchive())
            {
                archive.AddEntry("skin.ini", new MemoryStream(Encoding.UTF8.GetBytes(generateSkinIni(name, author))), true);

                foreach (var file in files)
                    archive.AddEntry(file.Filename, new MemoryStream(file.Contents), true);

                archive.SaveTo(stream, new ZipWriterOptions(CompressionType.Deflate));
            }

            stream.Position = 0;
            return stream;
        }

        private static string generateSkinIni(string name, string author) =>
            $"[General]{Environment.NewLine}Name: {name}{Environment.NewLine}Author: {author}{Environment.NewLine}";

        private static bool hasTexture(GameHost host, Skin skin, string name)
        {
            var result = new TaskCompletionSource<bool>();

            host.UpdateThread.Scheduler.Add(() =>
            {
                try
                {
                    result.SetResult(skin.GetTexture(name) != null);
                }
                catch (Exception e)
                {
                    result.SetException(e);
                }
            });

            Assert.That(result.Task.Wait(10000), Is.True, $"Timed out resolving texture {name} on the update thread");

            return result.Task.GetResultSafely();
        }

        private static string computeHash(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            return stream.ComputeSHA2Hash();
        }

        private static byte[] readStoredFile(Storage storage, SkinInfo skin, string filename)
        {
            RealmNamedFileUsage? usage = skin.GetFile(filename);

            Assert.That(usage, Is.Not.Null, $"Missing file reference for {filename} on {skin.Name}");

            using Stream? stream = storage.GetStorageForDirectory(file_storage_directory).GetStream(usage!.File.GetStoragePath());

            Assert.That(stream, Is.Not.Null, $"Missing stored content for {filename}");

            using var memory = new MemoryStream();
            stream!.CopyTo(memory);
            return memory.ToArray();
        }

        private static Dictionary<string, byte[]> readArchive(byte[] archiveBytes)
        {
            var entries = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

            using (var archive = ZipArchive.OpenArchive(new MemoryStream(archiveBytes)))
            {
                foreach (var entry in archive.Entries)
                {
                    string? key = entry.Key;

                    if (string.IsNullOrEmpty(key) || key.EndsWith('/'))
                        continue;

                    using Stream entryStream = entry.OpenEntryStream();
                    using var memory = new MemoryStream();

                    entryStream.CopyTo(memory);
                    entries[key] = memory.ToArray();
                }
            }

            return entries;
        }

        private static byte[] loadResourceBytes(string path)
        {
            Stream stream = TestResources.OpenResource(path);

            Assert.That(stream, Is.Not.Null, $"Missing test resource {path}");

            using (stream)
            {
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }

        private class SqliteHeadlessGameHost : CleanRunHeadlessGameHost
        {
            public SqliteHeadlessGameHost(bool bypassCleanupOnDispose = false, [CallerMemberName] string callingMethodName = @"")
                : base(bypassCleanupOnDispose: bypassCleanupOnDispose, callingMethodName: callingMethodName)
            {
            }

            protected override void SetupForRun()
            {
                base.SetupForRun();

                // Must match OsuGameBase's config manager choice (development builds use a separate file).
                using OsuConfigManager config = DebugUtils.IsDebugBuild
                    ? new DevelopmentOsuConfigManager(Storage)
                    : new OsuConfigManager(Storage);

                config.SetValue(OsuSetting.DataStoreBackend, DataStoreBackend.SQLite);
                config.Save();
            }
        }
    }
}
