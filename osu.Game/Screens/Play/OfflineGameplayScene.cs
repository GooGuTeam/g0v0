// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Containers;
using osu.Game.IO;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Scoring;
using osu.Game.Screens.Backgrounds;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// Private gameplay tree for offscreen export. Uses the production ruleset, judgement
    /// processors, skin hierarchy and HUD; never borrows a loaded drawable from the host.
    /// The caller must provide private renderer, texture, skin and audio dependencies.
    /// </summary>
    public partial class OfflineGameplayScene : CompositeDrawable, IBeatSyncProvider
    {
        private readonly WorkingBeatmap workingBeatmap;
        private readonly Score replay;
        private readonly ManualAdjustableClock sourceClock;
        private readonly Ruleset ruleset;
        private readonly ISkin beatmapSkin;
        private readonly ISkin classicSkin;
        private readonly bool userSkinIsLegacy;

        ControlPointInfo IBeatSyncProvider.ControlPoints => State.Beatmap.ControlPointInfo;
        IClock IBeatSyncProvider.Clock => sourceClock;
        ChannelAmplitudes IHasAmplitudes.CurrentAmplitudes => ChannelAmplitudes.Empty;
        private DependencyContainer dependencies = null!;

        [Resolved]
        private IStorageResourceProvider storageProvider { get; set; } = null!;

        public DrawableRuleset DrawableRuleset { get; private set; } = null!;
        public GameplayState State { get; private set; } = null!;

        public OfflineGameplayScene(WorkingBeatmap workingBeatmap, Score replay, ManualAdjustableClock sourceClock,
                                    ISkin beatmapSkin, ISkin classicSkin, int width, int height, bool userSkinIsLegacy)
        {
            if (replay.Replay == null)
                throw new ArgumentException(@"Offline export requires actual replay data.", nameof(replay));

            this.workingBeatmap = workingBeatmap;
            this.replay = replay;
            this.sourceClock = sourceClock;
            this.beatmapSkin = beatmapSkin;
            this.classicSkin = classicSkin;
            this.userSkinIsLegacy = userSkinIsLegacy;
            ruleset = replay.ScoreInfo.Ruleset.CreateInstance();
            Size = new Vector2(width, height);
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            => dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            var mods = replay.ScoreInfo.Mods.Select(m => m.DeepClone()).ToArray();
            var beatmap = workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo, mods);
            if (beatmap.HitObjects.Count == 0)
                throw new InvalidOperationException(@"Cannot export an empty beatmap.");

            beatmap.BeatmapInfo.Difficulty = beatmap.Difficulty.Clone();

            DrawableRuleset = ruleset.CreateDrawableRulesetWith(beatmap, mods);
            var scoreProcessor = ruleset.CreateScoreProcessor();
            scoreProcessor.Mods.Value = mods;
            scoreProcessor.ApplyBeatmap(beatmap);
            var healthProcessor = mods.OfType<IApplicableHealthProcessor>().FirstOrDefault()?.CreateHealthProcessor(beatmap.HitObjects[0].StartTime)
                                  ?? ruleset.CreateHealthProcessor(beatmap.HitObjects[0].StartTime);
            healthProcessor.ApplyBeatmap(beatmap);
            scoreProcessor.OnLoadComplete += _ =>
            {
                foreach (var mod in mods.OfType<IApplicableToScoreProcessor>())
                    mod.ApplyToScoreProcessor(scoreProcessor);
            };
            healthProcessor.OnLoadComplete += _ =>
            {
                foreach (var mod in mods.OfType<IApplicableToHealthProcessor>())
                    mod.ApplyToHealthProcessor(healthProcessor);
            };

            var exportDifficulty = new ReplayExportDifficulty(workingBeatmap, ruleset, mods, scoreProcessor);
            beatmap.BeatmapInfo.StarRating = exportDifficulty.Difficulty.Stars;
            var playableWorkingBeatmap = new PlayableWorkingBeatmap(beatmap, workingBeatmap, exportDifficulty.Difficulty.Stars);

            State = new GameplayState(beatmap, ruleset, mods, replay, scoreProcessor, healthProcessor, workingBeatmap.Storyboard);
            dependencies.CacheAs(State);
            dependencies.CacheAs<IBeatSyncProvider>(this);
            dependencies.CacheAs<IBindable<WorkingBeatmap>>(new Bindable<WorkingBeatmap>(playableWorkingBeatmap));
            dependencies.CacheAs<IBindable<RulesetInfo>>(new Bindable<RulesetInfo>(ruleset.RulesetInfo));
            dependencies.CacheAs<IBindable<IReadOnlyList<Mod>>>(new Bindable<IReadOnlyList<Mod>>(mods));
            dependencies.CacheAs(exportDifficulty);
            dependencies.CacheAs(DrawableRuleset);
            dependencies.CacheAs<ScoreProcessor>(scoreProcessor);
            dependencies.CacheAs<HealthProcessor>(healthProcessor);

            var leaderboardProvider = new SoloGameplayLeaderboardProvider();
            dependencies.CacheAs<IGameplayLeaderboardProvider>(leaderboardProvider);
            dependencies.CacheAs(new OverlayColourProvider(OverlayColourScheme.Purple));

            if (DrawableRuleset is IDrawableScrollingRuleset scrollingRuleset)
                dependencies.CacheAs(scrollingRuleset.ScrollingInfo);

            DrawableRuleset.NewResult += result =>
            {
                healthProcessor.ApplyResult(result);
                scoreProcessor.ApplyResult(result);
                State.ApplyResult(result);
            };
            DrawableRuleset.RevertResult += result =>
            {
                healthProcessor.RevertResult(result);
                scoreProcessor.RevertResult(result);
            };

            var breakTracker = new BreakTracker(DrawableRuleset.GameplayStartTime, scoreProcessor)
            {
                Breaks = beatmap.Breaks
            };

            DrawableRuleset.FrameStableComponents.Children = new Drawable[]
            {
                scoreProcessor,
                healthProcessor,
                new ComboEffects(scoreProcessor),
                breakTracker,
            };
            var clock = new GameplayClockContainer(sourceClock, false, false, interpolate: false);
            var skin = new RulesetSkinProvidingContainer(ruleset, beatmap, beatmapSkin, classicSkin, userSkinIsLegacy: userSkinIsLegacy);
            config.BindWith(OsuSetting.BeatmapSkins, skin.BeatmapSkins);
            config.BindWith(OsuSetting.BeatmapColours, skin.BeatmapColours);
            config.BindWith(OsuSetting.BeatmapHitsounds, skin.BeatmapHitsounds);
            ((IBindable<bool>)DrawableRuleset.IsPaused).BindTo(clock.IsPaused);
            var hud = new HUDOverlay(DrawableRuleset, mods, new PlayerConfiguration
            {
                AllowUserInteraction = false,
                AllowPause = false,
                AllowRestart = false,
                AllowSkipping = false,
                ShowResults = false,
            });

            foreach (var mod in mods.OfType<IApplicableToHUD>())
                mod.ApplyToHUD(hud);

            var textures = new TextureStore(storageProvider.Renderer, new TextureLoaderStore(storageProvider.Files));
            backgroundTextures = textures;
            string? backgroundPath = workingBeatmap.BeatmapSetInfo.GetPathForFile(workingBeatmap.Metadata.BackgroundFile);

            // The layers below mirror Player and the beatmap background screen, so that user dim and blur, break lightening,
            // storyboard replacement and the storyboard overlay layer behave as in normal gameplay.
            var background = new Background();
            if (!string.IsNullOrEmpty(backgroundPath))
                background.Sprite.Texture = textures.Get(backgroundPath);

            var dimmableBackground = new BackgroundScreenBeatmap.DimmableBackground
            {
                RelativeSizeAxes = Axes.Both,
                Background = background,
            };
            dimmableBackground.BlurAmount.Value = 0;
            dimmableBackground.StoryboardReplacesBackground.Value = State.Storyboard.ReplacesBackground && State.Storyboard.HasDrawable;
            dimmableBackground.IsBreakTime.BindTo(breakTracker.IsBreakTime);

            var dimmableStoryboard = new DimmableStoryboard(State.Storyboard, mods) { RelativeSizeAxes = Axes.Both };
            dimmableStoryboard.IsBreakTime.BindTo(breakTracker.IsBreakTime);

            var letterbox = new LetterboxOverlay
            {
                BreakTracker = breakTracker,
                Alpha = beatmap.LetterboxInBreaks ? 1 : 0,
            };

            skin.AddRange(new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        dimmableStoryboard,
                        letterbox,
                        new KiaiGameplayFountains(),
                    },
                },
                new ScalingContainer(ScalingMode.Gameplay) { Child = DrawableRuleset },
            });
            clock.Add(skin);

            // In the game, every screen is laid out by the root draw-size-preserving container (1024x768 with the user's UI scale).
            // Without it, the HUD, background and storyboard would be sized in output pixels and change with the export resolution.
            InternalChildren = new Drawable[]
            {
                leaderboardProvider,
                new ScalingContainer.ScalingDrawSizePreservingFillContainer(true)
                {
                    Children = new Drawable[]
                    {
                        dimmableBackground,
                        clock,
                    },
                },
            };
            // The frame-stable clock is created during ruleset loading. Publish it to the HUD
            // afterwards, without making the ruleset resolve its own clock as its parent clock.
            var breakOverlay = new BreakOverlay(scoreProcessor)
            {
                Clock = DrawableRuleset.FrameStableClock,
                ProcessCustomClock = false,
                BreakTracker = breakTracker,
            };
            letterbox.Clock = DrawableRuleset.FrameStableClock;
            letterbox.ProcessCustomClock = false;
            hud.Anchor = Anchor.Centre;
            hud.Origin = Anchor.Centre;
            skin.Add(new OfflineHUDContainer(DrawableRuleset.FrameStableClock)
            {
                Children = new Drawable[]
                {
                    dimmableStoryboard.OverlayLayerContainer.CreateProxy(),
                    hud,
                    breakOverlay,
                },
            });
            skin.Add(DrawableRuleset.Cursor?.CreateProxy() ?? new Container());
            if (!DrawableRuleset.AllowGameplayOverlays)
            {
                hud.ShowHud.Value = false;
                hud.ShowHud.Disabled = true;
                breakOverlay.Hide();
            }
            clock.Reset(sourceClock.CurrentTime, startClock: true);
        }

        private partial class OfflineHUDContainer : Container
        {
            private readonly IFrameStableClock gameplayClock;

            public OfflineHUDContainer(IFrameStableClock gameplayClock)
            {
                this.gameplayClock = gameplayClock;
                RelativeSizeAxes = Axes.Both;
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var hudDependencies = new DependencyContainer(base.CreateChildDependencies(parent));
                hudDependencies.CacheAs(gameplayClock);
                hudDependencies.CacheAs<IGameplayClock>(gameplayClock);
                return hudDependencies;
            }
        }

        private TextureStore? backgroundTextures;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            backgroundTextures?.Dispose();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            DrawableRuleset.SetReplayScore(replay);
        }
    }
}