// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Screens;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Screens.Backgrounds;
using osu.Game.Skinning;
using osu.Game.Screens;
using osu.Game.Screens.Play;

namespace osu.Game.Recording.Export
{
    /// <summary>
    /// The <see cref="ReplayPlayer"/> used by offline replay export, so that mods, HUD, storyboard, background and fail handling
    /// go through the normal player lifecycle (including <see cref="Rulesets.Mods.IApplicableToPlayer"/>).
    /// Only what export needs differently is overridden: a deterministic clock driven by the export, private skins,
    /// a background loaded before the first frame, no entry transition and no replay controls.
    /// </summary>
    public partial class ExportReplayPlayer : ReplayPlayer
    {
        private readonly IClock sourceClock;
        private readonly ISkin classicSkin;
        private readonly bool userSkinIsLegacy;
        private readonly BackgroundScreenBeatmap background;

        protected override bool PauseOnFocusLost => false;

        /// <param name="score">The replay to play.</param>
        /// <param name="sourceClock">The export clock, in beatmap time. Gameplay starts at its time when the player is entered.</param>
        /// <param name="classicSkin">The export's private classic skin, used behind legacy user skins.</param>
        /// <param name="userSkinIsLegacy">Whether the selected user skin is a legacy skin.</param>
        /// <param name="background">The beatmap background, already loaded, so that it is shown from the first frame.</param>
        public ExportReplayPlayer(Score score, IClock sourceClock, ISkin classicSkin, bool userSkinIsLegacy, BackgroundScreenBeatmap background)
            : base(score, new PlayerConfiguration
            {
                AllowUserInteraction = false,
                AllowPause = false,
                AllowRestart = false,
                AllowSkipping = false,
                ShowResults = false,
            })
        {
            this.sourceClock = sourceClock;
            this.classicSkin = classicSkin;
            this.userSkinIsLegacy = userSkinIsLegacy;
            this.background = background;
        }

        protected override BackgroundScreen CreateBackground() => background;

        protected override GameplayClockContainer CreateGameplayClockContainer(WorkingBeatmap beatmap, double gameplayStart)
            => new GameplayClockContainer(sourceClock, false, false, interpolate: false);

        protected internal override RulesetSkinProvidingContainer CreateRulesetSkinProvider(Ruleset ruleset, IBeatmap beatmap, ISkin beatmapSkin)
            => new RulesetSkinProvidingContainer(ruleset, beatmap, beatmapSkin, classicSkin, userSkinIsLegacy: userSkinIsLegacy);

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Hidden rather than removed: rulesets add their replay settings to it later (see ReplayPlayer.AddSettings).
            if (LoadedBeatmapSuccessfully)
                ReplayOverlay.Alpha = 0;
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);

            // The video starts in gameplay, so skip the transitions into the player and its background.
            FinishTransforms(true);
            background.FinishTransforms(true);
        }

        protected override void StartGameplay() => GameplayClockContainer.Reset(sourceClock.CurrentTime, startClock: true);
    }
}
