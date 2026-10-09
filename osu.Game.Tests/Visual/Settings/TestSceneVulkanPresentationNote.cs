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
    public partial class TestSceneVulkanPresentationNote : OsuTestScene
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
            AddStep("set size and mailbox status", () =>
            {
                note.Parent!.Width = width;
                note.Parent.Scale = new Vector2(scale);
                note.Status = new VulkanPresentationStatus(VulkanPresentMode.Mailbox, true, false, 3);
            });
            AddUntilStep("mailbox mode shown", () => currentText().Contains("Mailbox"));
            AddAssert("non-exclusive status shown", () => currentText().Contains(GraphicsSettingsStrings.ExclusiveFullscreenInactive.ToString()));
            AddWaitStep("wait for layout", 10);
            AddAssert("text fits horizontally", () =>
            {
                var text = note.ChildrenOfType<OsuTextFlowContainer>().Single();
                return text.DrawWidth > 0 && text.ScreenSpaceDrawQuad.AABBFloat.Width <= note.ScreenSpaceDrawQuad.AABBFloat.Width;
            });
            AddAssert("note has sane height", () => note.DrawHeight is > 0 and < 350);

            AddStep("enter exclusive fullscreen", () => note.Status = new VulkanPresentationStatus(VulkanPresentMode.Immediate, true, true, 2));
            AddUntilStep("exclusive status updated", () => currentText().Contains(GraphicsSettingsStrings.VulkanExclusiveFullscreen(GraphicsSettingsStrings.ExclusiveFullscreenActive).ToString()));
            AddStep("switch to FIFO", () => note.Status = new VulkanPresentationStatus(VulkanPresentMode.Fifo, false, false, 2));
            AddUntilStep("FIFO status shown", () => currentText().Contains("FIFO"));
            AddStep("query fails", () => note.Status = new VulkanPresentationStatus(null, null, null, null));
            AddUntilStep("unknown shown", () => currentText().Contains(GraphicsSettingsStrings.PresentationUnknown.ToString()));
            AddStep("switch to non-Vulkan renderer", () => note.Status = null);
            AddUntilStep("note removed", () => note.ChildrenOfType<SettingsNote>().Single().Current.Value == null);
            AddAssert("no extra layout spacing", () => !note.IsPresent);
            AddUntilStep("following item moves up", () => note.Parent!.DrawHeight, () => Is.EqualTo(20).Within(0.01));
            AddStep("restore Vulkan renderer", () => note.Status = new VulkanPresentationStatus(VulkanPresentMode.Immediate, true, false, 2));
            AddUntilStep("hidden note appears again", () => note.IsPresent && currentText().Contains("Immediate"));
            AddUntilStep("text visible again", () => note.ChildrenOfType<OsuTextFlowContainer>().Single().IsPresent);
        }

        [Test]
        public void TestSettingsSearch()
        {
            AddStep("show Vulkan status in search", () => Child = new SearchContainer
            {
                Width = 450,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Child = note = new TestPresentationNote
                {
                    Status = new VulkanPresentationStatus(VulkanPresentMode.Mailbox, true, false, 3),
                },
            });
            AddUntilStep("note visible", () => note.IsPresent);
            AddStep("search unrelated setting", () => ((SearchContainer)Child).SearchTerm = "audio");
            AddUntilStep("note filtered out", () => !note.IsPresent);
            AddStep("fullscreen changes while filtered", () => note.Status = new VulkanPresentationStatus(VulkanPresentMode.Mailbox, true, true, 3));
            AddUntilStep("hidden note still updates", () => currentText().Contains(GraphicsSettingsStrings.VulkanExclusiveFullscreen(GraphicsSettingsStrings.ExclusiveFullscreenActive).ToString()));
            AddStep("search mailbox", () => ((SearchContainer)Child).SearchTerm = "mailbox");
            AddUntilStep("note found", () => note.IsPresent);
            AddStep("active backend is not Vulkan", () => note.Status = null);
            AddUntilStep("not a search result", () => !note.CanBeShown.Value && !note.IsPresent);
            AddStep("restore active Vulkan backend", () => note.Status = new VulkanPresentationStatus(VulkanPresentMode.Mailbox, true, false, 3));
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

        private partial class TestPresentationNote : VulkanPresentationNote
        {
            public VulkanPresentationStatus? Status;

            protected override VulkanPresentationStatus? GetPresentationStatus() => Status;
        }
    }
}
