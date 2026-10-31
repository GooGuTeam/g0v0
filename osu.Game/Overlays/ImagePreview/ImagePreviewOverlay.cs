// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Threading;
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
        private const float quick_transition_duration = 100;
        private const float zoom_step = 1.25f;
        private const float min_zoom = 0.1f;
        private const float max_zoom = 10f;

        /// <summary>
        /// The empty space kept around the image when fitting it to the display area.
        /// This keeps the image clear of the edges of the screen (and of the toolbar at the bottom),
        /// so that its edges are never viewed flush against them.
        /// </summary>
        /// <remarks>
        /// Exposed internally so tests can verify the padding is actually applied.
        /// Note that this only applies to the fitted size; a zoomed in image can be panned all the way to the edges.
        /// </remarks>
        internal const float DISPLAY_PADDING = 150;

        private readonly BindableFloat zoom = new BindableFloat(1)
        {
            MinValue = min_zoom,
            MaxValue = max_zoom,
        };

        private Box background = null!;
        private Container imageArea = null!;
        private Sprite image = null!;
        private Container toolbar = null!;
        private OsuSpriteText zoomText = null!;
        private IconButton saveButton = null!;
        private LoadingSpinner saveSpinner = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        private Storage exportStorage = null!;

        private ImagePreviewRequest? currentRequest;
        private Vector2 nativeSize = Vector2.One;

        /// <summary>
        /// The in-flight save operation, if any. Non-null exactly while a save is running, which is what the
        /// save button's busy state is derived from.
        /// </summary>
        /// <remarks>
        /// A save is abandoned when the overlay is closed, as the image it was started for is no longer on screen.
        /// </remarks>
        private CancellationTokenSource? saveCancellation;

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
        internal Container ImageHolder { get; private set; } = null!;

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
                    Child = ImageHolder = new Container
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
                ImageHolder.ScaleTo(new Vector2(v.NewValue), quick_transition_duration, Easing.OutQuint);
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
            updateFittedSize();

            if (State.Value == Visibility.Visible)
            {
                // already displaying an image, just swap the content out.
                resetContent();
                return;
            }

            Show();
        }

        /// <summary>
        /// Puts the displayed content back into its resting state (unzoomed, centred and opaque), and ends any transition
        /// which is still in flight.
        /// </summary>
        /// <remarks>
        /// The resting state is assigned directly rather than animated towards, because the view is not seen to move when
        /// the content behind it is swapped - and because the zoom bindable starts its own scale animation as soon as the
        /// zoom is reset, which would otherwise fight over the scale for the next 100ms.
        /// </remarks>
        private void resetContent()
        {
            transitionInProgress = false;

            ImageHolder.ClearTransforms();

            ImageHolder.Alpha = 1;
            ImageHolder.Position = Vector2.Zero;
            ImageHolder.Scale = Vector2.One;
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
            Vector2 area = DrawSize - new Vector2(DISPLAY_PADDING * 2);

            if (area.X <= 0 || area.Y <= 0)
                return;

            float fitScale = Math.Min(area.X / nativeSize.X, area.Y / nativeSize.Y);
            Vector2 fitted = nativeSize * fitScale;

            if (ImageHolder.Size != fitted)
                ImageHolder.Size = fitted;
        }

        private void clampPosition()
        {
            Vector2 scaledSize = ImageHolder.Size * zoom.Value;
            Vector2 maxOffset = new Vector2(
                Math.Max((scaledSize.X - imageArea.DrawWidth) / 2, 0),
                Math.Max((scaledSize.Y - imageArea.DrawHeight) / 2, 0));
            Vector2 clamped = Vector2.Clamp(ImageHolder.Position, -maxOffset, maxOffset);

            if (clamped != ImageHolder.Position)
                ImageHolder.Position = clamped;
        }

        protected override void PopIn()
        {
            ClearTransforms(true);
            Alpha = 1;

            bool hasSource = tryGetSourceQuad(out Quad sourceQuad);

            // everything fades in from fully transparent, which serves two purposes:
            // - the moment at which the preview does not line up with the source it was opened from
            //   (their sizes and positions rarely match exactly) is kept out of sight;
            // - the very first open of the overlay is a fade rather than a hard cut, as the alpha of the
            //   background and toolbar is otherwise only reset by a previous close.
            ImageHolder.Alpha = 0;
            background.Alpha = 0;
            toolbar.Alpha = 0;

            background.FadeIn(transition_duration, Easing.OutQuint);
            toolbar.Delay(80).FadeIn(transition_duration, Easing.OutQuint);

            transitionInProgress = true;

            ImageHolder.Position = hasSource ? ToLocalSpace(sourceQuad.Centre) - DrawSize / 2 : Vector2.Zero;
            ImageHolder.Scale = new Vector2(hasSource ? sourceQuad.Width / ImageHolder.Size.X : 0.9f);

            ImageHolder.FadeIn(transition_duration, Easing.OutQuint)
                       .MoveTo(Vector2.Zero, transition_duration, Easing.OutQuint)
                       .ScaleTo(Vector2.One, transition_duration, Easing.OutQuint)
                       .OnComplete(_ => transitionInProgress = false);
        }

        protected override void PopOut()
        {
            base.PopOut();

            cancelSave();

            ClearTransforms(true);

            bool hasSource = tryGetSourceQuad(out Quad sourceQuad);

            background.FadeOut(transition_duration, Easing.OutQuint);
            toolbar.FadeOut(transition_duration / 2, Easing.OutQuint);

            transitionInProgress = true;

            // the image is faded out along the same curve it is moved back on, which means it is already
            // almost invisible by the time it lines up with the source again - so the source takes over
            // without the two ever being seen on top of each other.
            ImageHolder.FadeOut(transition_duration, Easing.OutQuint)
                       .MoveTo(hasSource ? ToLocalSpace(sourceQuad.Centre) - DrawSize / 2 : Vector2.Zero, transition_duration, Easing.OutQuint)
                       .ScaleTo(new Vector2(hasSource ? sourceQuad.Width / ImageHolder.Size.X : 0.9f), transition_duration, Easing.OutQuint)
                       .OnComplete(_ =>
                       {
                           transitionInProgress = false;

                           // fully hide once the image has returned to its origin, otherwise it would be clamped back to the centre.
                           Alpha = 0;

                           // this overlay lives for the lifetime of the game, so the content is released here rather
                           // than being kept alive until the next image is previewed.
                           image.Texture = null;
                           currentRequest = null;
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

            if (sourceQuad.Width <= 0 || ImageHolder.Size.X <= 0)
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
            ImageHolder.Position += e.Delta;
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
            if (currentRequest == null || saveCancellation != null)
                return;

            var request = currentRequest;
            var cancellation = saveCancellation = new CancellationTokenSource();

            updateSaveState();

            Task.Run(async () =>
            {
                try
                {
                    byte[]? data;

                    using (var webRequest = new WebRequest(request.Url))
                    {
                        await webRequest.PerformAsync(cancellation.Token).ConfigureAwait(false);
                        data = webRequest.GetResponseData();
                    }

                    string filename = getFilenameForUrl(request.Url);

                    using (var stream = exportStorage.CreateFileSafely(filename))
                        await stream.WriteAsync(data, cancellation.Token).ConfigureAwait(false);

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
                catch (OperationCanceledException)
                {
                    // the overlay was closed before the image finished downloading, so there is nothing to report.
                }
                catch (Exception)
                {
                    Schedule(() => notifications?.Post(new SimpleNotification
                    {
                        Text = ImagePreviewOverlayStrings.ImageSaveFailed,
                        Icon = FontAwesome.Solid.ExclamationTriangle,
                    }));
                }
                finally
                {
                    Schedule(() =>
                    {
                        // a newer save has taken over (or this one was cancelled) - leave its state alone.
                        if (saveCancellation != cancellation)
                            return;

                        saveCancellation = null;
                        updateSaveState();
                    });
                }
            }, cancellation.Token);
        }

        /// <summary>
        /// Abandons an in-flight save, if any.
        /// </summary>
        private void cancelSave()
        {
            if (saveCancellation == null)
                return;

            saveCancellation.Cancel();
            saveCancellation = null;

            updateSaveState();
        }

        /// <summary>
        /// Reflects whether a save is currently running in the save button, which turns into a spinner while it is.
        /// </summary>
        private void updateSaveState()
        {
            bool saving = saveCancellation != null;

            saveButton.Enabled.Value = !saving;
            saveButton.Alpha = saving ? 0 : 1;

            if (saving)
                saveSpinner.Show();
            else
                saveSpinner.Hide();
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
                            new Container
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Size = new Vector2(IconButton.DEFAULT_BUTTON_SIZE),
                                Children = new Drawable[]
                                {
                                    saveButton = new IconButton
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Icon = FontAwesome.Solid.Download,
                                        TooltipText = ImagePreviewOverlayStrings.SaveImage,
                                        Action = saveImage,
                                    },
                                    // only one of these two is ever visible; the button is swapped for the spinner while saving.
                                    saveSpinner = new LoadingSpinner
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Size = new Vector2(16),
                                        Alpha = 0,
                                    },
                                },
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
