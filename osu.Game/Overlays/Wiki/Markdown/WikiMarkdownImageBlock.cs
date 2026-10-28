// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using Markdig.Syntax.Inlines;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Containers.Markdown;
using osu.Framework.Graphics.Sprites;
using osu.Game.Overlays.ImagePreview;
using osuTK;

namespace osu.Game.Overlays.Wiki.Markdown
{
    public partial class WikiMarkdownImageBlock : FillFlowContainer
    {
        [Resolved]
        private IMarkdownTextFlowComponent parentFlowComponent { get; set; } = null!;

        private readonly LinkInline linkInline;

        public WikiMarkdownImageBlock(LinkInline linkInline)
        {
            this.linkInline = linkInline;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 3);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            MarkdownTextFlowContainer textFlow;

            Children = new Drawable[]
            {
                new BlockMarkdownImage(linkInline),
                textFlow = parentFlowComponent.CreateTextFlow().With(t =>
                {
                    t.Anchor = Anchor.TopCentre;
                    t.Origin = Anchor.TopCentre;
                    t.TextAnchor = Anchor.TopCentre;
                }),
            };

            textFlow.AddText(linkInline.Title);
        }

        private partial class BlockMarkdownImage : WikiMarkdownImage
        {
            public BlockMarkdownImage(LinkInline linkInline)
                : base(linkInline)
            {
                AutoSizeAxes = Axes.Y;
                RelativeSizeAxes = Axes.X;
            }

            protected override ImageContainer CreateImageContainerCore(string mediaUrl)
            {
                var container = new BlockImageContainer(mediaUrl);
                container.OnLoadComplete += d => d.FadeInFromZero(300, Easing.OutQuint);
                return container;
            }

            private partial class BlockImageContainer : ImageContainer
            {
                private PreviewableSprite sprite = null!;

                public BlockImageContainer(string url)
                    : base(url)
                {
                    // the sprite is created from ImageContainer's constructor, which runs before this type's fields are
                    // assigned, so the url can only be handed over to it here.
                    sprite.Url = url;

                    AutoSizeAxes = Axes.Y;
                    RelativeSizeAxes = Axes.X;
                }

                protected override Sprite CreateImageSprite() => sprite = new ImageSprite();

                private partial class ImageSprite : PreviewableSprite
                {
                    public ImageSprite()
                    {
                        Anchor = Anchor.TopCentre;
                        Origin = Anchor.TopCentre;
                    }

                    protected override void Update()
                    {
                        base.Update();

                        if (Width > Parent!.DrawWidth)
                        {
                            float ratio = Height / Width;
                            Width = Parent!.DrawWidth;
                            Height = ratio * Width;
                        }
                    }
                }
            }
        }
    }
}
