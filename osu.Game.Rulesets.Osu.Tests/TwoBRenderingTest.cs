// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osu.Game.Rulesets.Osu.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class TwoBRenderingTest
    {
        [Test]
        public void TestEarlierCirclesDrawInFront()
        {
            var first = new HitCircle { StartTime = 1000 };
            var second = new HitCircle { StartTime = 1200 };
            var third = new HitCircle { StartTime = 1400 };

            Assert.Multiple(() =>
            {
                Assert.That(depth(first, OsuVisualKind.CircleBase), Is.GreaterThan(depth(second, OsuVisualKind.CircleBase)));
                Assert.That(depth(second, OsuVisualKind.CircleBase), Is.GreaterThan(depth(third, OsuVisualKind.CircleBase)));
            });
        }

        [Test]
        public void TestSliderBodiesAndCirclesUseStableEventTimes()
        {
            var outer = createSlider(4000, 1000, 1);
            var inner = createSlider(4375, 500);
            var circle = new HitCircle { StartTime = 4375 };

            Assert.Multiple(() =>
            {
                Assert.That(depth(inner, OsuVisualKind.Body, inner), Is.GreaterThan(depth(outer, OsuVisualKind.Body, outer)));
                Assert.That(depth(circle, OsuVisualKind.CircleBase), Is.GreaterThan(depth(outer, OsuVisualKind.Body, outer)));
                Assert.That(depth(outer, OsuVisualKind.Body, outer), Is.EqualTo(0.8f - 6010f / 10000000f).Within(0.0000001f));
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestCircleNumberAndOverlayOffsets(bool overlayAboveNumber)
        {
            var circle = new HitCircle { StartTime = 1000 };
            float baseDepth = depth(circle, OsuVisualKind.CircleBase);
            float numberDepth = OsuVisualOrdering.GetStableDepth(circle, circle, OsuVisualKind.Number, overlayAboveNumber);
            float overlayDepth = OsuVisualOrdering.GetStableDepth(circle, circle, OsuVisualKind.Overlay, overlayAboveNumber);

            Assert.Multiple(() =>
            {
                Assert.That(numberDepth, Is.GreaterThan(baseDepth));
                Assert.That(overlayDepth, Is.GreaterThan(baseDepth));

                if (overlayAboveNumber)
                    Assert.That(overlayDepth, Is.GreaterThan(numberDepth));
                else
                    Assert.That(numberDepth, Is.GreaterThan(overlayDepth));
            });
        }

        [Test]
        public void TestRepeatArrowUsesSpanStartRatherThanRepeatEnd()
        {
            var slider = createSlider(1000, 500, 2);
            var firstRepeat = new SliderRepeat(slider) { RepeatIndex = 0, StartTime = 1500 };
            var secondRepeat = new SliderRepeat(slider) { RepeatIndex = 1, StartTime = 2000 };

            Assert.Multiple(() =>
            {
                Assert.That(depth(firstRepeat, OsuVisualKind.Arrow, slider), Is.EqualTo(0.8f - 999f / 10000000f).Within(0.0000001f));
                Assert.That(depth(secondRepeat, OsuVisualKind.Arrow, slider), Is.EqualTo(0.8f - 1499f / 10000000f).Within(0.0000001f));
                Assert.That(depth(secondRepeat, OsuVisualKind.Arrow, slider), Is.Not.EqualTo(0.8f - 2000f / 10000000f).Within(0.0000001f));
            });
        }

        [Test]
        public void TestTicksShareTheSliderStartDepth()
        {
            var slider = createSlider(1000, 500, 1);
            var firstTick = new SliderTick { StartTime = 1200 };
            var secondTick = new SliderTick { StartTime = 1700 };

            Assert.Multiple(() =>
            {
                Assert.That(depth(firstTick, OsuVisualKind.Tick, slider), Is.EqualTo(0.8f - 995f / 10000000f).Within(0.0000001f));
                Assert.That(depth(secondTick, OsuVisualKind.Tick, slider), Is.EqualTo(depth(firstTick, OsuVisualKind.Tick, slider)));
            });
        }

        [Test]
        public void TestApproachCirclesAndBallsShareStableDepthSpace()
        {
            var earlyApproach = new HitCircle { StartTime = 1000, TimePreempt = 600 };
            var lateApproach = new HitCircle { StartTime = 5800000, TimePreempt = 0 };

            float earlyDepth = depth(earlyApproach, OsuVisualKind.Approach);
            float lateDepth = depth(lateApproach, OsuVisualKind.Approach);
            float ballDepth = depth(earlyApproach, OsuVisualKind.Ball);

            Assert.Multiple(() =>
            {
                Assert.That(ballDepth, Is.EqualTo(0.99f));
                Assert.That(earlyDepth, Is.LessThan(ballDepth));
                Assert.That(lateDepth, Is.GreaterThan(ballDepth));
                Assert.That(key(lateDepth).CompareTo(key(ballDepth, 1)), Is.GreaterThan(0));
            });
        }

        [Test]
        public void TestEqualDepthUsesDeterministicDeclarationTieBreak()
        {
            Assert.That(key(0.8f, declaration: 1).CompareTo(key(0.8f)), Is.GreaterThan(0));
        }

        [Test]
        public void TestViewportLimitsFramebufferWithoutScalingGeometry()
        {
            Vector2 limited = PlaySliderBody.GetViewportLimitedSize(new Vector2(4000, 3000), new Vector2(1920, 1080), new Vector2(2));
            Vector2 unchanged = PlaySliderBody.GetViewportLimitedSize(new Vector2(400, 300), new Vector2(1920, 1080), new Vector2(2));

            Assert.Multiple(() =>
            {
                Assert.That(limited.X, Is.EqualTo(960).Within(0.0001));
                Assert.That(limited.Y, Is.EqualTo(540).Within(0.0001));
                Assert.That(unchanged, Is.EqualTo(new Vector2(400, 300)));
            });
        }

        [Test]
        public void TestZeroSpanProgressIsFinite()
        {
            Assert.Multiple(() =>
            {
                Assert.That(DrawableSlider.GetCompletionProgress(1000, 1000, 0), Is.Zero);
                Assert.That(double.IsFinite(DrawableSlider.GetCompletionProgress(1000, 1000, 0)), Is.True);
                Assert.That(DrawableSlider.GetCompletionProgress(1500, 1000, 1000), Is.EqualTo(0.5).Within(0.0001));
            });
        }

        private static OsuVisualOrdering.VisualKey key(float depth, int group = 0, int declaration = 0) =>
            new OsuVisualOrdering.VisualKey(depth, group, 0, declaration, 0, 0, 0);

        private static float depth(HitObject model, OsuVisualKind kind, HitObject? parent = null) =>
            OsuVisualOrdering.GetStableDepth(model, parent ?? model, kind);

        private static Slider createSlider(double startTime, double spanDuration, int repeatCount = 0)
        {
            var slider = new Slider
            {
                StartTime = startTime,
                RepeatCount = repeatCount,
                Path = new SliderPath(PathType.LINEAR, new[]
                {
                    Vector2.Zero,
                    new Vector2(100, 0),
                }, 100),
            };

            // TimingControlPoint.DEFAULT has a 1000 ms beat, so this multiplier produces the requested span duration for a 100-unit path.
            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty { SliderMultiplier = 1000 / spanDuration });
            Assert.That(slider.SpanDuration, Is.EqualTo(spanDuration).Within(0.0001));
            return slider;
        }
    }
}
