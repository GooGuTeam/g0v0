// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions.PolygonExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Screens.Select;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.SongSelect
{
    [HeadlessTest]
    public partial class TestSceneBeatmapCarouselForwardedDrag : BeatmapCarouselTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private Panel[] panels => Carousel.ChildrenOfType<Panel>().Where(p => p.Item != null).ToArray();

        private void create(bool legacy)
        {
            AddStep("set presentation", () => config.SetValue(OsuSetting.ToriiLegacyFooterUseSkin, legacy));
            RemoveAllBeatmaps();
            CreateCarousel();
            AddBeatmaps(60, 1);
            WaitForFiltering();
            WaitForDrawablePanels();
            AddStep("scroll to bottom", () => Scroll.ScrollToEnd(false));
            WaitForScrolling();
            AddWaitStep("settle row positions", 30);
        }

        [Test]
        public void TestUserDeltaDoesNotMoveLayout([Values(false, true)] bool legacy)
        {
            create(legacy);
            AddStep("forward drag without layout compensation", () =>
            {
                var before = panels.ToDictionary(p => p, p => p.DrawYPosition);
                double current = Scroll.Current;
                Carousel.ScrollFromDelta(25);
                Assert.That(Scroll.Current, Is.EqualTo(current - 25).Within(0.001));
                foreach (var (panel, y) in before)
                    Assert.That(panel.DrawYPosition, Is.EqualTo(y), "User drag must not move model-space panel positions");
                Assert.That(((UserTrackingScrollContainer)Scroll).UserScrolling, Is.True);
            });
        }

        [Test]
        public void TestOverscrollDoesNotExpireVisibleArtwork([Values(false, true)] bool legacy)
        {
            create(legacy);
            Panel[] before = Array.Empty<Panel>();
            ManualClock clock = null!;
            AddStep("control drag frame time", () => Carousel.Clock = new FramedClock(clock = new ManualClock { CurrentTime = Carousel.Time.Current }));
            AddStep("attach portable row artwork and drag beyond bottom", () =>
            {
                before = panels.Where(p => Scroll.ScreenSpaceDrawQuad.Intersects(p.ScreenSpaceDrawQuad)).ToArray();
                Assert.That(before, Is.Not.Empty);
                foreach (var panel in before)
                    panel.TopLevelContent.Add(new Box { Name = "synthetic skin artwork", RelativeSizeAxes = Axes.Both });
                for (int i = 0; i < 12; i++)
                    Carousel.ScrollFromDelta(-40);
            });
            AddStep("process first drag frame", () => clock.CurrentTime += 1);
            AddWaitStep("process virtualisation", 2);
            AddAssert("visible artwork not retired by mismatched layout range", () => before.All(p =>
                !Scroll.ScreenSpaceDrawQuad.Intersects(p.ScreenSpaceDrawQuad) || p.Item != null));
            AddStep("resume realtime", () => Carousel.Clock = Clock);
            AddUntilStep("bottom rebound", () => Math.Abs(Scroll.Current - Scroll.ScrollableExtent) < 1);
            AddAssert("bottom rows remain", () => panels.Any(p => Scroll.ScreenSpaceDrawQuad.Intersects(p.ScreenSpaceDrawQuad)));
            AddStep("scroll to top", () => Scroll.ScrollToStart(false));
            WaitForScrolling();
            AddWaitStep("allow pooled rows to settle", 30);
            AddStep("drag beyond top", () => Carousel.ScrollFromDelta(400));
            AddUntilStep("top rebound", () => Math.Abs(Scroll.Current) < 1);
            AddAssert("top rows are present", () => panels.Any(p => Scroll.ScreenSpaceDrawQuad.Intersects(p.ScreenSpaceDrawQuad)));
            AddStep("return to bottom", () => Scroll.ScrollToEnd(false));
            WaitForScrolling();
            AddUntilStep("pooled bottom rows restored", () => panels.Any(p => Scroll.ScreenSpaceDrawQuad.Intersects(p.ScreenSpaceDrawQuad)));
        }

        [Test]
        public void TestLeftAreaPointerDrag()
        {
            create(true);
            LeftSideInteractionContainer dragArea = null!;
            double start = 0;
            AddStep("add actual forwarding input area", () =>
            {
                Add(dragArea = new LeftSideInteractionContainer(() => { }, Carousel.ScrollFromDelta, () => true)
                {
                    Size = new Vector2(150, 150),
                    Depth = float.MinValue,
                });
                start = Scroll.Current;
                InputManager.MoveMouseTo(dragArea.ScreenSpaceDrawQuad.Centre);
                InputManager.PressButton(MouseButton.Left);
            });
            AddStep("drag down", () => InputManager.MoveMouseTo(dragArea.ScreenSpaceDrawQuad.Centre + new Vector2(0, 50)));
            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));
            AddAssert("pointer forwarded user scroll", () => Scroll.Current < start && ((UserTrackingScrollContainer)Scroll).UserScrolling);
            AddUntilStep("rows converge to model coordinates", () => panels.All(p => Math.Abs(p.DrawYPosition - p.Item!.CarouselYPosition) < 1));
        }

        [Test]
        public void TestForwardedDragSurvivesRefilter([Values(false, true)] bool legacy)
        {
            create(legacy);
            double position = 0;
            AddStep("select first beatmap", () => Carousel.CurrentGroupedBeatmap = new GroupedBeatmap(null, BeatmapSets.First().Beatmaps.First()));
            WaitForScrolling();
            AddStep("browse away from selection", () =>
            {
                Scroll.ScrollToEnd(false);
                Carousel.ScrollFromDelta(25);
                position = Scroll.Current;
            });
            ApplyToFilterAndWaitForFilter("refilter unchanged list", _ => { });
            AddUntilStep("user position retained rather than selection autoscroll", () => Math.Abs(Scroll.Current - position) < 1);
        }

        [Test]
        public void TestLayoutCompensationStillMovesRows()
        {
            create(false);
            AddStep("compensate layout movement", () =>
            {
                var before = panels.ToDictionary(p => p, p => p.DrawYPosition);
                double current = Scroll.Current;
                Scroll.OffsetScrollPosition(25);
                Assert.That(Scroll.Current, Is.EqualTo(current + 25));
                foreach (var (panel, y) in before)
                    Assert.That(panel.DrawYPosition, Is.EqualTo(y + 25));
            });
        }
    }
}
