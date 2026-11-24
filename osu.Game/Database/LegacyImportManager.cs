// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.IO;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings.Sections.Maintenance;
using osu.Game.Scoring;
using osu.Game.Skinning;

namespace osu.Game.Database
{
    /// <summary>
    /// Handles migration of user data from other installations (stable or another g0v0!/lazer installation).
    /// </summary>
    public partial class LegacyImportManager : Component
    {
        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private ScoreManager scores { get; set; } = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private IDialogOverlay dialogOverlay { get; set; } = null!;

        [Resolved]
        private RealmAccess realmAccess { get; set; } = null!;

        [Resolved]
        private GameHost gameHost { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        private StableStorage? cachedStableStorage;
        private LazerStorage? cachedLazerStorage;

        public bool SupportsImportFromStable => RuntimeInfo.IsDesktop;

        /// <summary>
        /// Whether user data can be migrated from another g0v0!/lazer installation.
        /// </summary>
        public bool SupportsImportFromLazer => RuntimeInfo.IsDesktop;

        public bool SupportsImportFrom(ImportSource source)
        {
            switch (source)
            {
                case ImportSource.Stable:
                    return SupportsImportFromStable;

                case ImportSource.Lazer:
                    return SupportsImportFromLazer;

                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, null);
            }
        }

        public void UpdateStorage(ImportSource source, string path)
        {
            switch (source)
            {
                case ImportSource.Stable:
                    if (gameHost is not DesktopGameHost desktopGameHost)
                        return;

                    cachedStableStorage = new StableStorage(path, desktopGameHost);
                    break;

                case ImportSource.Lazer:
                    cachedLazerStorage?.Dispose();
                    cachedLazerStorage = new LazerStorage(path);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, null);
            }
        }

