// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays.ImagePreview;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneImagePreviewOverlay : OsuManualInputManagerTestScene
    {
        private const string image_url = @"https://example.com/test-image.png";

        private ImagePreviewOverlay overlay = null!;

        /// <summary>
        /// A layer standing in for the content the preview is opened over (the wiki overlay, in the game).
        /// </summary>
        /// <remarks>
        /// The preview is added above this layer, mirroring how it is placed in the top-most overlay layer of the game,
        /// so that it covers the content an image was clicked in rather than being covered by it.
        /// </remarks>
        private Container contentLayer = null!;

        /// <summary>
        /// A layer standing in for the scaling container the whole game interface lives in, so that the effect of the
        /// interface scale on the preview can be exercised.
        /// </summary>
        /// <remarks>
        /// The preview is a child of this rather than of the test scene, mirroring how it is nested inside the game's
        /// interface scaling. The layer is left unscaled by default.
        /// </remarks>
        private Container interfaceLayer = null!;

        private Texture texture = null!;

        /// <summary>
        /// The position the previewed image was last observed at by <see cref="recordTransitionStartPosition"/>.
        /// </summary>
        private Vector2? transitionStartPosition;

        [BackgroundDependencyLoader]
        private void load(TextureStore textures)
        {
            texture = textures.Get(@"Menu/logo");
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("clean up container", Clear);
            AddStep("create content layer", () => Add(contentLayer = new Container { RelativeSizeAxes = Axes.Both }));
            AddStep("create interface layer", () => Add(interfaceLayer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            }));
            AddStep("create overlay", () => interfaceLayer.Add(overlay = new ImagePreviewOverlay()));
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
            float startingZoom = 0;

            AddStep("show image", () => overlay.ShowImage(createRequest()));
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);
            AddStep("record starting zoom", () => startingZoom = overlay.CurrentZoom.Value);

            AddStep("click zoom in", () => findButton(ImagePreviewOverlayStrings.ZoomIn)!.TriggerClick());
            AddUntilStep("zoom increased", () => overlay.CurrentZoom.Value > startingZoom);

            AddStep("click zoom out", () => findButton(ImagePreviewOverlayStrings.ZoomOut)!.TriggerClick());
            AddUntilStep("zoom restored", () => Precision.AlmostEquals(overlay.CurrentZoom.Value, startingZoom, 0.01f));
        }

        [Test]
        public void TestZoomViaScroll()
        {
            float startingZoom = 0;

            AddStep("show image", () => overlay.ShowImage(createRequest()));
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);
            AddStep("record starting zoom", () => startingZoom = overlay.CurrentZoom.Value);

            AddStep("move mouse to overlay", () => InputManager.MoveMouseTo(overlay));
            AddStep("scroll up", () => InputManager.ScrollVerticalBy(1));
            AddUntilStep("zoom increased", () => overlay.CurrentZoom.Value > startingZoom);

            AddStep("scroll down", () => InputManager.ScrollVerticalBy(-1));
            AddUntilStep("zoom restored", () => Precision.AlmostEquals(overlay.CurrentZoom.Value, startingZoom, 0.01f));
        }

        [Test]
        public void TestSwappingImageWhileVisibleResetsTheView()
        {
            Sprite source = null!;

            float restingZoom = 0;

            AddStep("create source image", () => contentLayer.Add(source = new Sprite
            {
                Texture = texture,
                Size = new Vector2(100),
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
            }));

            AddStep("show preview", () => overlay.ShowImage(createRequest(source)));
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("record resting zoom", () => restingZoom = overlay.CurrentZoom.Value);

            AddStep("zoom in", () => findButton(ImagePreviewOverlayStrings.ZoomIn)!.TriggerClick());
            AddUntilStep("zoomed in", () => overlay.CurrentZoom.Value > restingZoom);

            AddStep("show another image", () =>
            {
                overlay.ShowImage(createRequest(source));

                // the content is swapped out rather than transitioned in, so the view has to be reset to its resting
                // state right away - both by the call itself and by cancelling whatever was still animating.
                Assert.That(Precision.AlmostEquals(overlay.CurrentZoom.Value, restingZoom), Is.True,
                    $@"expected the zoom to be reset, but it was at {overlay.CurrentZoom.Value}");
                Assert.That(Precision.AlmostEquals(overlay.ImageHolder.Scale, new Vector2(restingZoom)), Is.True,
                    $@"expected the image to be back at its resting scale, but it was at {overlay.ImageHolder.Scale}");
                Assert.That(Precision.AlmostEquals(overlay.ImageHolder.Position, Vector2.Zero), Is.True,
                    $@"expected the image to be recentred, but it was at {overlay.ImageHolder.Position}");
                Assert.That(Precision.AlmostEquals(overlay.ImageHolder.Alpha, 1), Is.True,
                    $@"expected the image to be fully visible, but it was at alpha {overlay.ImageHolder.Alpha}");
            });

            AddUntilStep("image stays at rest", () => Precision.AlmostEquals(overlay.ImageHolder.Scale, new Vector2(overlay.CurrentZoom.Value))
                                                      && Precision.AlmostEquals(overlay.ImageHolder.Position, Vector2.Zero)
                                                      && Precision.AlmostEquals(overlay.ImageHolder.Alpha, 1));
        }

        [Test]
        public void TestTransitionFromSource()
        {
            Sprite source = null!;

            AddStep("create source image", () => contentLayer.Add(source = new Sprite
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
            AddUntilStep("image at resting scale", () => Precision.AlmostEquals(overlay.ImageHolder.Scale, new Vector2(overlay.CurrentZoom.Value), 0.01f));
        }

        [Test]
        public void TestTransitionBackToSource()
        {
            Sprite source = null!;

            AddStep("create source image", () => contentLayer.Add(source = new Sprite
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
        public void TestImageFadesInAndOut()
        {
            Sprite source = null!;

            AddStep("create source image", () => contentLayer.Add(source = new Sprite
            {
                Texture = texture,
                Size = new Vector2(100),
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
            }));

            AddStep("show preview", () =>
            {
                overlay.ShowImage(createRequest(source));

                // the preview must start out fully transparent, so that the moment at which it does not
                // line up with the source it was opened from is never visible.
                Assert.That(overlay.ImageHolder.Alpha, Is.EqualTo(0),
                    "expected the image to start out invisible when opened from a source");
            });

            AddUntilStep("image faded in", () => Precision.AlmostEquals(overlay.ImageHolder.Alpha, 1));

            AddStep("hide preview", () => overlay.Hide());

            // this is evaluated 200ms into the 250ms transition, by which point the image should already be
            // mostly gone - it must not still be opaque when it lines up with the source again.
            AddAssert("image faded out while returning", () => overlay.ImageHolder.Alpha < 0.5f);

            AddUntilStep("image faded out", () => Precision.AlmostEquals(overlay.ImageHolder.Alpha, 0));
        }

        [Test]
        public void TestImageKeepsPaddingAroundIt()
        {
            AddStep("show image", () => overlay.ShowImage(createRequest()));
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);
            AddUntilStep("transition finished", () => Precision.AlmostEquals(overlay.ImageHolder.Scale, new Vector2(overlay.CurrentZoom.Value), 0.01f));

            AddAssert("image inset from the edges", () => minInset() >= ImagePreviewOverlay.DISPLAY_PADDING - 1);

            // one axis always uses up all of the space it has been given, otherwise the image would be drawn
            // smaller than it needs to be.
            AddAssert("image uses the available area", () => Precision.AlmostEquals(minInset(), ImagePreviewOverlay.DISPLAY_PADDING, 1));
        }

        /// <summary>
        /// Covers 100% being the size the image really is, so that the preview can be used to inspect image detail.
        /// </summary>
        [Test]
        public void TestHundredPercentMatchesTheNativeImageSize()
        {
            AddStep("show image", () => overlay.ShowImage(createRequest()));
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("set zoom to 100%", () => setZoom(1));
            AddUntilStep("zoom applied", () => Precision.AlmostEquals(overlay.CurrentZoom.Value, 1));
            AddUntilStep("scale settled", () => Precision.AlmostEquals(overlay.ImageHolder.Scale, Vector2.One, 0.01f));

            AddAssert("image drawn at its original width", () => Precision.AlmostEquals(overlay.ImageHolder.ScreenSpaceDrawQuad.Width, texture.DisplayWidth, 1));
            AddAssert("image drawn at its original height", () => Precision.AlmostEquals(overlay.ImageHolder.ScreenSpaceDrawQuad.Height, texture.DisplayHeight, 1));
        }

        /// <summary>
        /// Covers the image being drawn at its real size regardless of how much the game's interface is scaled, which is
        /// what the interface scale setting and the resolution-dependent scaling of the interface would otherwise alter.
        /// </summary>
        [Test]
        public void TestInterfaceScaleDoesNotAffectTheImage()
        {
            const float scaled_interface = 1.5f;

            float fittedWidth = 0;

            // the interface is already scaled when the image is opened, which is the state the game is normally in
            // when the interface scale setting is not at 100%.
            AddStep("scale up the interface", () =>
            {
                interfaceLayer.Scale = new Vector2(scaled_interface);
                interfaceLayer.Size = new Vector2(1 / scaled_interface);
            });

            AddUntilStep("interface actually scaled up", () => Precision.AlmostEquals(interfaceScaleOfLayer(), scaled_interface));

            AddStep("show image", () => overlay.ShowImage(createRequest()));
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);
            AddUntilStep("transition finished", () => Precision.AlmostEquals(overlay.ImageHolder.Scale, new Vector2(overlay.CurrentZoom.Value), 0.01f));

            AddStep("record fitted width", () => fittedWidth = overlay.ImageHolder.ScreenSpaceDrawQuad.Width);
            AddAssert("image fitted with the padding around it", () => Precision.AlmostEquals(minInset(), ImagePreviewOverlay.DISPLAY_PADDING, 1));

            AddStep("scale the interface back down", () =>
            {
                interfaceLayer.Scale = Vector2.One;
                interfaceLayer.Size = Vector2.One;
            });

            AddUntilStep("interface actually scaled down", () => Precision.AlmostEquals(interfaceScaleOfLayer(), 1));
            AddUntilStep("image kept the size it was shown at", () => Precision.AlmostEquals(overlay.ImageHolder.ScreenSpaceDrawQuad.Width, fittedWidth, 1));
            AddAssert("padding around the image unchanged", () => Precision.AlmostEquals(minInset(), ImagePreviewOverlay.DISPLAY_PADDING, 1));

            AddStep("set zoom to 100%", () => setZoom(1));
            AddUntilStep("image drawn at its original size", () => Precision.AlmostEquals(overlay.ImageHolder.ScreenSpaceDrawQuad.Width, texture.DisplayWidth, 1));
            AddAssert("image still centred", () => Precision.AlmostEquals(overlay.ImageHolder.ScreenSpaceDrawQuad.Centre, overlay.ScreenSpaceDrawQuad.Centre, 1));
        }

        /// <summary>
        /// Covers the use case a developer would implement in the game: hooking an image up such that clicking it opens
        /// the preview overlay.
        /// </summary>
        /// <remarks>
        /// The tests above all call <see cref="ImagePreviewOverlay.ShowImage"/> directly, which skips over the wiring
        /// this test is about - the overlay being resolvable through dependency injection, a click on an image being
        /// handled, and the preview growing out of the image that was clicked.
        /// </remarks>
        [Test]
        public void TestClickingAnImageOpensThePreview()
        {
            PreviewableSprite source = null!;

            // mirrors the wiring done in OsuGame, where the overlay is cached so that any drawable can resolve it.
            AddStep("cache preview overlay", () => Dependencies.CacheAs<IImagePreviewOverlay>(overlay));

            AddStep("add clickable image", () => contentLayer.Add(source = new PreviewableSprite(image_url)
            {
                Texture = texture,
                Size = new Vector2(120),
                Position = new Vector2(200),
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
            }));

            AddUntilStep("image displayed", () => source.IsLoaded && source.ScreenSpaceDrawQuad.Width > 0);

            AddStep("move mouse to image", () => InputManager.MoveMouseTo(source));

            AddStep("click image", () =>
            {
                recordTransitionStartPosition();
                InputManager.Click(MouseButton.Left);
            });

            AddAssert("preview shown", () => overlay.State.Value == Visibility.Visible);
            AddAssert("preview shows the clicked image", () => displayedImage() != null);

            // in the game the preview lives in the top-most overlay layer, so it has to end up drawn over the content
            // it was opened from - otherwise the clicked image stays on top of it and is never seen dimmed behind it.
            AddAssert("preview drawn over the content it was opened from", () => drawnAbove(interfaceLayer, contentLayer));

            AddAssert("preview covers the clicked image", () =>
            {
                var background = previewBackground().ScreenSpaceDrawQuad;
                var sourceQuad = source.ScreenSpaceDrawQuad;

                return background.Contains(sourceQuad.TopLeft) && background.Contains(sourceQuad.BottomRight);
            });

            AddAssert("preview opened from the clicked image", () =>
            {
                Vector2 expected = overlay.ToLocalSpace(source.ScreenSpaceDrawQuad.Centre) - overlay.DrawSize / 2;

                return transitionStartPosition != null && Precision.AlmostEquals(transitionStartPosition.Value, expected, 1);
            });

            AddUntilStep("image settled at the centre", () => Precision.AlmostEquals(overlay.ImageHolder.Position, Vector2.Zero, 0.5f));
        }

        /// <summary>
        /// Records where the previewed image is on the first frame the overlay updates on after this is called.
        /// </summary>
        /// <remarks>
        /// A click is only dispatched on the next input poll, which happens after the step issuing it has run, so where
        /// the transition starts from cannot be observed from within that step itself.
        /// </remarks>
        private void recordTransitionStartPosition()
        {
            void capture(Drawable _)
            {
                overlay.OnUpdate -= capture;
                transitionStartPosition = overlay.ImageHolder.Position;
            }

            transitionStartPosition = null;
            overlay.OnUpdate += capture;
        }

        /// <summary>
        /// The scale the interface layer currently applies to its content.
        /// </summary>
        private float interfaceScaleOfLayer() => interfaceLayer.ScreenSpaceDrawQuad.Width / interfaceLayer.DrawWidth;

        /// <summary>
        /// The smallest of the two margins between the previewed image and the edges of the display area, in screen pixels.
        /// </summary>
        private float minInset()
        {
            Quad displayArea = overlay.ScreenSpaceDrawQuad;
            Quad image = overlay.ImageHolder.ScreenSpaceDrawQuad;

            return Math.Min((displayArea.Width - image.Width) / 2, (displayArea.Height - image.Height) / 2);
        }

        /// <summary>
        /// The full-screen background drawn behind the previewed image.
        /// </summary>
        private Box previewBackground() => overlay.ChildrenOfType<Box>().First();

        /// <summary>
        /// Whether <paramref name="upper"/> is drawn over <paramref name="lower"/>.
        /// </summary>
        /// <remarks>
        /// Both are expected to be added to this test scene directly, which is the case for the preview and the layer
        /// standing in for the content it covers.
        /// </remarks>
        private bool drawnAbove(Drawable upper, Drawable lower) => IndexOf(upper) > IndexOf(lower);

        /// <summary>
        /// Sets the zoom, standing in for the user reaching a given zoom through scrolling or the toolbar buttons.
        /// </summary>
        private void setZoom(float value) => ((Bindable<float>)overlay.CurrentZoom).Value = value;

        private ImagePreviewRequest createRequest(Drawable? source = null) => new ImagePreviewRequest(texture, image_url, null, source);

        private Sprite? displayedImage() => overlay.ChildrenOfType<Sprite>().FirstOrDefault(s => s.Texture == texture);

        private IconButton? findButton(LocalisableString tooltip) =>
            overlay.ChildrenOfType<IconButton>().FirstOrDefault(b => b.TooltipText.ToString() == tooltip.ToString());
    }
}
