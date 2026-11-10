// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    public partial class TestSceneLegacySliderBallPosition : OsuSkinnableTestScene
    {
        private readonly ManualClock manualClock = new ManualClock();
        private bool showPreviousFmSample;

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();
            if (showPreviousFmSample)
            {
                // Recorded by the pre-fix sampler on beatmap 5839931 at 206771 ms.
                // Only used to capture its offscreen ball before showing the correction.
                foreach (var slider in this.ChildrenOfType<DrawableSlider>())
                    slider.Ball.Position = new Vector2(255, 100730050) - slider.HitObject.Position;
            }
        }

        [Test]
        public void TestFmSynthesisRealObject()
        {
            AddStep("create real Aspire object", () =>
            {
                showPreviousFmSample = false;
                manualClock.CurrentTime = 206640;
                var original = AspireRuntimeBeatmaps.Decode(LegacySliderBallPositionTest.ReadFmSynthesisObject(206640)).HitObjects.Single();
                var timing = new ControlPointInfo();
                timing.Add(0, new TimingControlPoint { LegacyBeatLength = 300 / 201459915.8592682 });
                SetContents(_ =>
                {
                    var slider = new Slider
                    {
                        StartTime = original.StartTime,
                        Position = ((IHasPosition)original).Position,
                        Path = ((IHasPath)original).Path,
                        GenerateTicks = false,
                    };
                    slider.ApplyDefaults(timing, new BeatmapDifficulty { CircleSize = 4.8f, SliderMultiplier = 3 });
                    return new Container
                    {
                        Size = new Vector2(512, 384),
                        Clock = new FramedClock(manualClock),
                        Child = new DrawableSlider(slider),
                    };
                });
            });
            AddUntilStep("real objects loaded", () => this.ChildrenOfType<DrawableSlider>().Count() == 5
                                                       && this.ChildrenOfType<DrawableSlider>().All(s => s.IsLoaded));
            AddStep("isolate ball for inspection", () =>
            {
                foreach (var slider in this.ChildrenOfType<DrawableSlider>())
                {
                    slider.Body.ClearTransforms();
                    slider.Body.Alpha = 0;
                }
                manualClock.CurrentTime = 206771;
            });
            AddWaitStep("settle real sample", 2);
            AddStep("show previous recorded position", () => showPreviousFmSample = true);
            AddWaitStep("capture previous position", 2);
            AddStep("show corrected real position", () => showPreviousFmSample = false);
            AddWaitStep("settle corrected position", 2);
            AddAssert("real field position matches stable", () => this.ChildrenOfType<DrawableSlider>().All(s =>
                (s.Ball.Position + s.HitObject.Position - new Vector2(216, 104)).Length < 0.001f));
            AddAssert("screen centre matches field position", () => this.ChildrenOfType<DrawableSlider>().All(s =>
                (s.Ball.ScreenSpaceDrawQuad.Centre - s.Parent!.ToScreenSpace(new Vector2(216, 104))).Length < 0.01f));
        }

        [Test]
        public void TestSubmillisecondReversals()
        {
            AddStep("create fast slider", () =>
            {
                manualClock.CurrentTime = 1000;
                var timing = new ControlPointInfo();
                timing.Add(0, new TimingControlPoint { LegacyBeatLength = 0.42 });
                SetContents(_ =>
                {
                    var slider = new Slider
                    {
                        StartTime = 1000,
                        Position = new Vector2(-50, 0),
                        Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0) }),
                        RepeatCount = 19,
                        GenerateTicks = false,
                    };
                    slider.ApplyDefaults(timing, new BeatmapDifficulty { CircleSize = 4 });
                    return new DrawableSlider(slider)
                    {
                        Anchor = Anchor.Centre,
                        Clock = new FramedClock(manualClock),
                    };
                });
            });
            AddUntilStep("sliders loaded", () => this.ChildrenOfType<DrawableSlider>().Count() == 5
                                                 && this.ChildrenOfType<DrawableSlider>().All(s => s.IsLoaded));
            AddStep("sample 0.3 ms", () => manualClock.CurrentTime = 1000.3);
            AddWaitStep("show first sample", 2);
            AddStep("sample 1 ms", () => manualClock.CurrentTime = 1001);
            AddWaitStep("show integer sample", 2);
            AddAssert("visuals use stable position", () => this.ChildrenOfType<DrawableSlider>().All(s => s.Ball.LegacyVisualPosition.Length < 0.001f));
            AddAssert("gameplay position stays continuous", () => this.ChildrenOfType<DrawableSlider>().All(s => s.Ball.GameplayPosition.X > 60 && s.Ball.GameplayPosition.X < 70));
            AddStep("seek forward", () => manualClock.CurrentTime = 1004.2);
            AddWaitStep("show forward sample", 2);
            AddStep("seek backward", () => manualClock.CurrentTime = 1000.9);
            AddWaitStep("show backward sample", 2);
            AddAssert("visuals recover after rewind", () => this.ChildrenOfType<DrawableSlider>().All(s => s.Ball.LegacyVisualPosition.Length < 0.001f));
        }
    }
}
