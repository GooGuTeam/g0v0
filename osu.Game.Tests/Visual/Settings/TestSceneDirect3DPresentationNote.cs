// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections.Graphics;
using osuTK;

namespace osu.Game.Tests.Visual.Settings
{
    public partial class TestSceneDirect3DPresentationNote : OsuTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        private TestPresentationNote note = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            Child = new FillFlowContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Width = 450,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Children = new Drawable[]
                {
                    note = new TestPresentationNote(),
                    new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 20,
                    },
                },
            };
        });

        [TestCase(350f, 1f)]
        [TestCase(450f, 1.5f)]
        public void TestStatusAndLayout(float width, float scale)
        {
            AddStep("set size and flip status", () =>
            {
                note.Parent!.Width = width;
                note.Parent.Scale = new Vector2(scale);
                note.Status = new Direct3DPresentationStatus(Direct3DPresentationModel.FlipDiscard, false);
            });
            AddUntilStep("flip model shown", () => currentText().Contains("FLIP_DISCARD"));
            AddAssert("non-exclusive status shown", () => currentText().Contains(GraphicsSettingsStrings.ExclusiveFullscreenInactive.ToString()));
            AddWaitStep("wait for layout", 10);
            AddAssert("text fits horizontally", () =>
            {
                var text = note.ChildrenOfType<OsuTextFlowContainer>().Single();
                return text.DrawWidth > 0 && text.ScreenSpaceDrawQuad.AABBFloat.Width <= note.ScreenSpaceDrawQuad.AABBFloat.Width;
            });
            AddAssert("note has sane height", () => note.DrawHeight is > 0 and < 350);

            AddStep("enter exclusive fullscreen", () => note.Status = new Direct3DPresentationStatus(Direct3DPresentationModel.FlipDiscard, true));
            AddUntilStep("exclusive status updated", () => currentText().Contains(GraphicsSettingsStrings.Direct3DExclusiveFullscreen(GraphicsSettingsStrings.ExclusiveFullscreenActive).ToString()));
            AddStep("switch to BitBlt", () => note.Status = new Direct3DPresentationStatus(Direct3DPresentationModel.BitBltSequential, false));
            AddUntilStep("BitBlt status shown", () => currentText().Contains("BitBlt"));
            AddStep("query fails", () => note.Status = new Direct3DPresentationStatus(null, null));
            AddUntilStep("unknown shown", () => currentText().Contains(GraphicsSettingsStrings.PresentationUnknown.ToString()));
            AddStep("switch to non-Direct3D renderer", () => note.Status = null);
            AddUntilStep("note removed", () => note.ChildrenOfType<SettingsNote>().Single().Current.Value == null);
            AddAssert("no extra layout spacing", () => !note.IsPresent);
            AddUntilStep("following item moves up", () => note.Parent!.DrawHeight, () => Is.EqualTo(20).Within(0.01));
            AddStep("restore Direct3D renderer", () => note.Status = new Direct3DPresentationStatus(Direct3DPresentationModel.FlipSequential, false));
            AddUntilStep("hidden note appears again", () => note.IsPresent && currentText().Contains("FLIP_SEQUENTIAL"));
            AddUntilStep("text visible again", () => note.ChildrenOfType<OsuTextFlowContainer>().Single().IsPresent);
        }

        [Test]
        public void TestSettingsSearch()
        {
            AddStep("show Direct3D status in search", () => Child = new SearchContainer
            {
                Width = 450,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Child = note = new TestPresentationNote
                {
                    Status = new Direct3DPresentationStatus(Direct3DPresentationModel.FlipDiscard, false),
                },
            });
            AddUntilStep("note visible", () => note.IsPresent);
            AddStep("search unrelated setting", () => ((SearchContainer)Child).SearchTerm = "audio");
            AddUntilStep("note filtered out", () => !note.IsPresent);
            AddStep("fullscreen changes while filtered", () => note.Status = new Direct3DPresentationStatus(Direct3DPresentationModel.FlipDiscard, true));
            AddUntilStep("hidden note still updates", () => currentText().Contains(GraphicsSettingsStrings.Direct3DExclusiveFullscreen(GraphicsSettingsStrings.ExclusiveFullscreenActive).ToString()));
            AddStep("search flip", () => ((SearchContainer)Child).SearchTerm = "flip");
            AddUntilStep("note found", () => note.IsPresent);
            AddStep("active backend is not Direct3D", () => note.Status = null);
            AddUntilStep("not a search result", () => !note.CanBeShown.Value && !note.IsPresent);
            AddStep("restore active Direct3D backend", () => note.Status = new Direct3DPresentationStatus(Direct3DPresentationModel.FlipDiscard, false));
            AddUntilStep("search result restored", () => note.CanBeShown.Value && note.IsPresent);
        }

        [Test]
        public void TestHiddenOnOtherRenderers()
        {
            AddWaitStep("wait for layout", 10);
            AddAssert("no diagnostic note", () => note.ChildrenOfType<SettingsNote>().Single().Current.Value == null);
            AddAssert("no status text", () => currentText() == string.Empty);
            AddAssert("no extra layout spacing", () => !note.IsPresent);
            AddAssert("following item has no gap", () => note.Parent!.DrawHeight, () => Is.EqualTo(20).Within(0.01));
        }

        private string currentText() => note.ChildrenOfType<SettingsNote>().Single().Current.Value?.Text.ToString() ?? string.Empty;

        private partial class TestPresentationNote : Direct3DPresentationNote
        {
            public Direct3DPresentationStatus? Status;

            protected override Direct3DPresentationStatus? GetPresentationStatus() => Status;
        }
    }
}
