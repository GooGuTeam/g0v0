// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    public partial class TestSceneClassicSliderInput : OsuManualInputManagerTestScene
    {
        private const double slider_start = 1500;

        [Cached(typeof(IGameplayClock))]
        private readonly TestGameplayClock gameplayClock = new TestGameplayClock();

        private OsuInputManager osuInputManager = null!;
        private DrawableSlider drawableSlider = null!;

        [TestCase(41.9, 20)]
        [TestCase(60.9, 30)]
        [TestCase(71.9, 35)]
        [TestCase(72.9, 36)]
        [TestCase(100.9, 64)]
        [TestCase(1000.9, 964)]
        public void TestIntegerTailCheckpoint(double duration, double checkpointOffset)
        {
            createSlider(duration);
            pressAt(0);
            seekTo(checkpointOffset - 0.1);
            AddAssert("tail not judged before checkpoint", () => !drawableSlider.TailCircle.Judged);
            seekTo(checkpointOffset);
            AddAssert("tail hit at integer checkpoint", () => drawableSlider.TailCircle.Result.Type, () => Is.EqualTo(HitResult.SmallTickHit));
            AddAssert("tail judged at checkpoint", () => drawableSlider.TailCircle.Result.TimeAbsolute, () => Is.EqualTo(slider_start + checkpointOffset).Within(0.000001));
            AddAssert("parent waits for actual end time", () => !drawableSlider.Judged);
            seekTo(duration + 0.1);
            AddAssert("parent completed", () => drawableSlider.Result.Type, () => Is.EqualTo(HitResult.Great));
        }

        [TestCase(20, true)]
        [TestCase(29.9, true)]
        [TestCase(30, true)]
        [TestCase(30.1, false)]
        [TestCase(40, false)]
        public void TestLateHeadCannotRecoverTail(double headOffset, bool expectedHit)
        {
            createSlider(60.9);
            pressAt(headOffset);
            seekTo(61);
            AddAssert("late head is hit", () => drawableSlider.HeadCircle.IsHit);
            AddAssert("tail respects tracking start", () => drawableSlider.TailCircle.IsHit, () => Is.EqualTo(expectedHit));
            AddAssert("tracking begins at head input", () => drawableSlider.Result.TrackingHistory.First(h => h.tracking).time, () => Is.EqualTo(slider_start + headOffset));
        }

        [TestCase(0, 3)]
        [TestCase(50, 3)]
        [TestCase(50.1, 2)]
        [TestCase(100, 2)]
        [TestCase(100.1, 1)]
        [TestCase(120, 1)]
        public void TestLateHeadCannotRecoverTicks(double headOffset, int expectedHits)
        {
            // Checkpoints at 50, 100, 150 and the tail at 164ms.
            createSlider(200, tickRate: 20);
            pressAt(headOffset);
            seekTo(201);
            AddAssert("late head is hit", () => drawableSlider.HeadCircle.IsHit);
            AddAssert("only future ticks hit", () => drawableSlider.NestedHitObjects.OfType<DrawableSliderTick>().Count(t => t.IsHit), () => Is.EqualTo(expectedHits));
            AddAssert("future tail hit", () => drawableSlider.TailCircle.IsHit);
        }

        [TestCase(0, 3)]
        [TestCase(60, 3)]
        [TestCase(60.1, 2)]
        [TestCase(120, 2)]
        [TestCase(120.1, 1)]
        [TestCase(149, 1)]
        public void TestLateHeadCannotRecoverRepeats(double headOffset, int expectedHits)
        {
            // Repeat checkpoints at 60, 120 and 180ms.
            createSlider(240, repeatCount: 3);
            pressAt(headOffset);
            seekTo(241);
            AddAssert("late head is hit", () => drawableSlider.HeadCircle.IsHit);
            AddAssert("only future repeats hit", () => drawableSlider.NestedHitObjects.OfType<DrawableSliderRepeat>().Count(t => t.IsHit), () => Is.EqualTo(expectedHits));
            AddAssert("future tail hit", () => drawableSlider.TailCircle.IsHit);
        }

        [TestCase(30, true)]
        [TestCase(30.1, false)]
        public void TestReacquiredTrackingCannotRecoverTail(double recoveryOffset, bool expectedHit)
        {
            createSlider(60.9);
            pressAt(0);
            AddStep("release key", () =>
            {
                gameplayClock.Seek(slider_start + 10);
                osuInputManager.KeyBindingContainer.TriggerReleased(OsuAction.LeftButton);
            });
            AddAssert("tracking stopped", () => !drawableSlider.SliderInputManager.Tracking);
            pressAt(recoveryOffset);
            AddAssert("tracking restarted", () => drawableSlider.SliderInputManager.Tracking);
            seekTo(61);
            AddAssert("tail respects new tracking start", () => drawableSlider.TailCircle.IsHit, () => Is.EqualTo(expectedHit));
        }

        [Test]
        public void TestTailTrackingGateAfterRewind()
        {
            createSlider(100.9);
            pressAt(0);
            seekTo(64);
            AddAssert("tail initially hit", () => drawableSlider.TailCircle.IsHit);
            seekTo(-1);
            AddAssert("head judgement reverted", () => !drawableSlider.HeadCircle.Judged);
            AddAssert("tail judgement reverted", () => !drawableSlider.TailCircle.Judged);
            AddAssert("tracking history reverted", () => !drawableSlider.SliderInputManager.Tracking && drawableSlider.Result.TrackingHistory.Count == 1);
            AddStep("release key before resuming", () => osuInputManager.KeyBindingContainer.TriggerReleased(OsuAction.LeftButton));
            pressAt(65);
            seekTo(101);
            AddAssert("late head hit on resumed playback", () => drawableSlider.HeadCircle.IsHit);
            AddAssert("past tail not recovered", () => !drawableSlider.TailCircle.IsHit);
        }

        private void createSlider(double duration, int repeatCount = 0, int tickRate = 1)
        {
            AddStep("create classic slider with manual clock", () =>
            {
                gameplayClock.Seek(slider_start - 100);
                var slider = new Slider
                {
                    StartTime = slider_start,
                    Position = new Vector2(256, 192),
                    ClassicSliderBehaviour = true,
                    ClassicSliderJudgement = true,
                    RepeatCount = repeatCount,
                    Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(20, 0) }, 20)
                };
                var controlPoints = new ControlPointInfo();
                controlPoints.Add(0, new TimingControlPoint { BeatLength = duration * 5 / (repeatCount + 1) });
                slider.ApplyDefaults(controlPoints, new BeatmapDifficulty { SliderMultiplier = 1, SliderTickRate = tickRate });
                var playfield = new TestPlayfield();
                playfield.Add(drawableSlider = new DrawableSlider(slider));
                Child = osuInputManager = new OsuInputManager(new OsuRuleset().RulesetInfo)
                {
                    Clock = gameplayClock,
                    ProcessCustomClock = false,
                    Child = playfield
                };
            });
            AddStep("move cursor into slider", () => InputManager.MoveMouseTo(drawableSlider.ToScreenSpace(drawableSlider.OriginPosition)));
        }

        private void pressAt(double offset)
        {
            AddStep($"press at {offset}ms", () =>
            {
                gameplayClock.Seek(slider_start + offset);
                osuInputManager.KeyBindingContainer.TriggerPressed(OsuAction.LeftButton);
            });
        }

        private void seekTo(double offset) => AddStep($"advance to {offset}ms", () => gameplayClock.Seek(slider_start + offset));

        private partial class TestPlayfield : Playfield
        {
        }

        private class TestGameplayClock : FramedClock, IGameplayClock
        {
            private readonly ManualClock source;

            public TestGameplayClock()
                : this(new ManualClock())
            {
            }

            private TestGameplayClock(ManualClock source)
                : base(source)
            {
                this.source = source;
            }

            public double StartTime => 0;
            public double GameplayStartTime => slider_start;
            public IAdjustableAudioComponent AdjustmentsFromMods { get; } = new AudioAdjustments();
            public IBindable<bool> IsPaused { get; } = new BindableBool();
            public bool IsRewinding { get; private set; }

            public void Seek(double time)
            {
                IsRewinding = time < CurrentTime;
                source.CurrentTime = time;
                ProcessFrame();
            }
        }
    }
}