        /// <summary>
        /// Checks whether a valid location to import from can be determined starting from the supplied <paramref name="directory"/>.
        /// </summary>
        /// <param name="source">The installation type being imported from.</param>
        /// <param name="directory">The directory to check for import eligibility.</param>
        /// <param name="root">
        /// If the return value is <see langword="true"/>,
        /// this parameter will contain the <see cref="DirectoryInfo"/> to use as the root directory for importing.
        /// </param>
        public bool IsUsableAsImportSource(ImportSource source, DirectoryInfo? directory, [NotNullWhen(true)] out DirectoryInfo? root)
        {
            switch (source)
            {
                case ImportSource.Stable:
                    return IsUsableForStableImport(directory, out root);

                case ImportSource.Lazer:
                    if (directory != null && LazerStorage.IsUsableInstall(directory))
                    {
                        root = directory;
                        return true;
                    }

                    root = null;
                    return false;

                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, null);
            }
        }

        /// <inheritdoc cref="IsUsableAsImportSource"/>
        public bool IsUsableForStableImport(DirectoryInfo? directory, [NotNullWhen(true)] out DirectoryInfo? stableRoot)
        {
            if (directory == null || !directory.Exists)
            {
                stableRoot = null;
                return false;
            }

            // A full stable installation will have a configuration file present.
            // This is the best case scenario, as it may contain a custom beatmap directory we need to traverse to.
            if (directory.GetFiles(@"osu!.*.cfg").Any())
            {
                stableRoot = directory;
                return true;
            }

            // The user may only have their songs or skins folders left.
            // We still want to allow them to import based on this.
            if (directory.GetDirectories(@"Songs").Any() || directory.GetDirectories(@"Skins").Any())
            {
                stableRoot = directory;
                return true;
            }

            // The user may have traversed *inside* their songs or skins folders.
            if (directory.Parent != null && (directory.Name == @"Songs" || directory.Name == @"Skins"))
            {
                stableRoot = directory.Parent;
                return true;
            }

            stableRoot = null;
            return false;
        }

        /// <summary>
        /// The directory of the installation currently selected for importing, if any.
        /// </summary>
        public virtual string? GetCurrentStoragePath(ImportSource source)
        {
            switch (source)
            {
                case ImportSource.Stable:
                    return GetCurrentStableStorage()?.GetFullPath(string.Empty);

                case ImportSource.Lazer:
                    return cachedLazerStorage?.InstallPath;

                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, null);
            }
        }

        public bool CheckHardLinkAvailability(ImportSource source)
        {
            if (gameHost is not DesktopGameHost desktopGameHost)
                return false;

            string testDestinationPath = desktopGameHost.Storage.GetFullPath(string.Empty);

            switch (source)
            {
                case ImportSource.Stable:
                {
                    var stableStorage = GetCurrentStableStorage();

                    if (stableStorage == null)
                        return false;

                    return HardLinkHelper.CheckAvailability(testDestinationPath, stableStorage.GetSongStorage().GetFullPath(string.Empty));
                }

                case ImportSource.Lazer:
                {
                    var lazerStorage = GetCurrentLazerStorage();

                    if (lazerStorage == null || !lazerStorage.FileStoreAvailable)
                        return false;

                    return HardLinkHelper.CheckAvailability(testDestinationPath, lazerStorage.FileStorePath);
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, null);
            }
        }

        public virtual async Task<int> GetImportCount(ImportSource source, ImportContent content, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (source)
            {
                case ImportSource.Stable:
                {
                    var stableStorage = GetCurrentStableStorage();

                    if (stableStorage == null)
                        return 0;

                    switch (content)
                    {
                        case ImportContent.Beatmaps:
                            return await new LegacyBeatmapImporter(beatmaps).GetAvailableCount(stableStorage).ConfigureAwait(false);

                        case ImportContent.Skins:
                            return await new LegacySkinImporter(skins).GetAvailableCount(stableStorage).ConfigureAwait(false);

                        case ImportContent.Collections:
                            return await new LegacyCollectionImporter(realmAccess).GetAvailableCount(stableStorage).ConfigureAwait(false);

                        case ImportContent.Scores:
                            return await new LegacyScoreImporter(scores).GetAvailableCount(stableStorage).ConfigureAwait(false);

                        default:
                            throw new ArgumentException($"Only one {nameof(ImportContent)} flag should be specified.");
                    }
                }

                case ImportSource.Lazer:
                {
                    var lazerStorage = GetCurrentLazerStorage();

                    if (lazerStorage == null)
                        return 0;

                    try
                    {
                        switch (content)
                        {
                            case ImportContent.Beatmaps:
                                return await new LazerBeatmapImporter(beatmaps).GetAvailableCount(lazerStorage).ConfigureAwait(false);

                            case ImportContent.Skins:
                                return await new LazerSkinImporter(skins).GetAvailableCount(lazerStorage).ConfigureAwait(false);

                            case ImportContent.Collections:
                                return await new LazerCollectionImporter(realmAccess).GetAvailableCount(lazerStorage).ConfigureAwait(false);

                            case ImportContent.Scores:
                                return await new LazerScoreImporter(scores, realmAccess).GetAvailableCount(lazerStorage).ConfigureAwait(false);

                            default:
                                throw new ArgumentException($"Only one {nameof(ImportContent)} flag should be specified.");
                        }
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        // The installation's database could not be read (most likely an incompatible version).
                        Logger.Error(e, $@"Could not read the database of the installation at {lazerStorage.InstallPath}.");
                        return 0;
                    }
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, null);
            }
        }

        /// <summary>
        /// Verify that the data of the currently selected installation can actually be read.
        /// </summary>
        /// <remarks>
        /// Installations of other clients may store their data in a way which this version of the game cannot understand.
        /// </remarks>
        public Task<bool> CheckSourceReadableAsync(ImportSource source) => Task.Run(() =>
        {
            if (source != ImportSource.Lazer)
                return true;

            var lazerStorage = GetCurrentLazerStorage();

            if (lazerStorage == null)
                return false;

            try
            {
                using (lazerStorage.OpenRealm())
                    return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, $@"Could not read the database of the installation at {lazerStorage.InstallPath}.");
                return false;
            }
        });

        public async Task ImportAsync(ImportSource source, ImportContent content, bool interactiveLocateIfNotFound = true)
        {
            switch (source)
            {
                case ImportSource.Stable:
                    await ImportFromStableAsync(content, interactiveLocateIfNotFound).ConfigureAwait(false);
                    return;

                case ImportSource.Lazer:
                    await importFromLazerAsync(content).ConfigureAwait(false);
                    return;

                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, null);
            }
        }

        public async Task ImportFromStableAsync(ImportContent content, bool interactiveLocateIfNotFound = true)
        {
            var stableStorage = GetCurrentStableStorage();

            if (stableStorage == null)
            {
                if (!interactiveLocateIfNotFound)
                    return;

                var taskCompletionSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                Schedule(() => dialogOverlay.Push(new StableDirectoryLocationDialog(taskCompletionSource)));
                string stablePath = await taskCompletionSource.Task.ConfigureAwait(false);

                UpdateStorage(ImportSource.Stable, stablePath);
                stableStorage = GetCurrentStableStorage();
            }

            if (stableStorage == null)
                return;

            var importTasks = new List<Task>();

            Task beatmapImportTask = Task.CompletedTask;
            if (content.HasFlag(ImportContent.Beatmaps))
                importTasks.Add(beatmapImportTask = new LegacyBeatmapImporter(beatmaps).ImportFromStableAsync(stableStorage));

            if (content.HasFlag(ImportContent.Skins))
                importTasks.Add(new LegacySkinImporter(skins).ImportFromStableAsync(stableStorage));

            if (content.HasFlag(ImportContent.Collections))
            {
                importTasks.Add(beatmapImportTask.ContinueWith(_ => new LegacyCollectionImporter(realmAccess)
                {
                    // Other legacy importers import via model managers which handle the posting of notifications.
                    // Collections are an exception.
                    PostNotification = n => notifications?.Post(n)
                }.ImportFromStorage(stableStorage), TaskContinuationOptions.OnlyOnRanToCompletion));
            }

            if (content.HasFlag(ImportContent.Scores))
                importTasks.Add(beatmapImportTask.ContinueWith(_ => new LegacyScoreImporter(scores).ImportFromStableAsync(stableStorage), TaskContinuationOptions.OnlyOnRanToCompletion));

            await Task.WhenAll(importTasks.ToArray()).ConfigureAwait(false);
        }

        private async Task importFromLazerAsync(ImportContent content)
        {
            var lazerStorage = GetCurrentLazerStorage();

            if (lazerStorage == null)
                return;

            // Beatmaps have to be in place before scores can be linked to them.
            if (content.HasFlag(ImportContent.Beatmaps))
                await new LazerBeatmapImporter(beatmaps).ImportFromLazerAsync(lazerStorage).ConfigureAwait(false);

            if (content.HasFlag(ImportContent.Skins))
                await new LazerSkinImporter(skins).ImportFromLazerAsync(lazerStorage).ConfigureAwait(false);

            if (content.HasFlag(ImportContent.Collections))
            {
                await new LazerCollectionImporter(realmAccess)
                {
                    PostNotification = n => notifications?.Post(n)
                }.ImportFromLazerAsync(lazerStorage).ConfigureAwait(false);
            }

            if (content.HasFlag(ImportContent.Scores))
            {
                await new LazerScoreImporter(scores, realmAccess)
                {
                    PostNotification = n => notifications?.Post(n)
                }.ImportFromLazerAsync(lazerStorage).ConfigureAwait(false);
            }
        }

        public StableStorage? GetCurrentStableStorage()
        {
            if (cachedStableStorage != null)
                return cachedStableStorage;

            var stableStorage = game?.GetStorageForStableInstall();
            if (stableStorage != null)
                return cachedStableStorage = stableStorage;

            return null;
        }

        public LazerStorage? GetCurrentLazerStorage() => cachedLazerStorage;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            cachedLazerStorage?.Dispose();
            cachedLazerStorage = null;
        }
    }
}
