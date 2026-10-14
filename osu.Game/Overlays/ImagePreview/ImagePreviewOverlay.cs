// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.IO.Network;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays.Notifications;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.ImagePreview
{
    /// <summary>
    /// A full-screen overlay which presents a single image, with zooming, panning, link copying and save-to-disk capabilities.
    /// </summary>
    /// <remarks>
    /// This overlay is cached into game dependencies, so any drawable can resolve <see cref="IImagePreviewOverlay"/>
    /// and call <see cref="ShowImage"/> to present an image.
    /// </remarks>
    public partial class ImagePreviewOverlay : OsuFocusedOverlayContainer, IImagePreviewOverlay
    {
        private const float transition_duration = 250;
        private const float zoom_step = 1.25f;
        private const float min_zoom = 0.1f;
        private const float max_zoom = 10f;

        /// <summary>
        /// The padding kept between the image and the edges of the screen when fitting it to the display area.
        /// </summary>
        private const float display_padding = 100;

        private readonly BindableFloat zoom = new BindableFloat(1) { MinValue = min_zoom, MaxValue = max_zoom };

        private Box background = null!;
        private Container imageArea = null!;
        private Container imageHolder = null!;
        private Sprite image = null!;
        private Container toolbar = null!;
        private OsuSpriteText zoomText = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        private Storage exportStorage = null!;

        private ImagePreviewRequest? currentRequest;
        private Vector2 nativeSize = Vector2.One;

        /// <summary>
        /// Whether an open or close transition is currently playing.
        /// The image position is left alone while a transition is in progress, otherwise it would be clamped to the display area immediately.
        /// </summary>
        private bool transitionInProgress;

        /// <summary>
        /// The current zoom, where 1 represents the default fit-to-screen size.
        /// </summary>
        public IBindable<float> CurrentZoom => zoom;

        /// <summary>
        /// The container the image is drawn into.
        /// Exposed internally so tests can verify transition behaviour.
        /// </summary>
        internal Container ImageHolder => imageHolder;

        public ImagePreviewOverlay()
        {
            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            Masking = true;
        }

        [BackgroundDependencyLoader]
        private void load(Storage storage, OsuColour colours)
        {
            exportStorage = storage.GetStorageForDirectory(@"exports/images");

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black.Opacity(0.9f),
                },
                imageArea = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = imageHolder = new Container
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Child = image = new Sprite
                        {
                            RelativeSizeAxes = Axes.Both,
                            FillMode = FillMode.Stretch,
                        },
                    },
                },
                toolbar = createToolbar(colours),
            };

            zoom.BindValueChanged(v =>
            {
                imageHolder.ScaleTo(new Vector2(v.NewValue), 100, Easing.OutQuint);
                zoomText.Text = $@"{v.NewValue * 100:0}%";
            }, true);
        }

        public void ShowImage(ImagePreviewRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            currentRequest = request;
            nativeSize = new Vector2(Math.Max(request.Texture.DisplayWidth, 1), Math.Max(request.Texture.DisplayHeight, 1));
            image.Texture = request.Texture;

            zoom.Value = zoom.Default;
            imageHolder.Position = Vector2.Zero;
            updateFittedSize();

            if (State.Value == Visibility.Visible)
            {
                // already displaying an image, just swap the content out.
                imageHolder.Scale = Vector2.One;
                return;
            }

            Show();
        }

        protected override void Update()
        {
            base.Update();

            if (currentRequest == null)
                return;

            updateFittedSize();

            if (!transitionInProgress)
                clampPosition();
        }

        private void updateFittedSize()
        {
            Vector2 area = DrawSize - new Vector2(display_padding * 2);

            if (area.X <= 0 || area.Y <= 0)
                return;

            float fitScale = Math.Min(area.X / nativeSize.X, area.Y / nativeSize.Y);
            Vector2 fitted = nativeSize * fitScale;

            if (imageHolder.Size != fitted)
                imageHolder.Size = fitted;
        }

        private void clampPosition()
        {
            Vector2 scaledSize = imageHolder.Size * zoom.Value;
            Vector2 maxOffset = new Vector2(
                Math.Max((scaledSize.X - imageArea.DrawWidth) / 2, 0),
                Math.Max((scaledSize.Y - imageArea.DrawHeight) / 2, 0));
            Vector2 clamped = Vector2.Clamp(imageHolder.Position, -maxOffset, maxOffset);

            if (clamped != imageHolder.Position)
                imageHolder.Position = clamped;
        }

        protected override void PopIn()
        {
            ClearTransforms(true);
            Alpha = 1;

            bool hasSource = tryGetSourceQuad(out Quad sourceQuad);

            background.FadeIn(transition_duration, Easing.OutQuint);
            toolbar.Delay(80).FadeIn(transition_duration, Easing.OutQuint);

            transitionInProgress = true;

            imageHolder.Position = hasSource ? ToLocalSpace(sourceQuad.Centre) - DrawSize / 2 : Vector2.Zero;
            imageHolder.Scale = new Vector2(hasSource ? sourceQuad.Width / imageHolder.Size.X : 0.9f);
            imageHolder.Alpha = hasSource ? 1 : 0;

            imageHolder.FadeIn(transition_duration, Easing.OutQuint)
                        .MoveTo(Vector2.Zero, transition_duration, Easing.OutQuint)
                        .ScaleTo(Vector2.One, transition_duration, Easing.OutQuint)
                        .OnComplete(_ => transitionInProgress = false);
        }

        protected override void PopOut()
        {
            base.PopOut();

            ClearTransforms(true);

            bool hasSource = tryGetSourceQuad(out Quad sourceQuad);

            background.FadeOut(transition_duration, Easing.OutQuint);
            toolbar.FadeOut(transition_duration / 2, Easing.OutQuint);

            transitionInProgress = true;

            imageHolder.FadeTo(hasSource ? 1 : 0, transition_duration, Easing.OutQuint)
                        .MoveTo(hasSource ? ToLocalSpace(sourceQuad.Centre) - DrawSize / 2 : Vector2.Zero, transition_duration, Easing.OutQuint)
                        .ScaleTo(new Vector2(hasSource ? sourceQuad.Width / imageHolder.Size.X : 0.9f), transition_duration, Easing.OutQuint)
                        .OnComplete(_ =>
                        {
                            transitionInProgress = false;

                            // fully hide once the image has returned to its origin, otherwise it would be clamped back to the centre.
                            Alpha = 0;
                        });
        }

        /// <summary>
        /// Attempts to find the screen-space quad of the drawable the current image originated from.
        /// </summary>
        private bool tryGetSourceQuad(out Quad quad)
        {
            quad = default;

            if (currentRequest?.Source is not Drawable source || !source.IsPresent)
                return false;

            Quad sourceQuad = source.ScreenSpaceDrawQuad;

            if (sourceQuad.Width <= 0 || imageHolder.Size.X <= 0)
                return false;

            quad = sourceQuad;
            return true;
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            if (currentRequest == null)
                return base.OnScroll(e);

            adjustZoom(e.ScrollDelta.Y > 0 ? zoom_step : 1 / zoom_step);
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            // panning only makes sense once the image is larger than the display area.
            return currentRequest != null && zoom.Value > 1;
        }

        protected override void OnDrag(DragEvent e)
        {
            imageHolder.Position += e.Delta;
            clampPosition();
        }

        private void adjustZoom(float factor)
        {
            zoom.Value = Math.Clamp(zoom.Value * factor, min_zoom, max_zoom);
        }

        private void copyLink()
        {
            if (currentRequest == null)
                return;

            game?.CopyToClipboard(currentRequest.Url);
        }

        private void saveImage()
        {
            if (currentRequest == null)
                return;

            var request = currentRequest;

            Task.Run(async () =>
            {
                try
                {
                    byte[] data;

                    using (var webRequest = new WebRequest(request.Url))
                    {
                        await webRequest.PerformAsync().ConfigureAwait(false);
                        data = webRequest.GetResponseData();
                    }

                    string filename = getFilenameForUrl(request.Url);

                    using (var stream = exportStorage.CreateFileSafely(filename))
                        await stream.WriteAsync(data).ConfigureAwait(false);

                    Schedule(() => notifications?.Post(new SimpleNotification
                    {
                        Text = ImagePreviewOverlayStrings.ImageSaved(filename),
                        Icon = FontAwesome.Solid.Download,
                        Activated = () =>
                        {
                            exportStorage.PresentFileExternally(filename);
                            return true;
                        },
                    }));
                }
                catch (Exception)
                {
                    Schedule(() => notifications?.Post(new SimpleNotification
                    {
                        Text = ImagePreviewOverlayStrings.ImageSaveFailed,
                        Icon = FontAwesome.Solid.ExclamationTriangle,
                    }));
                }
            });
        }

        private Container createToolbar(OsuColour colours)
        {
            return new Container
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Y = -30,
                AutoSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 10,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.GreySeaFoamDarker,
                        Alpha = 0.92f,
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(4),
                        Padding = new MarginPadding(8),
                        Children = new Drawable[]
                        {
                            new IconButton
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Icon = FontAwesome.Solid.Minus,
                                TooltipText = ImagePreviewOverlayStrings.ZoomOut,
                                Action = () => adjustZoom(1 / zoom_step),
                            },
                            new Container
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Size = new Vector2(56, IconButton.DEFAULT_BUTTON_SIZE),
                                Child = zoomText = new OsuSpriteText
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                                },
                            },
                            new IconButton
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Icon = FontAwesome.Solid.Plus,
                                TooltipText = ImagePreviewOverlayStrings.ZoomIn,
                                Action = () => adjustZoom(zoom_step),
                            },
                            new Box
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Width = 1,
                                Height = 20,
                                Margin = new MarginPadding { Horizontal = 6 },
                                Colour = Color4.White.Opacity(0.25f),
                            },
                            new IconButton
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Icon = FontAwesome.Solid.Link,
                                TooltipText = CommonStrings.CopyLink,
                                Action = copyLink,
                            },
                            new IconButton
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Icon = FontAwesome.Solid.Download,
                                TooltipText = ImagePreviewOverlayStrings.SaveImage,
                                Action = saveImage,
                            },
                            new IconButton
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Icon = FontAwesome.Solid.Times,
                                TooltipText = ImagePreviewOverlayStrings.ClosePreview,
                                Action = Hide,
                            },
                        },
                    },
                },
            };
        }

        private static string getFilenameForUrl(string url)
        {
            string name = @"image";
            string extension = @".png";

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
                return name + extension;

            string path = uri.AbsolutePath;

            // some urls (such as image proxies) carry the real image url in a query parameter.
            foreach (string parameter in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = parameter.Split('=', 2);

                if (parts.Length == 2 && parts[0].Equals(@"url", StringComparison.OrdinalIgnoreCase))
                {
                    path = Uri.UnescapeDataString(parts[1]);
                    break;
                }
            }

            string extensionPart = Path.GetExtension(path);
            string namePart = Path.GetFileNameWithoutExtension(path);

            if (!string.IsNullOrEmpty(extensionPart))
                extension = extensionPart;

            if (!string.IsNullOrEmpty(namePart))
                name = namePart;

            return name + extension;
        }
    }
}
