// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    [HeadlessTest]
    public partial class TestSceneSpinnerSeek : OsuTestScene
    {
        private ManualClock clock = null!;
        private ModAppliedPlayfield playfield = null!;
        private Spinner spinner = null!;

        protected override Ruleset CreateRuleset() => new OsuRuleset();

        [Test]
        public void TestClassicSpunOutForwardSeekCompletesSpinner()
        {
            OsuSpinnerJudgementResult? spinnerResult = null;

            AddStep("create CL+SO spinner playfield", () =>
            {
                spinnerResult = null;
                var mods = new IApplicableToDrawableHitObject[] { new OsuModClassic(), new OsuModSpunOut() };
                var beatmap = new OsuBeatmap();
                spinner = new Spinner
                {
                    StartTime = 1000,
                    Duration = 3000,
                    Position = new Vector2(256, 192),
                };

                foreach (var hitObjectMod in mods.OfType<IApplicableToHitObject>())
                    hitObjectMod.ApplyToHitObject(spinner);

                spinner.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                beatmap.HitObjects.Add(spinner);

                Child = new OsuInputManager(new OsuRuleset().RulesetInfo)
                {
                    Child = new BeatmapContainer(beatmap)
                    {
                        Child = playfield = new ModAppliedPlayfield(mods)
                        {
                            Clock = new FramedClock(clock = new ManualClock { CurrentTime = -2000 }),
                        },
                    },
                };

                playfield.NewResult += (_, r) =>
                {
                    if (r is OsuSpinnerJudgementResult sr)
                        spinnerResult = sr;
                };

                playfield.Add(spinner);
            });

            AddUntilStep("playfield loaded", () => playfield.IsLoaded);

            AddStep("seek forward into middle of spinner", () => clock.CurrentTime = 2500);
            AddWaitStep("process mid-spinner seek", 3);
            AddAssert("mid-spinner seek accumulated rotation", () =>
            {
                var drawable = playfield.HitObjectContainer.AliveObjects.OfType<DrawableSpinner>().SingleOrDefault();
                return drawable != null && drawable.Result.TotalRotation > 0;
            });

            AddStep("seek backward before lifetime and jump past spinner end", () => clock.CurrentTime = -2000);
            AddWaitStep("process rewind", 3);
            AddStep("jump directly past spinner end", () => clock.CurrentTime = 4050);
            AddWaitStep("process forward jump", 3);

            AddStep("spinner judged as Great with non-zero rotation", () =>
            {
                Assert.That(spinnerResult, Is.Not.Null);
                Assert.That(spinnerResult!.TotalRotation, Is.GreaterThan(0));
                Assert.That(spinnerResult.Type, Is.EqualTo(HitResult.Great));
            });
        }

        [Test]
        public void TestLegacyScoreV1BackwardSeekDoesNotRejudgeTicksFromStalePeak()
        {
            DrawableSpinner drawableSpinner = null!;

            AddStep("create ScoreV1 spinner playfield", () =>
            {
                var beatmap = new OsuBeatmap();
                spinner = new Spinner
                {
                    StartTime = 1000,
                    Duration = 3000,
                    Position = new Vector2(256, 192),
                };
                spinner.ApplyLegacyScoreV1(new ControlPointInfo(), new BeatmapDifficulty());
                beatmap.HitObjects.Add(spinner);

                Child = new OsuInputManager(new OsuRuleset().RulesetInfo)
                {
                    Child = new BeatmapContainer(beatmap)
                    {
                        Child = playfield = new ModAppliedPlayfield(Array.Empty<IApplicableToDrawableHitObject>())
                        {
                            Clock = new FramedClock(clock = new ManualClock { CurrentTime = 1000, Rate = 1 }),
                        },
                    },
                };

                playfield.Add(spinner);
            });

            AddUntilStep("playfield loaded", () => playfield.IsLoaded);
            AddStep("retrieve drawable spinner", () =>
            {
                drawableSpinner = playfield.HitObjectContainer.AliveObjects.OfType<DrawableSpinner>().Single();
                drawableSpinner.HandleUserInput = false;
            });

            AddRepeatStep("spin forward across multiple ticks", () =>
            {
                clock.CurrentTime += 50;
                drawableSpinner.RotationTracker.Tracking = true;
                drawableSpinner.RotationTracker.AddRotation(180f);
            }, 20);
            AddWaitStep("update judgements", 2);
            AddAssert("ticks judged and progress completed", () =>
                drawableSpinner.NestedHitObjects.Any(n => n.IsHit)
                && drawableSpinner.CompletedFullSpins.Value > 0
                && drawableSpinner.Result.TimeStarted != null
                && drawableSpinner.Result.TimeCompleted != null);

            AddStep("seek backward before spinner start", () =>
            {
                drawableSpinner.RotationTracker.Tracking = false;
                clock.CurrentTime = 800;
            });
            AddWaitStep("process rewind", 3);

            AddAssert("state reset after rewind", () =>
                drawableSpinner.Result.TotalRotation == 0
                && drawableSpinner.CompletedFullSpins.Value == 0
                && drawableSpinner.Result.TimeStarted == null
                && drawableSpinner.Result.TimeCompleted == null
                && drawableSpinner.NestedHitObjects.All(n => !n.Judged));

            AddStep("advance past start without spinning", () => clock.CurrentTime = 1200);
            AddWaitStep("process forward frame", 2);
            AddAssert("reverted ticks stay unjudged without new spin", () => drawableSpinner.NestedHitObjects.All(n => !n.Judged));
        }

        private partial class ModAppliedPlayfield : OsuPlayfield
        {
            private readonly IReadOnlyList<IApplicableToDrawableHitObject> mods;

            public ModAppliedPlayfield(IReadOnlyList<IApplicableToDrawableHitObject> mods)
            {
                this.mods = mods;
            }

            protected override void OnNewDrawableHitObject(DrawableHitObject drawable)
            {
                base.OnNewDrawableHitObject(drawable);

                foreach (var mod in mods)
                    mod.ApplyToDrawableHitObject(drawable);
            }
        }

        private partial class BeatmapContainer : Container
        {
            [Cached(typeof(IBeatmap))]
            private readonly IBeatmap beatmap;

            public BeatmapContainer(IBeatmap beatmap)
            {
                this.beatmap = beatmap;
                RelativeSizeAxes = Axes.Both;
            }
        }
    }
}
