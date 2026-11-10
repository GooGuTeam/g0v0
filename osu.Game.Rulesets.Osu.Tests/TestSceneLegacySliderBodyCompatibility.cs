// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
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
using osu.Game.Rulesets.Osu.Skinning.Legacy;
using osu.Game.Rulesets.Osu.Skinning.Argon;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    public partial class TestSceneLegacySliderBodyCompatibility : OsuSkinnableTestScene
    {
        private readonly List<DrawableSlider> sliders = new List<DrawableSlider>();
        private readonly List<Drawable> oldRenderers = new List<Drawable>();
        private readonly ManualClock clock = new ManualClock { CurrentTime = 1001 };

        [Test]
        public void TestBodyRendererRecreatedAfterRecycle()
        {
            AddStep("create oversized legacy body", () =>
            {
                sliders.Clear();
                SetContents(_ =>
                {
                    var slider = new Slider
                    {
                        StartTime = 1000,
                        Position = new Vector2(224, 192),
                        Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(8192, 0), new Vector2(8192, 100000000), new Vector2(128, 0) }),
                    };
                    slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                    var drawable = new DrawableSlider(slider);
                    sliders.Add(drawable);
                    return new Container
                    {
                        Size = new Vector2(512, 384),
                        Clock = new FramedClock(clock),
                        Child = drawable,
                    };
                });
            });
            AddUntilStep("classic and argon oversized renderers are present", () =>
                hasRenderer<LegacySliderBody>() && hasRenderer<ArgonSliderBody>());
            AddAssert("all loaded skin bodies use the oversized renderer", () =>
                this.ChildrenOfType<PlaySliderBody>().All(body => body.ChildrenOfType<Drawable>().Any(isActiveRenderer)));
            AddStep("recycle all body paths", () =>
            {
                oldRenderers.Clear();
                oldRenderers.AddRange(renderers());
                foreach (var drawable in sliders)
                    drawable.SliderBody?.RecyclePath();
            });
            AddUntilStep("fresh classic and argon renderers replace disposed ones", () =>
                hasRenderer<LegacySliderBody>() && hasRenderer<ArgonSliderBody>() && renderers().All(r => !oldRenderers.Contains(r)));
            AddWaitStep("draw recycled body and retire resources", 10);
        }

        private bool hasRenderer<T>() where T : PlaySliderBody
            => this.ChildrenOfType<T>().Any(body => body.ChildrenOfType<Drawable>().Any(isActiveRenderer));

        private static bool isActiveRenderer(Drawable drawable)
            => drawable.GetType().Name == "LegacySliderBodyRenderer" && drawable.IsLoaded && drawable.IsPresent;

        private IEnumerable<Drawable> renderers()
            => this.ChildrenOfType<PlaySliderBody>().SelectMany(body => body.ChildrenOfType<Drawable>()).Where(drawable => drawable.GetType().Name == "LegacySliderBodyRenderer");
    }
}
