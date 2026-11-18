// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Video;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Recording;
using osu.Game.Recording.Export;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>
    /// Exports a few seconds of a replay through the offline export path.
    /// Covers mods that rely on <see cref="IApplicableToPlayer"/>, which export applies through <see cref="ExportReplayPlayer"/>, and the autoplay path.
    /// </summary>
    public partial class TestSceneOfflineReplayExport : OsuTestScene
    {
        private const int frame_rate = 60;
        private const int audio_frames_per_video_frame = 48000 / frame_rate;
        private const int seconds = 4;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        [Test]
        public void TestDifficultyCacheRespectsModSpecificSetting()
        {
            double modSpecificStars = 0;
            double standardStars = 0;
            double expectedModSpecificStars = 0;
            double expectedStandardStars = 0;

            AddStep("calculate difficulties", () =>
            {
                bool previous = ModSpecificCalculation.Enabled;

                try
                {
                    var ruleset = new OsuRuleset();
                    var beatmap = CreateWorkingBeatmap(ruleset.RulesetInfo);
                    Mod[] mods = { new OsuModRelax() };
                    var playable = beatmap.GetPlayableBeatmap(ruleset.RulesetInfo, mods);
                    var processor = ruleset.CreateScoreProcessor();
                    processor.Mods.Value = mods;
                    processor.ApplyBeatmap(playable);

                    expectedModSpecificStars = ruleset.CreateModSpecificDifficultyCalculator(beatmap).Calculate(mods).StarRating;
                    expectedStandardStars = ruleset.CreateDifficultyCalculator(beatmap).Calculate(mods).StarRating;

                    ModSpecificCalculation.Enabled = false;
                    using var modSpecificCache = new ReplayExportDifficultyCache(beatmap, ruleset, mods, processor, useModSpecific: true);
                    modSpecificCache.Publish();
                    modSpecificStars = modSpecificCache.GetBindableDifficulty(beatmap.BeatmapInfo).Value.Stars;

                    ModSpecificCalculation.Enabled = true;
                    using var standardCache = new ReplayExportDifficultyCache(beatmap, ruleset, mods, processor, useModSpecific: false);
                    standardCache.Publish();
                    standardStars = standardCache.GetBindableDifficulty(beatmap.BeatmapInfo).Value.Stars;
                }
                finally
                {
                    ModSpecificCalculation.Enabled = previous;
                }
            });

            AddAssert("mod-specific stars match mod-specific calculator", () => osu.Framework.Utils.Precision.AlmostEquals(modSpecificStars, expectedModSpecificStars));
            AddAssert("standard stars match standard calculator", () => osu.Framework.Utils.Precision.AlmostEquals(standardStars, expectedStandardStars));
            AddAssert("mod-specific and standard stars differ under Relax", () => !osu.Framework.Utils.Precision.AlmostEquals(modSpecificStars, standardStars));
        }

        [Test]
        public void TestOfflineGameplaySceneSnapsUiScaleImmediately()
        {
            OfflineExportConfigManager? exportConfig = null;
            ReplayExportDifficultyCache? difficulty = null;
            OfflineGameplayScene scene = null!;

            AddStep("create offline scene with custom UI scale", () =>
            {
                var ruleset = new OsuRuleset();
                var beatmap = CreateWorkingBeatmap(ruleset.RulesetInfo);
                beatmap.LoadTrack();

                var request = ReplayExportRequest.FromAutoplay(beatmap, ruleset.RulesetInfo, Array.Empty<Mod>());
                var mods = request.Score.ScoreInfo.Mods.Select(m => m.DeepClone()).ToArray();
                var playable = beatmap.GetPlayableBeatmap(ruleset.RulesetInfo, mods);
                var processor = ruleset.CreateScoreProcessor();
                processor.Mods.Value = mods;
                processor.ApplyBeatmap(playable);

                exportConfig = new OfflineExportConfigManager(new Dictionary<OsuSetting, string>
                {
                    [OsuSetting.UIScale] = @"1.4",
                });
                difficulty = new ReplayExportDifficultyCache(beatmap, ruleset, mods, processor);
                var clock = new ManualAdjustableClock { CurrentTime = request.StartTime, IsRunning = true };

                Child = new ExportConfigContainer(exportConfig)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = scene = new OfflineGameplayScene(beatmap, mods, request.Score, clock, skins.DefaultClassicSkin, false, difficulty, 640, 360),
                };

                difficulty.Publish();
            });

            AddAssert("UI scale snapped without zoom animation",
                () => scene.ChildrenOfType<ScalingContainer.ScalingDrawSizePreservingFillContainer>().First().Scale == new Vector2(1.4f));
            AddAssert("export player loaded", () => scene.ChildrenOfType<ExportReplayPlayer>().Single().IsLoaded);

            AddStep("clean up", () =>
            {
                Clear();
                difficulty?.Dispose();
                exportConfig?.Dispose();
            });
        }

        [Test]
        public void TestLegacyModDisplayMapsDualStagesToKeycoop()
        {
            LegacyModDisplay display = null!;
            KeycoopTestSkin? skin = null;

            AddStep("create legacy mod display", () =>
            {
                skin = new KeycoopTestSkin(renderer.WhitePixel);
                Child = new ManiaDualStagesContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new SkinProvidingContainer(skin)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = display = new LegacyModDisplay(),
                    },
                };
            });

            AddUntilStep("keycoop mod icon created", () => display.ChildrenOfType<Sprite>().Count() == 1);
            AddStep("clean up", () =>
            {
                Clear();
                skin?.Dispose();
            });
        }

        [Test]
        [Explicit("Renders on a real GPU device.")]
        public void TestExportWithPlayerMods()
        {
            long writtenFrames = 0;

            AddStep("export", () =>
            {
                var ruleset = new OsuRuleset();
                var beatmap = CreateWorkingBeatmap(ruleset.RulesetInfo);
                var autoplay = new OsuModAutoplay();
                var playable = beatmap.GetPlayableBeatmap(ruleset.RulesetInfo, new Mod[] { autoplay });
                var replay = autoplay.CreateReplayData(playable, new Mod[] { autoplay }).Replay;

                // Relax crashed in export while ApplyToPlayer was not called; Bloom and No Scope bind to the player's break state.
                var score = new Score
                {
                    Replay = replay,
                    ScoreInfo = new ScoreInfo
                    {
                        Ruleset = ruleset.RulesetInfo,
                        Mods = new Mod[] { new OsuModRelax(), new OsuModBloom(), new OsuModNoScope() },
                        BeatmapInfo = beatmap.BeatmapInfo,
                    },
                };

                writtenFrames = export(ReplayExportRequest.FromScore(beatmap, score), beatmap, null);
            });

            AddAssert("all frames written", () => writtenFrames == frame_rate * seconds);
        }

        [Test]
        [Explicit("Renders on a real GPU device.")]
        public void TestExportAutoplay()
        {
            long writtenFrames = 0;

            AddStep("export", () =>
            {
                var ruleset = new OsuRuleset();
                var beatmap = CreateWorkingBeatmap(ruleset.RulesetInfo);

                writtenFrames = export(ReplayExportRequest.FromAutoplay(beatmap, ruleset.RulesetInfo, new Mod[] { new OsuModHidden() }), beatmap, null);
            });

            AddAssert("all frames written", () => writtenFrames == frame_rate * seconds);
        }

        [Test]
        [Explicit("Renders on a real GPU device.")]
        public void TestExportAutoplayToFile()
        {
            string destination = Path.Combine(Path.GetTempPath(), $@"g0v0-export-test-{Guid.NewGuid():N}.mp4");
            long length = 0;

            AddStep("export", () =>
            {
                var ruleset = new OsuRuleset();
                var beatmap = CreateWorkingBeatmap(ruleset.RulesetInfo);

                export(ReplayExportRequest.FromAutoplay(beatmap, ruleset.RulesetInfo, Array.Empty<Mod>()), beatmap, destination);
                length = new FileInfo(destination).Length;
            });

            AddAssert("file written", () => length > 0);
            AddStep("delete file", () => File.Delete(destination));
        }

        /// <summary>
        /// Exports the first few seconds of gameplay on a worker thread, as <see cref="ReplayExportJob"/> does.
        /// </summary>
        /// <param name="request">The replay to export.</param>
        /// <param name="beatmap">The beatmap, to start shortly before its first object.</param>
        /// <param name="destination">Where to encode with the automatic backend, or <c>null</c> to discard the frames.</param>
        /// <returns>The number of frames written.</returns>
        private long export(ReplayExportRequest request, WorkingBeatmap beatmap, string? destination)
        {
            Exception? failure = null;
            long writtenFrames = 0;

            double startTime = beatmap.Beatmap.HitObjects.First().StartTime - 1000;
            string output = destination ?? Path.Combine(Path.GetTempPath(), $@"g0v0-export-test-{Guid.NewGuid():N}.mp4");

            var worker = new Thread(() =>
            {
                try
                {
                    IOfflineVideoRecording? recording = null;
                    Func<OfflineRenderContext, IOfflineVideoRecording>? factory = destination == null ? _ => recording = new DiscardingRecording() : null;

                    var session = new OfflineReplayExportSession(request.StartingAt(startTime), ArgonSkin.CreateInfo(), skins, storage, new Dictionary<OsuSetting, string>(),
                        typeof(OsuRuleset), new Dictionary<string, string>(), output, new VideoRecordingOptions(frame_rate, width: 640, height: 360),
                        RecordingBackend.Automatic, @"ffmpeg", null, factory, CancellationToken.None);

                    try
                    {
                        for (int i = 0; i < frame_rate * seconds; i++)
                        {
                            session.EncodeFrameAsync(i, i * 1000L / frame_rate, (long)i * audio_frames_per_video_frame, audio_frames_per_video_frame, CancellationToken.None)
                                   .GetAwaiter().GetResult();
                        }

                        if (destination != null)
                            session.CompleteAsync(CancellationToken.None).GetAwaiter().GetResult();

                        writtenFrames = recording?.WrittenFrames ?? frame_rate * seconds;
                    }
                    finally
                    {
                        session.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    }
                }
                catch (Exception e)
                {
                    failure = e;
                }
            })
            { IsBackground = true, Name = @"Offline export test" };

            worker.Start();
            worker.Join();

            if (failure != null)
                ExceptionDispatchInfo.Capture(failure).Throw();

            return writtenFrames;
        }

        private sealed class DiscardingRecording : IOfflineVideoRecording
        {
            public long WrittenFrames { get; private set; }
            public long WrittenAudioFrames { get; private set; }
            public int AudioFramesPerVideoFrame => audio_frames_per_video_frame;

            public void WriteFrame(Veldrid.Texture texture, float[] audio)
            {
                WrittenFrames++;
                WrittenAudioFrames += audio_frames_per_video_frame;
            }

            public void Complete()
            {
            }

            public void Dispose()
            {
            }
        }

        private partial class ExportConfigContainer : Container
        {
            private readonly OfflineExportConfigManager config;

            public ExportConfigContainer(OfflineExportConfigManager config)
            {
                this.config = config;
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
                dependencies.CacheAs<OsuConfigManager>(config);
                dependencies.CacheAs<IGameplaySettings>(config);
                return dependencies;
            }
        }

        private partial class ManiaDualStagesContainer : Container
        {
            private readonly IBindable<IReadOnlyList<Mod>> mods = new Bindable<IReadOnlyList<Mod>>(new Mod[] { new ManiaModDualStages() });
            private readonly IBindable<RulesetInfo> ruleset = new Bindable<RulesetInfo>(new ManiaRuleset().RulesetInfo);

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
                dependencies.CacheAs(mods);
                dependencies.CacheAs(ruleset);
                return dependencies;
            }
        }

        private sealed class KeycoopTestSkin : LegacySkin
        {
            private readonly Texture whitePixel;

            public KeycoopTestSkin(Texture whitePixel)
                : base(new SkinInfo { Name = @"keycoop test" }, null, null, string.Empty)
            {
                this.whitePixel = whitePixel;
            }

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT)
                => componentName == @"selection-mod-keycoop" ? whitePixel : null;
        }
    }
}
