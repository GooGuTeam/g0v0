// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// What osu!stable shows during breaks (<c>Player.InitializeBreaks</c>, <c>showBreakRanking</c> and <c>ShowBreakInfo</c>): the warning arrows
    /// that blink in the corners as a break ends, the skin's <c>section-pass</c> or <c>section-fail</c> with its sound part way through a long
    /// break. Rank display remains owned by the skin's HUD components.
    /// </summary>
    public partial class LegacyBreakOverlay : CompositeDrawable
    {
        // stable lays its HUD out in 640x480 units, which g0v0 draws at 1.6x. Textures are drawn a pixel each at 768 high, so aren't scaled.
        private const float scale = 1.6f;

        /// <summary>
        /// The arrows' distance from each corner of the screen.
        /// </summary>
        private static readonly Vector2 arrow_offset = new Vector2(80, 100) * scale;

        /// <summary>
        /// A break needs to be longer than this for stable to rate the section.
        /// </summary>
        private const double section_rating_min_length = 2880;

        private readonly BreakTracker breakTracker;

        [Resolved]
        private GameplayState gameplayState { get; set; } = null!;

        [Resolved]
        private ScoreProcessor scoreProcessor { get; set; } = null!;

        [Resolved]
        private HealthProcessor healthProcessor { get; set; } = null!;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        private readonly IBindable<Period?> currentPeriod = new Bindable<Period?>();

        private Container arrows = null!;
        private Sprite? sectionPass;
        private Sprite? sectionFail;
        private SkinnableSound sectionPassSample = null!;
        private SkinnableSound sectionFailSample = null!;

        private List<BreakPeriod> breaks = null!;

        /// <summary>
        /// Taiko and mania have no warning arrows, and rate no sections.
        /// </summary>
        private bool allowWarningArrows;

        private (double time, SkinnableSound sample)? pendingSample;

        public override bool RemoveCompletedTransforms => false;

        public LegacyBreakOverlay(BreakTracker breakTracker)
        {
            this.breakTracker = breakTracker;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            allowWarningArrows = gameplayState.Ruleset.RulesetInfo.ShortName is not (@"taiko" or @"mania");
            breaks = gameplayState.Beatmap.Breaks.Where(b => b.HasEffect).ToList();

            InternalChildren = new Drawable[]
            {
                arrows = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                },
                sectionPassSample = new SkinnableSound(new SampleInfo(@"sectionpass")),
                sectionFailSample = new SkinnableSound(new SampleInfo(@"sectionfail")),
            };

            if (!allowWarningArrows)
                return;

            // stable only tints the old play-warningarrow, and only for skins made for its 2.0 layout.
            var arrowTexture = skin.GetStableTexture(@"arrow-warning");
            var arrowColour = Color4.White;

            if (arrowTexture == null)
            {
                arrowTexture = skin.GetStableTexture(@"play-warningarrow") ?? skins.DefaultClassicSkin.GetStableTexture(@"play-warningarrow");

                decimal? version = skin.GetConfig<SkinConfiguration.LegacySetting, decimal>(SkinConfiguration.LegacySetting.Version)?.Value;
                if (version is not decimal v || v > 1)
                    arrowColour = Color4.Red;
            }

            arrows.Children = new Drawable[]
            {
                createArrow(Anchor.TopLeft, arrowTexture, arrowColour, false),
                createArrow(Anchor.BottomLeft, arrowTexture, arrowColour, false),
                createArrow(Anchor.TopRight, arrowTexture, arrowColour, true),
                createArrow(Anchor.BottomRight, arrowTexture, arrowColour, true),
            };

            AddRangeInternal(new Drawable[]
            {
                sectionPass = createSectionSprite(@"section-pass"),
                sectionFail = createSectionSprite(@"section-fail"),
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (allowWarningArrows)
            {
                double preempt = IBeatmapDifficultyInfo.DifficultyRange(gameplayState.Beatmap.Difficulty.ApproachRate, 1800, 1200, 450);

                foreach (var b in breaks)
                    applyArrowBlinks(b, preempt);
            }

            currentPeriod.BindTo(breakTracker.CurrentPeriod);
            currentPeriod.BindValueChanged(p =>
            {
                if (p.NewValue is not Period period)
                    return;

                var b = breaks.FirstOrDefault(br => Precision.AlmostEquals(br.StartTime, period.Start));

                if (b != null)
                    breakStarted(b);
            });
        }

        protected override void Update()
        {
            base.Update();

            if (pendingSample is not var (time, sample))
                return;

            if (Time.Current >= time)
            {
                pendingSample = null;

                // a seek far past the moment means it was skipped over rather than reached.
                if (Time.Current - time < 200)
                    sample.Play();
            }
            else if (time - Time.Current > section_rating_min_length)
                pendingSample = null;
        }

        /// <summary>
        /// stable blinks the arrows on for 100ms every 200ms up to the break's end, carrying on into the following objects' approach.
        /// </summary>
        private void applyArrowBlinks(BreakPeriod b, double preempt)
        {
            int intoApproach = Math.Min(2, (int)preempt / 200);
            int blinks = Math.Min(5, ((int)b.Duration + 200) / 200) + intoApproach;
            double start = b.EndTime - 200 * (blinks - intoApproach);

            for (int i = 0; i < blinks; i++)
            {
                using (BeginAbsoluteSequence(start + i * 200))
                    arrows.FadeIn().Delay(100).FadeOut();
            }
        }

        private void breakStarted(BreakPeriod b)
        {
            bool over = healthProcessor.HasFailed || scoreProcessor.HasCompleted.Value;

            if (allowWarningArrows && b.Duration > section_rating_min_length && !over)
                showSectionRating(b);

        }

        /// <summary>
        /// stable's <c>showBreakRanking</c>: halfway through the break, or nearer its end for shorter ones, a pass flickers three times or a
        /// fail twice before holding for a second.
        /// </summary>
        private void showSectionRating(BreakPeriod b)
        {
            bool passing = healthProcessor.Health.Value >= 0.5;
            double time = b.Duration / 2 > section_rating_min_length ? b.StartTime + b.Duration / 2 : b.EndTime - section_rating_min_length;

            var sprite = passing ? sectionPass : sectionFail;

            sectionPass?.ClearTransforms();
            sectionFail?.ClearTransforms();
            sectionPass?.FadeOut();
            sectionFail?.FadeOut();

            if (passing)
            {
                pendingSample = (time + 20, sectionPassSample);

                using (BeginAbsoluteSequence(time + 20))
                {
                    sprite?.FadeIn().Delay(80).FadeOut()
                          .Delay(60).FadeIn().Delay(70).FadeOut()
                          .Delay(50).FadeIn().Delay(1000).FadeOut(200);
                }
            }
            else
            {
                pendingSample = (time + 130, sectionFailSample);

                using (BeginAbsoluteSequence(time + 130))
                {
                    sprite?.FadeIn().Delay(100).FadeOut()
                          .Delay(50).FadeIn().Delay(1000).FadeOut(200);
                }
            }
        }

        private Sprite createSectionSprite(string name) => new Sprite
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Texture = skin.GetStableTexture(name) ?? skins.DefaultClassicSkin.GetStableTexture(name),
            Alpha = 0,
        };

        private static Sprite createArrow(Anchor corner, Texture? texture, Color4 colour, bool flipped) => new Sprite
        {
            Anchor = corner,
            Origin = Anchor.Centre,
            Texture = texture,
            Colour = colour,
            // the right arrows are the left ones flipped.
            Scale = new Vector2(flipped ? -1 : 1, 1),
            Position = new Vector2(
                corner.HasFlag(Anchor.x2) ? -arrow_offset.X : arrow_offset.X,
                corner.HasFlag(Anchor.y2) ? -arrow_offset.Y : arrow_offset.Y),
        };
    }
}
