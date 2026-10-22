// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Models;
using osu.Game.Overlays.Notifications;
using osu.Game.Utils;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Handles the storage and retrieval of <see cref="Skin"/>s.
    /// </summary>
    /// <remarks>
    /// This is also exposed and cached as <see cref="ISkinSource"/> to allow for any component to potentially have skinning support.
    /// For gameplay components, see <see cref="RulesetSkinProvidingContainer"/> which adds extra legacy and toggle logic that may affect the lookup process.
    /// </remarks>
    public class SkinManager : ModelManager<SkinInfo>, ISkinSource, IStorageResourceProvider, IModelImporter<SkinInfo>
    {
        /// <summary>
        /// The default "classic" skin.
        /// </summary>
        public Skin DefaultClassicSkin { get; }

        private readonly AudioManager audio;

        private readonly Scheduler scheduler;

        private readonly GameHost host;

        private readonly IResourceStore<byte[]> resources;

        public readonly Bindable<Skin> CurrentSkin = new Bindable<Skin>();

        public readonly Bindable<Live<SkinInfo>> CurrentSkinInfo = new Bindable<Live<SkinInfo>>(ArgonSkin.CreateInfo().ToLiveUnmanaged());

        private readonly SkinImporter skinImporter;

        private readonly LegacySkinExporter skinExporter;

        private readonly IResourceStore<byte[]> userFiles;

        public ISkinStore Store { get; }

        private Skin argonSkin { get; }

        private Skin trianglesSkin { get; }

        private static readonly Live<SkinInfo> random_skin_info = new SkinInfo
        {
            ID = SkinInfo.RANDOM_SKIN,
            Name = "<Random Skin>",
        }.ToLiveUnmanaged();

        public override bool PauseImports
        {
            get => base.PauseImports;
            set
            {
                base.PauseImports = value;
                skinImporter.PauseImports = value;
            }
        }

        public SkinManager(Storage storage, RealmAccess realm, GameHost host, IResourceStore<byte[]> resources, AudioManager audio, Scheduler scheduler, ISkinStore skins = null)
            : base(storage, realm)
        {
            this.audio = audio;
            this.scheduler = scheduler;
            this.host = host;
            this.resources = resources;
            Store = skins ?? new RealmSkinStore(realm);

            userFiles = new StorageBackedResourceStore(storage.GetStorageForDirectory(Store.FileStorageDirectory));

            skinImporter = new SkinImporter(storage, realm, this, Store)
            {
                PostNotification = obj => PostNotification?.Invoke(obj),
            };

            var defaultSkins = new[]
            {
                DefaultClassicSkin = new DefaultLegacySkin(this),
                trianglesSkin = new TrianglesSkin(this),
                argonSkin = new ArgonSkin(this),
                new ArgonProSkin(this),
            };

            // Ensure the default entries are present.
            foreach (var skin in defaultSkins)
            {
                if (Store.GetDetached(skin.SkinInfo.ID) == null)
                    Store.Add(skin.SkinInfo.Value);

                if (Store is not RealmSkinStore)
                    skin.BindStore(Store);
            }

            CurrentSkinInfo.ValueChanged += skin =>
            {
                CurrentSkin.Value = skin.NewValue.PerformRead(GetSkin);
            };

            CurrentSkin.Value = argonSkin;
            CurrentSkin.ValueChanged += skin =>
            {
                if (!skin.NewValue.SkinInfo.Equals(CurrentSkinInfo.Value))
                    throw new InvalidOperationException($"Setting {nameof(CurrentSkin)}'s value directly is not supported. Use {nameof(CurrentSkinInfo)} instead.");

                SourceChanged?.Invoke();
            };

            skinExporter = new LegacySkinExporter(storage, Store.FileStorageDirectory)
            {
                PostNotification = obj => PostNotification?.Invoke(obj)
            };
        }

        /// <summary>
        /// Returns the dropdown ordering for use mainly by the skin selection UI.
        /// Inserts the defaults first, then 'random skin', then custom ones.
        /// Returns a list of <see cref="Live{SkinInfo}"/> items.
        /// </summary>
        public IList<Live<SkinInfo>> GetAllUsableSkins()
        {
            var skins = new List<Live<SkinInfo>>
            {
                GetLive(Store.GetDetached(SkinInfo.ARGON_SKIN)),
                GetLive(Store.GetDetached(SkinInfo.ARGON_PRO_SKIN)),
                GetLive(Store.GetDetached(SkinInfo.TRIANGLES_SKIN)),
                GetLive(Store.GetDetached(SkinInfo.CLASSIC_SKIN)),
                random_skin_info,
            };

            skins.AddRange(Store.GetAllUsableDetached()
                                .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                                .Select(GetLive));
            return skins;
        }

        public void SelectRandomSkin()
        {
            // Can be disabled while the current skin is externally mounted for editing.
            if (CurrentSkinInfo.Disabled)
                return;

            Guid currentSkinId = CurrentSkinInfo.Value.ID;
            var randomChoices = Store.GetAllDetached().Where(s => !s.DeletePending && s.ID != currentSkinId).ToArray();

            CurrentSkinInfo.Value = randomChoices.Length == 0
                ? GetLive(Store.GetDetached(SkinInfo.ARGON_SKIN))
                : GetLive(randomChoices[RNG.Next(0, randomChoices.Length)]);
        }

        private void cycleSkins(int direction)
        {
            Debug.Assert(direction != 0);

            // don't change selection if current skin is externally disabled/mounted for editing.
            if (CurrentSkinInfo.Disabled)
                return;

            var skins = GetAllUsableSkins();

            int i = skins.IndexOf(CurrentSkinInfo.Value);

            // If the current skin isn't selectable anymore, start from the top.
            if (i < 0 && direction < 0)
                i = 0;

            do
            {
                i = (i + direction + skins.Count) % skins.Count;
            } while (skins[i].ID == SkinInfo.RANDOM_SKIN);

            CurrentSkinInfo.Value = skins[i];
        }

        /// <summary>
        /// Cycle one skin backward.
        /// </summary>
        public void SelectPreviousSkin() => cycleSkins(-1);

        /// <summary>
        /// Cycle one skin forward.
        /// </summary>
        public void SelectNextSkin() => cycleSkins(1);

        /// <summary>
        /// Retrieve a <see cref="Skin"/> instance for the provided <see cref="SkinInfo"/>
        /// </summary>
        /// <param name="skinInfo">The skin to lookup.</param>
        /// <returns>A <see cref="Skin"/> instance correlating to the provided <see cref="SkinInfo"/>.</returns>
        public Skin GetSkin(SkinInfo skinInfo)
        {
            var skin = skinInfo.CreateInstance(this);
            if (Store is not RealmSkinStore && Store.GetDetached(skinInfo.ID) != null)
                skin.BindStore(Store);
            return skin;
        }

        internal Live<SkinInfo> GetLive(SkinInfo skin)
        {
            if (Store is not RealmSkinStore)
                return new SkinStoreLive(Store, skin.ID);

            return Realm.Run(r => r.Find<SkinInfo>(skin.ID)?.ToLive(Realm) ?? skin.ToLiveUnmanaged());
        }

        /// <summary>
        /// Ensure that the current skin is in a state it can accept user modifications.
        /// This will create a copy of any internal skin and being tracking in the database if not already.
        /// </summary>
        /// <returns>
        /// Whether a new skin was created to allow for mutation.
        /// </returns>
        public bool EnsureMutableSkin()
        {
            return CurrentSkinInfo.Value.PerformRead(s =>
            {
                if (!s.Protected)
                    return false;

                string[] existingSkinNames = Store.GetAllDetached().Where(skin => !skin.DeletePending).Select(skin => skin.Name).ToArray();

                // if the user is attempting to save one of the default skin implementations, create a copy first.
                var skinInfo = new SkinInfo
                {
                    Creator = s.Creator,
                    InstantiationInfo = s.InstantiationInfo,
                    Name = NamingUtils.GetNextBestName(existingSkinNames, $@"{s.Name} (modified)")
                };

                var result = skinImporter.ImportModel(skinInfo, parameters: new ImportParameters
                {
                    ImportImmediately = true // to avoid possible deadlocks when editing skin during gameplay.
                });

                if (result != null)
                {
                    // save once to ensure the required json content is populated.
                    // currently this only happens on save.
                    result.PerformRead(skin => Save(GetSkin(skin)));
                    CurrentSkinInfo.Value = result;
                    return true;
                }

                return false;
            });
        }

        /// <summary>
        /// Save a skin, serialising any changes to skin layouts to relevant JSON structures.
        /// </summary>
        /// <returns>Whether any change actually occurred.</returns>
        public bool Save(Skin skin)
        {
            if (!skin.SkinInfo.IsManaged)
                throw new InvalidOperationException($"Attempting to save a skin which is not yet tracked. Call {nameof(EnsureMutableSkin)} first.");

            return skinImporter.Save(skin);
        }

        /// <summary>
        /// Perform a lookup query on available <see cref="SkinInfo"/>s.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>The first result for the provided query, or null if no results were found.</returns>
        public Live<SkinInfo> Query(Expression<Func<SkinInfo, bool>> query)
        {
            var skin = Store.GetAllDetached().FirstOrDefault(query.Compile());
            return skin == null ? null : GetLive(skin);
        }

        public event Action SourceChanged;

        public Drawable GetDrawableComponent(ISkinComponentLookup lookup) => lookupWithFallback(s => s.GetDrawableComponent(lookup));

        public Texture GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => lookupWithFallback(s => s.GetTexture(componentName, wrapModeS, wrapModeT));

        public ISample GetSample(ISampleInfo sampleInfo) => lookupWithFallback(s => s.GetSample(sampleInfo));

        public IBindable<TValue> GetConfig<TLookup, TValue>(TLookup lookup) => lookupWithFallback(s => s.GetConfig<TLookup, TValue>(lookup));

        public ISkin FindProvider(Func<ISkin, bool> lookupFunction)
        {
            foreach (var source in AllSources)
            {
                if (lookupFunction(source))
                    return source;
            }

            return null;
        }

        public IEnumerable<ISkin> AllSources
        {
            get
            {
                yield return CurrentSkin.Value;

                // Skin manager provides default fallbacks.
                // This handles cases where a user skin doesn't have the required resources for complete display of
                // certain elements.

                if (CurrentSkin.Value is LegacySkin && CurrentSkin.Value != DefaultClassicSkin)
                    yield return DefaultClassicSkin;

                if (CurrentSkin.Value != trianglesSkin)
                    yield return trianglesSkin;
            }
        }

        private T lookupWithFallback<T>(Func<ISkin, T> lookupFunction)
            where T : class
        {
            try
            {
                Skin.LogLookupDebug(this, lookupFunction, Skin.LookupDebugType.Enter);

                foreach (var source in AllSources)
                {
                    if (lookupFunction(source) is T skinSourced)
                        return skinSourced;
                }

                return null;
            }
            finally
            {
                Skin.LogLookupDebug(this, lookupFunction, Skin.LookupDebugType.Exit);
            }
        }

        #region IResourceStorageProvider

        IRenderer IStorageResourceProvider.Renderer => host.Renderer;
        AudioManager IStorageResourceProvider.AudioManager => audio;
        IResourceStore<byte[]> IStorageResourceProvider.Resources => resources;
        IResourceStore<byte[]> IStorageResourceProvider.Files => userFiles;
        RealmAccess IStorageResourceProvider.RealmAccess => Realm;
        IResourceStore<TextureUpload> IStorageResourceProvider.CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore) => host.CreateTextureLoaderStore(underlyingStore);

        #endregion

        #region Implementation of IModelImporter<SkinInfo>

        public Action<IEnumerable<Live<SkinInfo>>> PresentImport
        {
            set => skinImporter.PresentImport = value;
        }

        public Task Import(params string[] paths) => skinImporter.Import(paths);

        public Task Import(ImportTask[] imports, ImportParameters parameters = default) => skinImporter.Import(imports, parameters);

        public IEnumerable<string> HandledExtensions => skinImporter.HandledExtensions;

        public Task<IEnumerable<Live<SkinInfo>>> Import(ProgressNotification notification, ImportTask[] tasks, ImportParameters parameters = default) =>
            skinImporter.Import(notification, tasks, parameters);

        public Task<Live<SkinInfo>> ImportAsUpdate(ProgressNotification notification, ImportTask task, SkinInfo original) =>
            skinImporter.ImportAsUpdate(notification, task, original);

        public Task<ExternalEditOperation<SkinInfo>> BeginExternalEditing(SkinInfo model) => skinImporter.BeginExternalEditing(model);

        public Task<Live<SkinInfo>> Import(ImportTask task, ImportParameters parameters = default, CancellationToken cancellationToken = default) =>
            skinImporter.Import(task, parameters, cancellationToken);

        public Task ExportCurrentSkin() => ExportSkin(CurrentSkinInfo.Value);

        public Task ExportSkin(Live<SkinInfo> skin) => skinExporter.ExportAsync(skin);

        #endregion

        public void Delete([CanBeNull] Expression<Func<SkinInfo, bool>> filter = null, bool silent = false)
        {
            var items = Store.GetAllUsableDetached().AsEnumerable();
            if (filter != null)
                items = items.Where(filter.Compile());
            Delete(items.ToList(), silent);
        }

        public override bool Delete(SkinInfo item)
        {
            if (!Store.Delete(item.ID))
                return false;

            if (CurrentSkinInfo.Value.ID == item.ID)
                scheduler.Add(() => CurrentSkinInfo.Value = GetLive(Store.GetDetached(SkinInfo.ARGON_SKIN)));
            return true;
        }

        public override void Undelete(SkinInfo item) => Store.Undelete(item.ID);

        public override void AddFile(SkinInfo item, Stream contents, string filename)
        {
            if (Store is RealmSkinStore)
                base.AddFile(item, contents, filename);
            else
                updateFiles(item, s => skinImporter.AddFile(s, contents, filename));
        }

        public override void ReplaceFile(SkinInfo item, RealmNamedFileUsage file, Stream contents)
        {
            if (Store is RealmSkinStore)
                base.ReplaceFile(item, file, contents);
            else
                updateFiles(item, s => skinImporter.ReplaceFile(s, file, contents));
        }

        public override void DeleteFile(SkinInfo item, RealmNamedFileUsage file)
        {
            if (Store is RealmSkinStore)
                base.DeleteFile(item, file);
            else
                updateFiles(item, s => skinImporter.DeleteFile(s, file));
        }

        private void updateFiles(SkinInfo item, Action<SkinInfo> update)
        {
            Store.Update(item.ID, s =>
            {
                update(s);
                s.Hash = skinImporter.ComputeHash(s);
            });

            var stored = Store.GetDetached(item.ID);
            if (stored == null)
                return;

            item.Files.Clear();
            foreach (var file in stored.Files)
                item.Files.Add(file);
            item.Hash = stored.Hash;
        }

        public void Rename(Live<SkinInfo> skin, string newName)
        {
            skin.PerformWrite(s =>
            {
                s.Name = newName;
                skinImporter.UpdateSkinIniMetadata(s, s.Realm!);
            });
        }

        public void SetSkinFromConfiguration(string guidString)
        {
            Live<SkinInfo> skinInfo = null;

            if (Guid.TryParse(guidString, out var guid))
                skinInfo = Query(s => s.ID == guid);

            if (skinInfo == null)
            {
                if (guid == SkinInfo.CLASSIC_SKIN)
                    skinInfo = DefaultClassicSkin.SkinInfo;
            }

            CurrentSkinInfo.Value = skinInfo ?? trianglesSkin.SkinInfo;
        }
    }
}
