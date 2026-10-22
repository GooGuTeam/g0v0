// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Extensions;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.IO.Archives;
using osu.Game.Models;
using osu.Game.Overlays.Notifications;
using osu.Game.Utils;
using Realms;

namespace osu.Game.Skinning
{
    public class SkinImporter : RealmArchiveModelImporter<SkinInfo>
    {
        private const string skin_info_file = "skininfo.json";

        private readonly IStorageResourceProvider skinResources;

        private readonly ModelManager<SkinInfo> modelManager;

        private readonly ISkinStore skinStore;

        /// <summary>
        /// Serialises imports against a store backend which does not already serialise them itself.
        /// Both import schedulers can enter <see cref="ImportModel"/> concurrently; duplicate
        /// detection and insertion must happen as one unit.
        /// </summary>
        private readonly Lock importLock = new Lock();

        public SkinImporter(Storage storage, RealmAccess realm, IStorageResourceProvider skinResources, ISkinStore? skinStore = null)
            : base(storage, realm, skinStore?.FileStorageDirectory ?? @"files")
        {
            this.skinResources = skinResources;

            this.skinStore = skinStore ?? new RealmSkinStore(realm);

            modelManager = new ModelManager<SkinInfo>(storage, realm);
        }

        public override IEnumerable<string> HandledExtensions => new[] { ".osk" };

        protected override string[] HashableFileTypes => new[] { ".ini", ".json" };

        protected override bool ShouldDeleteArchive(string path) => string.Equals(Path.GetExtension(path), @".osk", StringComparison.OrdinalIgnoreCase);

        protected override SkinInfo CreateModel(ArchiveReader archive, ImportParameters parameters) => new SkinInfo { Name = archive.Name ?? @"No name" };

        private const string unknown_creator_string = @"Unknown";

        /// <summary>
        /// Import a single skin. Realm imports retain the base implementation; store-backed imports
        /// write file content directly into the selected store's file directory and persist only
        /// metadata and file references.
        /// </summary>
        public override Live<SkinInfo>? ImportModel(SkinInfo item, ArchiveReader? archive = null, ImportParameters parameters = default, CancellationToken cancellationToken = default)
        {
            if (skinStore is RealmSkinStore)
                return base.ImportModel(item, archive, parameters, cancellationToken);

            lock (importLock)
            {
                cancellationToken.ThrowIfCancellationRequested();
                item = item.Detach();

                if (archive != null)
                {
                    item.Files.Clear();

                    foreach (var (original, shortened) in getShortenedFilenames(archive))
                    {
                        if (FilesystemSanityCheckHelpers.IncursPathTraversalRisk(shortened))
                            throw new InvalidOperationException($@"Filename ""{original}"" is not allowed.");

                        using Stream stream = archive.GetStream(original);
                        item.Files.Add(new RealmNamedFileUsage(Files.AddUnmanaged(stream, parameters.PreferHardLinks), shortened));
                    }
                }

                populate(item, cancellationToken);
                checkSkinIniMetadata(item, null);

                cancellationToken.ThrowIfCancellationRequested();

                // Populate() may have rewritten skin.ini, so the hash must be computed after it.
                item.Hash = computeHashWithFallback(item, archive);

                cancellationToken.ThrowIfCancellationRequested();

                SkinInfo? existing = skinStore.FindByHash(item.Hash);

                if (existing != null && CanReuseExisting(existing, item))
                {
                    LogForModel(item, @$"Found existing {HumanisedModelName} for {item} (ID {existing.ID}) – skipping import.");

                    if (existing.DeletePending)
                        skinStore.Undelete(existing.ID);

                    return new SkinStoreLive(skinStore, existing.ID);
                }

                cancellationToken.ThrowIfCancellationRequested();

                skinStore.Add(item);

                if (existing != null && existing.ID != item.ID)
                    skinStore.Delete(existing.ID);

                LogForModel(item, @"Import successfully completed!");

                return new SkinStoreLive(skinStore, item.ID);
            }
        }

