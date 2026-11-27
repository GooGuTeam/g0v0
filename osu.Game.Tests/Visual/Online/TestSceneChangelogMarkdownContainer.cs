// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Overlays.Changelog;
using osu.Game.Overlays.ImagePreview;

namespace osu.Game.Tests.Visual.Online
{
    public partial class TestSceneChangelogMarkdownContainer : OsuTestScene
    {
        /// <summary>
        /// The width of the container the markdown is displayed in, far narrower than a real screenshot.
        /// </summary>
        private const float container_width = 20;

        /// <summary>
        /// The preview overlay, standing in for the game-wide one images are made previewable against.
        /// </summary>
        private ImagePreviewOverlay preview = null!;

        private ChangelogMarkdownContainer markdown = null!;
        private ChangelogMarkdownImage image = null!;
        private Sprite sprite = null!;

        private Texture texture = null!;

        [BackgroundDependencyLoader]
        private void load(TextureStore textures)
        {
            texture = textures.Get(@"Menu/logo")!;
        }

        [SetUp]
        public void Setup() => Schedule(() =>
        {
            preview = new ImagePreviewOverlay();

            Children = new Drawable[]
            {
                new Box
                {
                    Colour = Colour4.Black,
                    RelativeSizeAxes = Axes.Both,
                },
                new DependencyProvidingContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = container_width,
                    AutoSizeAxes = Axes.Y,
                    CachedDependencies = new (Type, object)[]
                    {
                        // mirrors the wiring done in OsuGame, where the overlay is cached so that any drawable can resolve it.
                        (typeof(IImagePreviewOverlay), preview),
                    },
                    Child = markdown = new ChangelogMarkdownContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        // mirrors the padding a changelog entry gives its message.
                        DocumentPadding = new MarginPadding(0),
                    },
                },
                preview,
            };
        });

        [Test]
        public void TestImageWiderThanContainerIsScaledToFit()
        {
            addImageSteps();

            // the image is fetched through the media endpoint, which the test environment cannot reach, so it never gets
            // a texture of its own, and therefore has nothing to be sized against.
            AddStep("give the image a texture", () => sprite.Texture = texture);
            AddUntilStep("image has size", () => sprite.DrawWidth > 0);

            AddAssert("texture is wider than the container", () => texture.DisplayWidth > container_width);
            AddAssert("image does not exceed container", () => sprite.DrawWidth <= container_width + Precision.FLOAT_EPSILON);
            AddAssert("image height scaled with width",
                () => Precision.AlmostEquals(sprite.DrawHeight, sprite.DrawWidth * texture.DisplayHeight / texture.DisplayWidth));
        }

        [Test]
        public void TestImageNarrowerThanContainerIsCentred()
        {
            addImageSteps();

            // a narrow image is the only case where the horizontal alignment of the image is observable.
            AddStep("give the image a narrow texture", () => sprite.Texture = texture.Crop(new RectangleF(0, 0, 10, 10)));
            AddUntilStep("image has size", () => sprite.DrawWidth > 0);

            AddAssert("image is not stretched", () => Precision.AlmostEquals(sprite.DrawWidth, 10));
            AddAssert("image is centred", () => Precision.AlmostEquals(sprite.ScreenSpaceDrawQuad.Centre.X, image.ScreenSpaceDrawQuad.Centre.X));
        }

        [Test]
        public void TestImageIsPreviewable()
        {
            addImageSteps();

            AddStep("give the image a texture", () => sprite.Texture = texture);
            AddUntilStep("image has size", () => sprite.DrawWidth > 0);

            // a container deriving from ImageContainer creates its sprite before its own constructor runs, so the url
            // is the thing most likely to be silently dropped on the way to the preview.
            AddAssert("image knows its url", () => !string.IsNullOrEmpty((sprite as PreviewableSprite)?.Url));

            // the image is already scaled down at this point, which is exactly the case the preview exists for - it is
            // the only way of seeing a screenshot that does not fit the overlay at its own proportions.
            bool clickHandled = false;

            AddStep("click image", () => clickHandled = sprite.TriggerClick());
            AddAssert("click was handled", () => clickHandled);

            AddUntilStep("preview shown", () => preview.State.Value == Visibility.Visible);
            AddAssert("preview shows the clicked image", () => preview.ChildrenOfType<Sprite>().Any(s => s.Texture == texture));
            AddAssert("preview keeps the image proportions", () => Precision.AlmostEquals(
                preview.ImageHolder.DrawWidth / preview.ImageHolder.DrawHeight,
                texture.DisplayWidth / texture.DisplayHeight));
        }

        private void addImageSteps()
        {
            AddStep("set content with an image", () => markdown.Text = "![image](https://example.com/image.png)");

            AddUntilStep("image created", () => markdown.ChildrenOfType<ChangelogMarkdownImage>().Any());
            AddUntilStep("image sprite created", () => markdown.ChildrenOfType<ChangelogMarkdownImage>().Any(i => i.ChildrenOfType<PreviewableSprite>().Any()));

            AddStep("take references", () =>
            {
                image = markdown.ChildrenOfType<ChangelogMarkdownImage>().Single();
                sprite = image.ChildrenOfType<PreviewableSprite>().Single();
            });
        }
    }
}
