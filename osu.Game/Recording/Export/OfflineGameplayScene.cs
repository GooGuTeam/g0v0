// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics.Containers;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Backgrounds;
using osu.Game.Skinning;
using osuTK;
using osu.Game.Screens;
using osu.Game.Screens.Play;

namespace osu.Game.Recording.Export
{
    /// <summary>
    /// Private gameplay tree for offscreen export. Runs an <see cref="ExportReplayPlayer"/> in its own screen stack and provides
    /// what the game would otherwise provide to it. Never borrows a loaded drawable from the host; the caller must provide
    /// private renderer, texture, skin and audio dependencies, and a working beatmap serving private resources.
    /// </summary>
    public partial class OfflineGameplayScene : CompositeDrawable, IBeatSyncProvider
    {
        private readonly WorkingBeatmap workingBeatmap;
        private readonly IReadOnlyList<Mod> mods;
        private readonly Score replay;
        private readonly IClock sourceClock;
        private readonly ISkin classicSkin;
        private readonly bool userSkinIsLegacy;
        private readonly ReplayExportDifficultyCache difficulty;

        private DependencyContainer dependencies = null!;
        private OsuScreenStack screenStack = null!;
        private ExportReplayPlayer player = null!;

        ControlPointInfo IBeatSyncProvider.ControlPoints => workingBeatmap.Beatmap.ControlPointInfo;
        IClock IBeatSyncProvider.Clock => sourceClock;
        ChannelAmplitudes IHasAmplitudes.CurrentAmplitudes => ChannelAmplitudes.Empty;

        /// <param name="workingBeatmap">The modded beatmap, serving the export's private track, skin and background.</param>
        /// <param name="mods">The replay's mods; the player clones them.</param>
        /// <param name="replay">The replay to play.</param>
        /// <param name="sourceClock">The export clock, in beatmap time.</param>
        /// <param name="classicSkin">The export's private classic skin.</param>
        /// <param name="userSkinIsLegacy">Whether the selected user skin is a legacy skin.</param>
        /// <param name="difficulty">Difficulty computed before export, standing in for the game's difficulty cache.</param>
        /// <param name="width">The output width in pixels.</param>
        /// <param name="height">The output height in pixels.</param>
        public OfflineGameplayScene(WorkingBeatmap workingBeatmap, IReadOnlyList<Mod> mods, Score replay, IClock sourceClock,
                                    ISkin classicSkin, bool userSkinIsLegacy, ReplayExportDifficultyCache difficulty, int width, int height)
        {
            if (replay.Replay == null)
                throw new ArgumentException(@"Offline export requires actual replay data.", nameof(replay));

            this.workingBeatmap = workingBeatmap;
            this.mods = mods;
            this.replay = replay;
            this.sourceClock = sourceClock;
            this.classicSkin = classicSkin;
            this.userSkinIsLegacy = userSkinIsLegacy;
            this.difficulty = difficulty;
            Size = new Vector2(width, height);
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            => dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

        [BackgroundDependencyLoader]
        private void load()
        {
            // Screens lease these from the game; the same bindables are exposed read-only for the other export components.
            var beatmapBindable = new Bindable<WorkingBeatmap>(workingBeatmap);
            var rulesetBindable = new Bindable<RulesetInfo>(replay.ScoreInfo.Ruleset);
            var modsBindable = new Bindable<IReadOnlyList<Mod>>(mods);
            dependencies.CacheAs(beatmapBindable);
            dependencies.CacheAs<IBindable<WorkingBeatmap>>(beatmapBindable);
            dependencies.CacheAs(rulesetBindable);
            dependencies.CacheAs<IBindable<RulesetInfo>>(rulesetBindable);
            dependencies.CacheAs(modsBindable);
            dependencies.CacheAs<IBindable<IReadOnlyList<Mod>>>(modsBindable);
            dependencies.CacheAs<IBeatSyncProvider>(this);

            // osu-framework's Screen requires a Game. None of the screens used here access it, and the host's game must not be borrowed.
            dependencies.CacheAs<Framework.Game>(new PlaceholderGame());
            dependencies.CacheAs<BeatmapDifficultyCache>(difficulty);

            // The pause and fail overlays resolve the global action container. Export has no input, so no handler is attached.
            var globalActions = new GlobalActionContainer(null);
            dependencies.CacheAs(globalActions);

            // In the game, every screen is laid out by the root draw-size-preserving container (1024x768 with the user's UI scale).
            // Without it, the HUD, background and storyboard would be sized in output pixels and change with the export resolution.
            globalActions.Child = new ScalingContainer.ScalingDrawSizePreservingFillContainer(true)
            {
                Child = screenStack = new OsuScreenStack { RelativeSizeAxes = Axes.Both },
            };
            InternalChild = globalActions;

            // Loaded here rather than by the stacks, which load asynchronously, so that the first exported frame already shows them.
            var background = new BackgroundScreenBeatmap(workingBeatmap);
            LoadComponent(background);
            player = new ExportReplayPlayer(replay, sourceClock, classicSkin, userSkinIsLegacy, background);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            screenStack.PushSynchronously(player);
        }

        /// <summary>
        /// Satisfies <see cref="Framework.Screens.Screen"/>'s dependency on a game. Never loaded.
        /// </summary>
        private partial class PlaceholderGame : Framework.Game
        {
        }
    }
}
