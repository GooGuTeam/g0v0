// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.ImagePreview;
using osuTK;

namespace osu.Game.Overlays.Screenshots
{
    public partial class ScreenshotGridTile : OsuClickableContainer
    {
        /// <summary>
        /// The size a tile is given until the display lays its tiles out, using a 16:9 aspect ratio.
        /// </summary>
        public static readonly Vector2 DEFAULT_SIZE = new Vector2(320, 180);

        private const float action_button_size = 24;
        private const float icon_scale = 0.7f;

        public APIScreenshotInfo Screenshot { get; }

        private readonly Action<APIScreenshotInfo> onDeleteRequested;

        private ThumbnailSprite thumbnail = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private IImagePreviewOverlay? previewOverlay { get; set; }

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private LargeTextureStore textures { get; set; } = null!;

        private Box hoverLayer = null!;

        public ScreenshotGridTile(APIScreenshotInfo screenshot, Action<APIScreenshotInfo> onDeleteRequested)
        {
            Screenshot = screenshot;
            this.onDeleteRequested = onDeleteRequested;

            Size = DEFAULT_SIZE;
            Masking = true;
            CornerRadius = 4;
            CornerExponent = 2.5f;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddRangeInternal(new Drawable[]
            {
                new DelayedLoadWrapper(thumbnail = new ThumbnailSprite(Screenshot.ImageUrl)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fill,
                }, 200),
                hoverLayer = new Box
                {
                    Name = @"Hover layer",
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.White.Opacity(0.2f),
                    Alpha = 0,
                },
                new Box
                {
                    Name = @"Bottom gradient",
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    RelativeSizeAxes = Axes.X,
                    Height = action_button_size + 8,
                    Colour = ColourInfo.GradientVertical(Colour4.Black.Opacity(0), Colour4.Black.Opacity(0.6f)),
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(4),
                    Padding = new MarginPadding(5),
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Size = new Vector2(action_button_size * icon_scale),
                            Icon = FontAwesome.Solid.MousePointer,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Text = Screenshot.Hits.ToString(),
                        },
                    },
                },
                new GridContainer
                {
                    Name = @"Bottom information",
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(5),
                    RowDimensions = [new Dimension(GridSizeMode.AutoSize)],
                    ColumnDimensions =
                    [
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(GridSizeMode.AutoSize),
                    ],
                    Content = new Drawable[][]
                    {
                        [
                            new FillFlowContainer
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(4),
                                Children = new Drawable[]
                                {
                                    new SpriteIcon
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Size = new Vector2(action_button_size * icon_scale),
                                        Icon = FontAwesome.Solid.Clock,
                                    },
                                    new DrawableDate(Screenshot.Timestamp, 14)
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                    },
                                },
                            },
                            new IconButton
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Size = new Vector2(action_button_size),
                                IconScale = new Vector2(icon_scale),
                                Icon = FontAwesome.Solid.Link,
                                TooltipText = CommonStrings.CopyLink,
                                Action = () => game?.CopyToClipboard(Screenshot.Url),
                            },
                            new IconButton
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Size = new Vector2(action_button_size),
                                IconScale = new Vector2(icon_scale),
                                Icon = FontAwesome.Solid.Trash,
                                TooltipText = CommonStrings.DeleteWithConfirmation,
                                Action = () => onDeleteRequested(Screenshot),
                            },
                        ],
                    },
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background5,
                    Depth = float.MaxValue,
                },
            });
        }

        protected override bool OnHover(HoverEvent e)
        {
            hoverLayer.FadeIn(200, Easing.OutQuint);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hoverLayer.FadeOut(200, Easing.OutQuint);
            base.OnHoverLost(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (previewOverlay == null || thumbnail.Texture?.Available != true)
                return true;

            // the preview releases the texture it is handed when it is closed, so it is given a reference of its
            // own. handing over the tile's reference would leave the tile pointing at a released texture, which
            // crashes as soon as the tile is drawn again.
            Texture? previewTexture = textures.Get(Screenshot.ImageUrl);

            if (previewTexture == null)
                return true;

            // the share url is handed over rather than the direct one, as it is the canonical link of the screenshot
            // and the one worth copying. opening it is a deliberate action by the user at this point.
            previewOverlay.ShowImage(new ImagePreviewRequest(previewTexture, Screenshot.Url, source: this));
            return true;
        }

        private partial class ThumbnailSprite : Sprite
        {
            private readonly string url;

            [Resolved]
            private LargeTextureStore textures { get; set; } = null!;

            [Resolved]
            private GameHost host { get; set; } = null!;

            public ThumbnailSprite(string url)
            {
                this.url = url;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Task.Factory.StartNew(() =>
                {
                    Texture? texture;

                    try
                    {
                        texture = textures.Get(url);
                    }
                    catch
                    {
                        // a single failing image should leave the tile blank rather than take the game down.
                        texture = null;
                    }

                    if (texture == null)
                        return;

                    host.UpdateThread.Scheduler.Add(() =>
                    {
                        if (IsDisposed)
                        {
                            // the tile went away while the image was still being fetched, so nothing owns this reference.
                            texture.Dispose();
                            return;
                        }

                        Texture = texture;
                    });
                }, TaskCreationOptions.LongRunning);
            }
        }
    }
}