        /// <summary>
        /// Update an existing skin with the contents of a path
        /// </summary>
        /// <param name="notification">The progress notification</param>
        /// <param name="task">The <see cref="ImportTask"/> to update the <paramref name="original"/> with</param>
        /// <param name="original">The <see cref="SkinInfo"/> to update</param>
        /// <returns></returns>
        public override async Task<Live<SkinInfo>?> ImportAsUpdate(ProgressNotification notification, ImportTask task, SkinInfo original)
        {
            if (skinStore is RealmSkinStore)
            {
                return await Realm.WriteAsync<Live<SkinInfo>?>(r =>
                {
                    var skinInfo = r.Find<SkinInfo>(original.ID)!;
                    skinInfo.Files.Clear();

                    string[] filesInMountedDirectory = Directory.EnumerateFiles(task.Path, "*.*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(task.Path, f)).ToArray();

                    foreach (string file in filesInMountedDirectory)
                    {
                        using var stream = File.OpenRead(Path.Combine(task.Path, file));

                        modelManager.AddFile(skinInfo, stream, file, r);
                    }

                    string skinIniPath = Path.Combine(task.Path, "skin.ini");

                    if (File.Exists(skinIniPath))
                    {
                        using (var stream = File.OpenRead(skinIniPath))
                        using (var lineReader = new LineBufferedReader(stream))
                        {
                            var decodedSkinIni = new LegacySkinDecoder().Decode(lineReader);

                            if (!string.IsNullOrEmpty(decodedSkinIni.SkinInfo.Name))
                                skinInfo.Name = decodedSkinIni.SkinInfo.Name;

                            if (!string.IsNullOrEmpty(decodedSkinIni.SkinInfo.Creator))
                                skinInfo.Creator = decodedSkinIni.SkinInfo.Creator;
                        }
                    }

                    return skinInfo.ToLive(Realm);
                }).ConfigureAwait(false);
            }

            return await Task.Run(() => importAsUpdateFromDirectory(task, original, notification.CancellationToken), notification.CancellationToken).ConfigureAwait(false);
        }

        private Live<SkinInfo>? importAsUpdateFromDirectory(ImportTask task, SkinInfo original, CancellationToken cancellationToken)
        {
            lock (importLock)
            {
                cancellationToken.ThrowIfCancellationRequested();

                SkinInfo item = original.Detach();
                item.Files.Clear();

                var filenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (string file in Directory.EnumerateFiles(task.Path, "*", SearchOption.AllDirectories))
                {
                    string filename = Path.GetRelativePath(task.Path, file).ToStandardisedPath();

                    if (FilesystemSanityCheckHelpers.IncursPathTraversalRisk(filename) || !filenames.Add(filename))
                        throw new InvalidOperationException($@"Invalid or duplicate skin filename: {filename}");

                    using var stream = File.OpenRead(file);
                    item.Files.Add(new RealmNamedFileUsage(Files.AddUnmanaged(stream), filename));
                }

                populate(item, cancellationToken);

                // skin.ini is the canonical source of user-facing metadata during an external edit.
                if (item.GetFile(@"skin.ini") is RealmNamedFileUsage skinIni)
                {
                    using Stream? stream = Files.Store.GetStream(skinIni.File.GetStoragePath());

                    if (stream != null)
                    {
                        using var lineReader = new LineBufferedReader(stream);
                        var decodedSkinIni = new LegacySkinDecoder().Decode(lineReader);

                        if (!string.IsNullOrEmpty(decodedSkinIni.SkinInfo.Name))
                            item.Name = decodedSkinIni.SkinInfo.Name;

                        if (!string.IsNullOrEmpty(decodedSkinIni.SkinInfo.Creator))
                            item.Creator = decodedSkinIni.SkinInfo.Creator;
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                item.Hash = ComputeHash(item);

                if (string.IsNullOrEmpty(item.Hash))
                {
                    // Fall back to the previous hash for skins without hashable files (for example
                    // an all-image skin whose skin.ini was deleted), then to a stable archive name.
                    item.Hash = !string.IsNullOrEmpty(original.Hash)
                        ? original.Hash
                        : Path.GetFileName(task.Path).ComputeSHA2Hash();
                }

                skinStore.Update(original.ID, stored =>
                {
                    stored.Name = item.Name;
                    stored.Creator = item.Creator;
                    stored.InstantiationInfo = item.InstantiationInfo;
                    stored.Hash = item.Hash;

                    stored.Files.Clear();

                    foreach (var file in item.Files)
                        stored.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = file.File.Hash }, file.Filename));
                });

                return skinStore.GetDetached(original.ID) is { } updated
                    ? new SkinStoreLive(skinStore, updated.ID)
                    : null;
            }
        }

