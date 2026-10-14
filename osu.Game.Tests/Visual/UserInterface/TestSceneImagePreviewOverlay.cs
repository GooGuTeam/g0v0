// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays.ImagePreview;
using osuTK;

namespace osu.Game.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneImagePreviewOverlay : OsuManualInputManagerTestScene
    {
        private const string image_url = @"https://example.com/test-image.png";

        private ImagePreviewOverlay overlay = null!;

        private Texture texture = null!;

        [BackgroundDependencyLoader]
        private void load(TextureStore textures)
        {
            texture = textures.Get(@"Menu/logo");
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create overlay", () => Child = overlay = new ImagePreviewOverlay());
            AddUntilStep("wait for overlay to load", () => overlay.IsLoaded);
        }

        [Test]
        public void TestShowAndHide()
        {
            AddStep("show image", () => overlay.ShowImage(createRequest()));

            AddAssert("overlay visible", () => overlay.State.Value == Visibility.Visible);
            AddUntilStep("image displayed", () => displayedImage() != null);

            AddStep("hide overlay", () => overlay.Hide());
            AddUntilStep("overlay hidden", () => overlay.State.Value == Visibility.Hidden);
            AddUntilStep("image faded out", () => Precision.AlmostEquals(overlay.ImageHolder.Alpha, 0));
        }

        [Test]
        public void TestToolbarButtons()
        {
            AddStep("show image", () => overlay.ShowImage(createRequest()));
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);

            AddUntilStep("five toolbar buttons displayed", () => overlay.ChildrenOfType<IconButton>().Count() == 5);
            AddAssert("zoom out button displayed", () => findButton(ImagePreviewOverlayStrings.ZoomOut) != null);
            AddAssert("zoom in button displayed", () => findButton(ImagePreviewOverlayStrings.ZoomIn) != null);
            AddAssert("copy link button displayed", () => findButton(CommonStrings.CopyLink) != null);
            AddAssert("save button displayed", () => findButton(ImagePreviewOverlayStrings.SaveImage) != null);
            AddAssert("close button displayed", () => findButton(ImagePreviewOverlayStrings.ClosePreview) != null);
        }

        [Test]
        public void TestZoomViaButtons()
        {
            AddStep("show image", () => overlay.ShowImage(createRequest()));
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);
            AddUntilStep("starting at 100%", () => Precision.AlmostEquals(overlay.CurrentZoom.Value, 1));

            AddStep("click zoom in", () => findButton(ImagePreviewOverlayStrings.ZoomIn)!.TriggerClick());
            AddUntilStep("zoom increased", () => overlay.CurrentZoom.Value > 1);

            AddStep("click zoom out", () => findButton(ImagePreviewOverlayStrings.ZoomOut)!.TriggerClick());
            AddUntilStep("zoom restored", () => Precision.AlmostEquals(overlay.CurrentZoom.Value, 1));
        }

        [Test]
        public void TestZoomViaScroll()
        {
            AddStep("show image", () => overlay.ShowImage(createRequest()));
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("move mouse to overlay", () => InputManager.MoveMouseTo(overlay));
            AddStep("scroll up", () => InputManager.ScrollVerticalBy(1));
            AddUntilStep("zoom increased", () => overlay.CurrentZoom.Value > 1);

            AddStep("scroll down", () => InputManager.ScrollVerticalBy(-1));
            AddUntilStep("zoom restored", () => Precision.AlmostEquals(overlay.CurrentZoom.Value, 1));
        }

        [Test]
        public void TestTransitionFromSource()
        {
            Sprite source = null!;

            AddStep("create source image", () => Add(source = new Sprite
            {
                Texture = texture,
                Size = new Vector2(100),
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
            }));

            AddStep("show preview", () =>
            {
                overlay.ShowImage(createRequest(source));

                Assert.That(overlay.State.Value, Is.EqualTo(Visibility.Visible));

                Vector2 expectedStart = overlay.ToLocalSpace(source.ScreenSpaceDrawQuad.Centre) - overlay.DrawSize / 2;

                Assert.That(Precision.AlmostEquals(overlay.ImageHolder.Position, expectedStart, 1), Is.True,
                    $@"expected image to start at the source position ({expectedStart}), but it was at {overlay.ImageHolder.Position}");
            });

            AddUntilStep("image moved to centre", () => Precision.AlmostEquals(overlay.ImageHolder.Position, Vector2.Zero, 0.5f));
            AddUntilStep("image full scale", () => Precision.AlmostEquals(overlay.ImageHolder.Scale, Vector2.One, 0.01f));
        }

        [Test]
        public void TestTransitionBackToSource()
        {
            Sprite source = null!;

            AddStep("create source image", () => Add(source = new Sprite
            {
                Texture = texture,
                Size = new Vector2(100),
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
            }));

            AddStep("show preview", () => overlay.ShowImage(createRequest(source)));
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);
            AddUntilStep("image centred", () => Precision.AlmostEquals(overlay.ImageHolder.Position, Vector2.Zero, 0.5f));

            Vector2 expectedEnd = default;

            AddStep("record source position", () => expectedEnd = overlay.ToLocalSpace(source.ScreenSpaceDrawQuad.Centre) - overlay.DrawSize / 2);

            AddStep("hide preview", () => overlay.Hide());
            AddAssert("overlay hidden", () => overlay.State.Value == Visibility.Hidden);

            // the image should travel back to the position it was clicked at, rather than being reset to the centre.
            AddUntilStep("image returned to source", () => Precision.AlmostEquals(overlay.ImageHolder.Position, expectedEnd, 1));
        }

        [Test]
        public void TestImageFitsWithinDisplayArea()
        {
            AddStep("show image", () => overlay.ShowImage(createRequest()));
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);

            AddUntilStep("image fits within overlay", () =>
                overlay.ImageHolder.DrawWidth <= overlay.DrawWidth && overlay.ImageHolder.DrawHeight <= overlay.DrawHeight);
        }

        private ImagePreviewRequest createRequest(Drawable? source = null) => new ImagePreviewRequest(texture, image_url, null, source);

        private Sprite? displayedImage() => overlay.ChildrenOfType<Sprite>().FirstOrDefault(s => s.Texture == texture);

        private IconButton? findButton(LocalisableString tooltip) =>
            overlay.ChildrenOfType<IconButton>().FirstOrDefault(b => b.TooltipText.ToString() == tooltip.ToString());
    }
}
