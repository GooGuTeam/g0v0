// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Markdig.Syntax.Inlines;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.Containers.Markdown;
using osu.Game.Overlays.ImagePreview;
using osuTK;

namespace osu.Game.Overlays.Changelog
{
    /// <summary>
    /// An image embedded in a changelog entry, which occupies the full width available to it, is centred, and opens a
    /// full-size preview of itself when clicked.
    /// </summary>
    public partial class ChangelogMarkdownImage : OsuMarkdownImage
    {
        public ChangelogMarkdownImage(LinkInline linkInline)
            : base(linkInline)
        {
            // the auto size axes have to be relaxed before the relative size axes may be set, as the two may not overlap.
            AutoSizeAxes = Axes.Y;
            RelativeSizeAxes = Axes.X;
        }

        protected override ImageContainer CreateImageContainerCore(string mediaUrl)
        {
            var container = new ChangelogImageContainer(mediaUrl);
            container.OnLoadComplete += d => d.FadeInFromZero(300, Easing.OutQuint);
            return container;
        }

        private partial class ChangelogImageContainer : ImageContainer
        {
            private PreviewableSprite sprite = null!;

            public ChangelogImageContainer(string url)
                : base(url)
            {
                AutoSizeAxes = Axes.Y;
                RelativeSizeAxes = Axes.X;

                // the sprite is created from ImageContainer's constructor, which runs before this type's fields are
                // assigned, so the url can only be handed over to it here.
                sprite.Url = url;
            }

            protected override Sprite CreateImageSprite() => sprite = new ChangelogImageSprite();

            private partial class ChangelogImageSprite : PreviewableSprite
            {
                public ChangelogImageSprite()
                {
                    Anchor = Anchor.TopCentre;
                    Origin = Anchor.TopCentre;
                }

                protected override void Update()
                {
                    base.Update();

                    if (Texture == null)
                        return;

                    var nativeSize = new Vector2(Texture.DisplayWidth, Texture.DisplayHeight);

                    // the sprite is given the texture's size when the texture is assigned, which happens before the
                    // container has a width to fit it into, so it has to be refitted whenever that width changes.
                    float availableWidth = Parent?.DrawWidth ?? 0;

                    Size = availableWidth > 0 && nativeSize.X > availableWidth
                        ? new Vector2(availableWidth, nativeSize.Y * (availableWidth / nativeSize.X))
                        : nativeSize;
                }
            }
        }
    }
}