        protected override void Populate(SkinInfo model, ArchiveReader? archive, Realm realm, CancellationToken cancellationToken = default)
        {
            populate(model, cancellationToken);
            checkSkinIniMetadata(model, realm);
        }

        private void populate(SkinInfo model, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skinInfoFile = model.GetFile(skin_info_file);

            if (skinInfoFile != null)
            {
                try
                {
                    using (var existingStream = Files.Storage.GetStream(skinInfoFile.File.GetStoragePath()))
                    using (var reader = new StreamReader(existingStream))
                    {
                        var deserialisedSkinInfo = JsonConvert.DeserializeObject<SkinInfo>(reader.ReadToEnd());

                        if (deserialisedSkinInfo != null)
                        {
                            // for now we only care about the instantiation info.
                            // eventually we probably want to transfer everything across.
                            model.InstantiationInfo = deserialisedSkinInfo.InstantiationInfo;
                        }
                    }
                }
                catch (Exception e)
                {
                    LogForModel(model, $"Error during {skin_info_file} parsing, falling back to default", e);

                    // Not sure if we should still run the import in the case of failure here, but let's do so for now.
                    model.InstantiationInfo = string.Empty;
                }
            }

            // Always rewrite instantiation info (even after parsing in from the skin json) for sanity.
            using var instance = createInstance(model);
            model.InstantiationInfo = instance.GetType().GetInvariantInstantiationInfo();
        }

        private void checkSkinIniMetadata(SkinInfo item, Realm? realm)
        {
            using var instance = createInstance(item);

            // This function can be run on fresh import or save. The logic here ensures a skin.ini file is in a good state for both operations.
            // `Skin` will parse the skin.ini and populate `Skin.Configuration` during construction above.
            string skinIniSourcedName = instance.Configuration.SkinInfo.Name;
            string skinIniSourcedCreator = instance.Configuration.SkinInfo.Creator;
            string archiveName = item.Name.Replace(@".osk", string.Empty, StringComparison.OrdinalIgnoreCase);

            bool isImport = !item.IsManaged;

            if (isImport)
            {
                item.Name = !string.IsNullOrEmpty(skinIniSourcedName) ? skinIniSourcedName : archiveName;
                item.Creator = !string.IsNullOrEmpty(skinIniSourcedCreator) ? skinIniSourcedCreator : unknown_creator_string;

                // For imports, we want to use the archive or folder name as part of the metadata, in addition to any existing skin.ini metadata.
                // In an ideal world, skin.ini would be the only source of metadata, but a lot of skin creators and users don't update it when making modifications.
                // In both of these cases, the expectation from the user is that the filename or folder name is displayed somewhere to identify the skin.
                if (archiveName != item.Name
                    // lazer exports use this format
                    // GetValidFilename accounts for skins with non-ASCII characters in the name that have been exported by lazer.
                    && archiveName != item.GetDisplayString().GetValidFilename())
                    item.Name = @$"{item.Name} [{archiveName}]";
            }

            // By this point, the metadata in SkinInfo will be correct.
            // Regardless of whether this is an import or not, let's write the skin.ini if non-existing or non-matching.
            if (skinIniSourcedName != item.Name)
                UpdateSkinIniMetadata(item, realm);
        }

