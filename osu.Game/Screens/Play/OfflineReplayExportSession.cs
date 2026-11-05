// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Mixing;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Veldrid;
using osu.Framework.Graphics.Video;
using osu.Framework.IO.Stores;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.IO;
using osu.Game.Resources;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Play
{
    public sealed class OfflineReplayExportSession : IReplayExportSession
    {
        private readonly int ownerThread = Environment.CurrentManagedThreadId;
        private readonly List<IDisposable> resources = new List<IDisposable>();
        private readonly string destination;
        private readonly string temporaryPath;
        private readonly ManualAdjustableClock clock = new ManualAdjustableClock { IsRunning = true };
        private readonly FramedClock framedClock;
        private readonly ReplayExportPresentationClock presentationClock = new ReplayExportPresentationClock();
        private readonly double startTime;
        private readonly double rate;
        private const int audio_step_samples = 200;
        private readonly float[] pcm;
        private readonly float[] intervalPcm = new float[audio_step_samples * 2];
        private OfflineRenderContext context = null!;
        private OfflineFrameRenderer target = null!;
        private IOfflineVideoRecording recording = null!;
        private OfflineAudioMixer mixer = null!;
        private AudioManager audio = null!;
        private Drawable scene = null!;
        private Track? track;
        private bool trackStarted;
        private bool published;
        private bool disposed;

        public OfflineReplayExportSession(WorkingBeatmap beatmap, Score replay, SkinInfo selectedSkin,
                                          IStorageResourceProvider storage, IDictionary<OsuSetting, string> settings,
                                          Type rulesetType, IReadOnlyDictionary<string, string>? rulesetSettings,
                                          string destination, double startTime, double rate, VideoRecordingOptions options,
                                          ReplayVideoBackend backend, string ffmpegPath, Storage? shaderCacheStorage, CancellationToken cancellationToken)
            : this(beatmap, replay, selectedSkin, storage, settings, rulesetType, rulesetSettings, destination, startTime, rate, options, backend, ffmpegPath, shaderCacheStorage, null,
                cancellationToken)
        {
        }

        // recordingFactory replaces the video encoder, e.g. to benchmark rendering alone.
        internal OfflineReplayExportSession(WorkingBeatmap beatmap, Score replay, SkinInfo selectedSkin,
                                            IStorageResourceProvider storage, IDictionary<OsuSetting, string> settings,
                                            Type rulesetType, IReadOnlyDictionary<string, string>? rulesetSettings,
                                            string destination, double startTime, double rate, VideoRecordingOptions options,
                                            ReplayVideoBackend backend, string ffmpegPath, Storage? shaderCacheStorage,
                                            Func<OfflineRenderContext, IOfflineVideoRecording>? recordingFactory, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(options);
            int width = options.Width;
            int height = options.Height;
            bool useFfmpeg = backend == ReplayVideoBackend.Ffmpeg || (backend == ReplayVideoBackend.Automatic && !OperatingSystem.IsWindows());
            if (!Enum.IsDefined(backend))
                throw new ArgumentOutOfRangeException(nameof(backend));
            if (!useFfmpeg && !OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException(@"The Media Foundation recording backend requires Windows. Select FFmpeg for replay export.");
            pcm = new float[48000 / options.FrameRate * 2];
            this.destination = Path.GetFullPath(destination);
            temporaryPath = this.destination + $@".{Guid.NewGuid():N}.partial.mp4";
            this.startTime = startTime;
            this.rate = rate;
            framedClock = new FramedClock(clock);
            try
            {
                context = own(new OfflineRenderContext(width, height, shaderCacheStorage));
                mixer = own(new OfflineAudioMixer());
                // Same stores as OsuGameBase.Resources (MapleMono and the V2 textures are embedded in osu.Game itself), plus the framework's.
                var bytes = new ResourceStore<byte[]>(new DllResourceStore(OsuResources.ResourceAssembly));
                bytes.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(OsuGameBase).Assembly), @"Resources"));
                bytes.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(osu.Framework.Game).Assembly), @"Resources"));
                audio = own(mixer.CreateAudioManager(bytes));
                var provider = new OfflineStorageResourceProvider(context.Renderer, audio, storage);
                var dependencies = new DependencyContainer();
                dependencies.CacheAs(context.Renderer);
                dependencies.CacheAs(presentationClock);
                dependencies.CacheAs(audio);
                dependencies.CacheAs(audio.Samples);
                dependencies.CacheAs<IStorageResourceProvider>(provider);
                dependencies.CacheAs(storage.RealmAccess);
                var config = own(new OfflineExportConfigManager(settings));
                dependencies.CacheAs<IRulesetConfigCache>(own(new OfflineRulesetConfigCache(rulesetType, rulesetSettings)));
                dependencies.CacheAs<OsuConfigManager>(config);
                dependencies.CacheAs<IGameplaySettings>(config);
                var exportMods = replay.ScoreInfo.Mods.Select(m => m.DeepClone()).ToArray();
                var exportRuleset = replay.ScoreInfo.Ruleset.CreateInstance();
                var playableBeatmap = beatmap.GetPlayableBeatmap(exportRuleset.RulesetInfo, exportMods);
                playableBeatmap.BeatmapInfo.Difficulty = playableBeatmap.Difficulty.Clone();
                var playableWorkingBeatmap = new PlayableWorkingBeatmap(playableBeatmap, beatmap);

                dependencies.CacheAs<IBindable<WorkingBeatmap>>(new Bindable<WorkingBeatmap>(playableWorkingBeatmap));
                dependencies.CacheAs<IBindable<RulesetInfo>>(new Bindable<RulesetInfo>(exportRuleset.RulesetInfo));
                dependencies.CacheAs<IBindable<IReadOnlyList<Mod>>>(new Bindable<IReadOnlyList<Mod>>(exportMods));
                dependencies.CacheAs(new OsuColour());
                dependencies.CacheAs(own(new SessionStatics()));
                var frameworkConfig = own(new OfflineFrameworkConfigManager());
                dependencies.CacheAs(own(new LocalisationManager(frameworkConfig)));

                var shaderBytes = new ResourceStore<byte[]>();
                shaderBytes.AddStore(new NamespacedResourceStore<byte[]>(bytes, @"Shaders"));
                var shaders = own(new ShaderManager(context.Renderer, shaderBytes));
                dependencies.CacheAs(shaders);
                dependencies.CacheAs<IShaderStore>(shaders);
                var textures = own(new TextureStore(context.Renderer, new TextureLoaderStore(new NamespacedResourceStore<byte[]>(bytes, @"Textures"))));
                dependencies.CacheAs(textures);
                dependencies.CacheAs(own(new LargeTextureStore(context.Renderer, new TextureLoaderStore(new NamespacedResourceStore<byte[]>(bytes, @"Textures")))));
                var textureLoader = new TextureLoaderStore(bytes);
                // The overload taking a filtering mode packs glyphs into an atlas; the three-argument one gives each glyph its own texture.
                var fonts = own(new FontStore(context.Renderer, textureLoader, 100, TextureFilteringMode.Linear));
                // Same fonts and order as the game: the framework's FontAwesome, then OsuGameBase's list (including the CJK fallbacks).
                foreach (string font in new[]
                         {
                             @"Fonts/FontAwesome5/FontAwesome-Solid",
                             @"Fonts/FontAwesome5/FontAwesome-Regular",
                             @"Fonts/FontAwesome5/FontAwesome-Brands",
                         }.Concat(OsuGameBase.GAME_FONTS))
                    fonts.AddTextureSource(new GlyphStore(bytes, font, textureLoader));

                // Mod icons are texture-backed glyphs, separate from the text and FontAwesome fonts.
                fonts.AddStore(new OsuIcon.OsuIconStore(textures));
                dependencies.CacheAs(fonts);

                var privateSkin = own(selectedSkin.CreateInstance(provider));
                var classicSkin = own(new DefaultLegacySkin(provider));
                dependencies.CacheAs(classicSkin);
                var defaultSkin = own(new TrianglesSkin(provider));
                var beatmapSkin = own(new LegacyBeatmapSkin(playableBeatmap.BeatmapInfo, provider));
                var gameplay = new OfflineGameplayScene(playableWorkingBeatmap, replay, clock, beatmapSkin, classicSkin, width, height, privateSkin is LegacySkin);
                // Same fallback order as SkinManager.AllSources: the classic skin only backs legacy user skins, and the triangles skin
                // backs everything but itself. Unconditional fallbacks would show classic elements under argon or triangles skins.
                var fallbacks = new List<ISkin>();
                if (privateSkin is LegacySkin && privateSkin is not DefaultLegacySkin)
                    fallbacks.Add(classicSkin);
                if (privateSkin is not TrianglesSkin)
                    fallbacks.Add(defaultSkin);

                Drawable skinnedGameplay = new SkinProvidingContainer(privateSkin) { Child = gameplay };
                foreach (var fallback in fallbacks)
                    skinnedGameplay = new SkinProvidingContainer(fallback) { Child = skinnedGameplay };

                scene = new SafeAreaDefiningContainer(new BindableSafeArea())
                {
                    Size = new Vector2(width, height),
                    Child = skinnedGameplay,
                };
                own(new SceneOwnership(context, scene));
                target = own(new OfflineFrameRenderer(context.Renderer, width, height));
                recording = recordingFactory != null
                    ? own(recordingFactory(context))
                    : useFfmpeg
                    ? own<IOfflineVideoRecording>(new FfmpegOfflineVideoRecording(context, temporaryPath, options, ffmpegPath, cancellationToken))
                    : own<IOfflineVideoRecording>(new OfflineVideoRecording(context, temporaryPath, width, height, options));

                clock.CurrentTime = startTime;
                clock.Rate = rate;
                framedClock.ProcessFrame();
                context.LoadScene(scene, framedClock, dependencies);
                context.UpdateScene(scene);
                audio.Update();

                string? audioPath = beatmap.BeatmapSetInfo.GetPathForFile(beatmap.Metadata.AudioFile);
                if (!string.IsNullOrEmpty(audioPath))
                {
                    track = audio.GetTrackStore(provider.Files).Get(audioPath);
                    if (track == null || !track.IsLoaded)
                        throw new InvalidOperationException(@"Unable to decode the beatmap audio.");
                    foreach (var mod in replay.ScoreInfo.Mods.OfType<IApplicableToTrack>())
                        mod.ApplyToTrack(track);
                }
            }
            catch
            {
                try { disposeResources(); }
                catch (Exception) { }
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
                throw;
            }
        }

        public Task EncodeFrameAsync(long frameIndex, long timestamp, long audioStartSample, int audioSampleCount, CancellationToken cancellationToken)
        {
            checkThread();
            ObjectDisposedException.ThrowIf(disposed, this);
            if (frameIndex != recording.WrittenFrames || audioStartSample != recording.WrittenAudioFrames || audioSampleCount != recording.AudioFramesPerVideoFrame)
                throw new InvalidOperationException(@"The export timeline must match the configured video frame rate and contiguous 48 kHz audio.");

            // One scope per frame instead of one per update and render; each scope allocates a new execution context.
            using var suppression = context.SuppressLoadErrorHandling();

            // Pull intervals keep hitsounds close to their simulation time without
            // tying judgement precision to the encoded video frame rate (target 240 Hz).
            for (int offset = 0; offset < audioSampleCount; offset += audio_step_samples)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int step = Math.Min(audio_step_samples, audioSampleCount - offset);
                // Trail lifetimes use output time, not wall time or rate-adjusted beatmap time.
                presentationClock.AdvanceTo((audioStartSample + offset) / 48.0);
                clock.CurrentTime = startTime + (audioStartSample + offset) * rate / 48.0;
                framedClock.ProcessFrame();
                context.UpdateScene(scene);
                if (!trackStarted && clock.CurrentTime >= 0 && track != null)
                {
                    track.Seek(clock.CurrentTime);
                    track.Start();
                    trackStarted = true;
                }
                audio.Update();
                if (offset == 0)
                    context.Render(scene, target);
                mixer.Read(intervalPcm, step);
                Array.Copy(intervalPcm, 0, pcm, offset * 2, step * 2);
            }
            recording.WriteFrame(target.Texture, pcm);
            return Task.CompletedTask;
        }

        public Task CompleteAsync(CancellationToken cancellationToken)
        {
            checkThread();
            cancellationToken.ThrowIfCancellationRequested();
            recording.Complete();
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, destination, false);
            published = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            checkThread();
            if (disposed)
                return ValueTask.CompletedTask;
            disposed = true;
            try { disposeResources(); }
            finally
            {
                if (!published && File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            return ValueTask.CompletedTask;
        }

        private T own<T>(T resource) where T : IDisposable
        {
            resources.Add(resource);
            return resource;
        }

        private void disposeResources()
        {
            // Audio disposal is queued; drain it while the private mixer and device exist.
            Exception? failure = null;
            for (int i = resources.Count - 1; i >= 0; i--)
            {
                try
                {
                    resources[i].Dispose();
                    if (ReferenceEquals(resources[i], audio))
                        audio.Update();
                }
                catch (Exception ex) { failure ??= ex; }
            }
            resources.Clear();
            if (failure != null)
                throw new InvalidOperationException(@"Unable to release all replay export resources.", failure);
        }

        private void checkThread()
        {
            if (Environment.CurrentManagedThreadId != ownerThread)
                throw new InvalidOperationException(@"Replay export must remain on its dedicated worker thread.");
        }

        /// <summary>
        /// Disposes the scene through its render context, which treats the export thread as the scene's update thread.
        /// </summary>
        private sealed class SceneOwnership : IDisposable
        {
            private readonly OfflineRenderContext context;
            private readonly Drawable scene;

            public SceneOwnership(OfflineRenderContext context, Drawable scene)
            {
                this.context = context;
                this.scene = scene;
            }

            public void Dispose() => context.DisposeScene(scene);
        }

        private sealed class OfflineFrameworkConfigManager : FrameworkConfigManager
        {
            protected override string? Filename => null;

            public OfflineFrameworkConfigManager() : base(null!)
            {
            }
        }
    }
}