        /// <summary>
        /// Update the skin.ini of the provided skin to match its metadata.
        /// </summary>
        /// <remarks>
        /// For store-backed (non-Realm) skins this mutates <paramref name="item"/> only. Callers
        /// holding a store-backed <see cref="Live{T}"/> should invoke this from
        /// <see cref="Live{T}.PerformWrite"/> so the mutation is persisted as part of the store's
        /// transaction. During an import the item is persisted by <see cref="ImportModel"/>.
        /// </remarks>
        public void UpdateSkinIniMetadata(SkinInfo item, Realm? realm = null)
        {
            realm ??= item.IsManaged ? item.Realm : null;

            var existingFile = item.GetFile(@"skin.ini");

            // create a working instance of the skin.
            // this implicitly decodes all contents of `skin.ini` into `skin.Configuration` and others.
            // of note, `item` and `skin.Configuration.SkinInfo` are not the same object.
            using var skin = createInstance(item);

            // `skin.Configuration.SkinInfo` will be in a post-decoding state,
            // which means it will potentially be in a different state than `item`.
            // write changes from `item` to `skin.SkinInfo` to definitively apply them.
            skin.Configuration.SkinInfo.Name = item.Name;
            skin.Configuration.SkinInfo.Creator = item.Creator;

            using var outStream = new MemoryStream();

            // now with the skin having the updated metadata, write out the full `skin.ini`.
            using (var outWriter = new StreamWriter(outStream, Encoding.UTF8, 1024, true))
            {
                var encoder = new LegacySkinEncoder(skin);
                encoder.Encode(outWriter);
            }

            if (realm != null)
            {
                if (existingFile == null)
                    modelManager.AddFile(item, outStream, @"skin.ini", realm);
                else
                    modelManager.ReplaceFile(existingFile, outStream, realm);
            }
            else
            {
                if (skinStore is RealmSkinStore)
                    throw new InvalidOperationException("A realm context is required to update a realm-backed skin's metadata.");

                if (existingFile == null)
                    AddFile(item, outStream, @"skin.ini");
                else
                    ReplaceFile(item, existingFile, outStream);
            }

            // The hash is already populated at this point in import.
            // As we have changed files, it needs to be recomputed.
            item.Hash = ComputeHash(item);
        }

        /// <summary>
        /// Add a file to a detached skin, replacing any existing file with the same name.
        /// Content is written to the selected store's file directory; the store is not updated.
        /// </summary>
        internal void AddFile(SkinInfo item, Stream contents, string filename)
        {
            filename = filename.ToStandardisedPath();

            if (FilesystemSanityCheckHelpers.IncursPathTraversalRisk(filename))
                throw new InvalidOperationException($@"Filename ""{filename}"" is not allowed.");

            var existing = item.GetFile(filename);

            if (existing != null)
            {
                ReplaceFile(item, existing, contents);
                return;
            }

            item.Files.Add(new RealmNamedFileUsage(Files.AddUnmanaged(contents), filename));
        }

        /// <summary>
        /// Replace the content of a file on a detached skin. Content is written to the selected
        /// store's file directory; the store is not updated.
        /// </summary>
        internal void ReplaceFile(SkinInfo item, RealmNamedFileUsage file, Stream contents)
        {
            // The usage may belong to an equivalent model (for example a caller-side snapshot
            // while this is invoked inside a store update). Resolve it within the target item by
            // filename so the target's file list is the one which changes.
            var target = item.GetFile(file.Filename) ?? file;

            target.File = Files.AddUnmanaged(contents);
        }

        /// <summary>
        /// Remove a file from a detached skin's file list. Content is shared by hash and is left
        /// on disk; the store is not updated.
        /// </summary>
        internal void DeleteFile(SkinInfo item, RealmNamedFileUsage file)
        {
            var target = item.GetFile(file.Filename) ?? file;

            item.Files.Remove(target);
        }

        private Skin createInstance(SkinInfo item) => item.CreateInstance(skinResources);

        /// <summary>
        /// Save a skin, serialising any changes to skin layouts to relevant JSON structures.
        /// </summary>
        /// <returns>Whether any change actually occurred.</returns>
        public bool Save(Skin skin)
        {
            if (skinStore is RealmSkinStore)
            {
                bool realmHadChanges = false;

                skin.SkinInfo.PerformWrite(s => realmHadChanges = save(skin, s));

                return realmHadChanges;
            }

            if (skin.SkinInfo is SkinStoreLive live)
            {
                bool storeHadChanges = false;

                // Persisting via the live wrapper keeps the write in one store transaction.
                live.PerformWrite(s => storeHadChanges = save(skin, s));

                return storeHadChanges;
            }

            if (skinStore.GetDetached(skin.SkinInfo.ID) == null)
                throw new InvalidOperationException($"Attempting to save a skin which is not yet tracked. Call {nameof(SkinManager.EnsureMutableSkin)} first.");

            bool hadChanges = false;

            // A persisted but unbound snapshot (for example one created directly from a detached
            // item) still needs an explicit store transaction.
            skinStore.Update(skin.SkinInfo.ID, s => hadChanges = save(skin, s));

            return hadChanges;
        }

        private bool save(Skin skin, SkinInfo s)
        {
            // Update for safety
            s.InstantiationInfo = skin.GetType().GetInvariantInstantiationInfo();

            // Serialise out the SkinInfo itself.
            string skinInfoJson = JsonConvert.SerializeObject(s, new JsonSerializerSettings { Formatting = Formatting.Indented });

            using (var streamContent = new MemoryStream(Encoding.UTF8.GetBytes(skinInfoJson)))
            {
                addOrUpdateFile(s, streamContent, skin_info_file);
            }

            // Then serialise each of the drawable component groups into respective files.
            foreach (var drawableInfo in skin.LayoutInfos)
            {
                string json = JsonConvert.SerializeObject(drawableInfo.Value, new JsonSerializerSettings { Formatting = Formatting.Indented });

                using (var streamContent = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    addOrUpdateFile(s, streamContent, @$"{drawableInfo.Key}.json");
                }
            }

            string newHash = ComputeHash(s);

            bool hadChanges = newHash != s.Hash;

            s.Hash = newHash;

            return hadChanges;
        }

        private void addOrUpdateFile(SkinInfo item, Stream contents, string filename)
        {
            if (item.IsManaged)
                modelManager.AddFile(item, contents, filename);
            else
                AddFile(item, contents, filename);
        }

        private string computeHashWithFallback(SkinInfo item, ArchiveReader? archive)
        {
            string hash = ComputeHash(item);

            if (!string.IsNullOrEmpty(hash))
                return hash;

            // Match the base importer's fast hash fallback so skins without skin.ini/json still
            // receive a stable, non-empty hash.
            return archive != null ? archive.Name.ComputeSHA2Hash() : item.Name.ComputeSHA2Hash();
        }

        private IEnumerable<(string original, string shortened)> getShortenedFilenames(ArchiveReader reader)
        {
            string prefix = reader.Filenames.GetCommonPrefix();
            if (!(prefix.EndsWith('/') || prefix.EndsWith('\\')))
                prefix = string.Empty;

            // filename lookups on models are case insensitive, but an archive can contain both
            // "audio.mp3" and "Audio.MP3". importing such a pair would result in a model which
            // throws on any file lookup or be unclear which file should be chosen should that be guarded.
            var seenFilenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string file in reader.Filenames)
            {
                string shortened = file.Substring(prefix.Length).ToStandardisedPath();

                if (FilesystemSanityCheckHelpers.IncursPathTraversalRisk(shortened))
                    throw new InvalidOperationException($@"Filename ""{file}"" is not allowed.");

                if (!seenFilenames.Add(shortened))
                    throw new InvalidOperationException($@"Multiple files with the name ""{shortened}"" (ignoring case) are present. Only one of them can be kept.");

                yield return (file, shortened);
            }
        }
    }
}
