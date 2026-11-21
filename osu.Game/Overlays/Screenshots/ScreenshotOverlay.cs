// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Net;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.Overlays.Screenshots
{
    public partial class ScreenshotOverlay : OnlineOverlay<ScreenshotOverlayHeader>
    {
        private const int page_size = 50;

        private readonly Container listContainer;
        private readonly ShowMoreButton showMore;
        private readonly IconButton refreshButton;

        private readonly ScreenshotManager? screenshotManager;

        private readonly IBindable<APIState> apiState = new Bindable<APIState>();

        private ScreenshotDisplay? display;

        private GetScreenshotsRequest? request;
        private CancellationTokenSource? cancellationToken;

        private int totalCount;

        /// <summary>
        /// Set when the current server has been determined to not support the private screenshot endpoints.
        /// No further requests will be issued while this is set.
        /// </summary>
        private bool unsupported;

        /// <summary>
        /// Whether the listing has been successfully fetched at least once.
        /// </summary>
        private bool listingLoaded;

        /// <summary>
        /// Set when the listing is known to no longer match the server, so that it is fetched again the next time
        /// the overlay is opened. Starts set, as nothing has been fetched yet.
        /// </summary>
        private bool listingStale = true;

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        public ScreenshotOverlay(ScreenshotManager? screenshotManager = null)
            : base(OverlayColourScheme.Purple)
        {
            this.screenshotManager = screenshotManager;

            Child = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Padding = new MarginPadding
                {
                    Vertical = 20,
                    Left = 30,
                    Right = HORIZONTAL_PADDING,
                },
                Children = new Drawable[]
                {
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Margin = new MarginPadding { Bottom = 8 },
                        ColumnDimensions =
                        [
                            new Dimension(),
                            new Dimension(GridSizeMode.AutoSize),
                        ],
                        RowDimensions = [new Dimension(GridSizeMode.AutoSize)],
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new SettingsNote
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Current =
                                    {
                                        Value = new SettingsNote.Data(ScreenshotOverlayStrings.RetentionNote, SettingsNote.Type.Informational),
                                    },
                                },
                                refreshButton = new IconButton
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    Margin = new MarginPadding { Left = 8 },
                                    Icon = FontAwesome.Solid.Sync,
                                    TooltipText = ScreenshotOverlayStrings.Refresh,
                                    Action = refresh,
                                },
                            },
                        },
                    },
                    listContainer = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                    },
                    showMore = new ShowMoreButton
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Margin = new MarginPadding { Top = 15 },
                        Action = loadMore,
                        Alpha = 0,
                    },
                },
            };
        }

        protected override ScreenshotOverlayHeader CreateHeader() => new ScreenshotOverlayHeader();

        [BackgroundDependencyLoader]
        private void load()
        {
            if (screenshotManager != null)
                screenshotManager.ScreenshotUploaded += InvalidateListing;

            apiState.BindTo(API.State);
            apiState.BindValueChanged(_ =>
            {
                if (!API.IsLoggedIn || State.Value != Visibility.Visible || unsupported || display != null)
                    return;

                loadInitialIfRequired();
            });
        }

        /// <summary>
        /// Marks the listing as out of date, so that it is brought up to date the next time the overlay is opened.
        /// </summary>
        /// <remarks>
        /// Used when a screenshot has been uploaded. The listing is intentionally not refreshed right away, as its
        /// contents should never change while the user is looking at them.
        /// </remarks>
        public void InvalidateListing()
        {
            if (listingStale)
                return;

            Schedule(() => listingStale = true);
        }

        protected override void PopIn()
        {
            base.PopIn();

            // should not be run until first pop-in to avoid requesting data before user views.
            loadInitialIfRequired();
        }

        private void loadInitialIfRequired()
        {
            if (!API.IsLoggedIn || unsupported)
                return;

            // the listing is kept as-is across closing and reopening - including the position the user had scrolled
            // to - so it is only fetched when there is nothing to show yet, or when it went stale in the meantime.
            if (listingLoaded && !listingStale)
                return;

            listingStale = false;
            loadInitial();
        }

        private void refresh()
        {
            if (!API.IsLoggedIn || unsupported)
                return;

            listingStale = false;
            loadInitial();
        }

        private void loadInitial()
        {
            beginLoading(true);

            var initialRequest = request = new GetScreenshotsRequest(page_size);
            initialRequest.Success += response => Schedule(() =>
            {
                if (request != initialRequest)
                    return;

                listingLoaded = true;
                totalCount = response.Total;

                var newDisplay = new ScreenshotDisplay(onDeleteRequested);
                newDisplay.AddScreenshots(response.Screenshots);
                loadDisplay(newDisplay);

                if (totalCount == 0)
                    loadPlaceholder(createEmptyState());
            });
            initialRequest.Failure += e => onInitialFailure(e, initialRequest, loadInitial);

            API.PerformAsync(initialRequest);
        }

        private void loadMore()
        {
            if (display == null)
                return;

            beginLoading(false);
            showMore.IsLoading = true;

            var moreRequest = request = new GetScreenshotsRequest(page_size, display.Count);
            moreRequest.Success += response => Schedule(() =>
            {
                if (request != moreRequest)
                    return;

                totalCount = response.Total;
                display.AddScreenshots(response.Screenshots);
                showMore.IsLoading = false;
                updateShowMoreVisibility();
            });
            moreRequest.Failure += e =>
            {
                if (e is OperationCanceledException || request != moreRequest)
                    return;

                // the already displayed screenshots remain usable, so only the failure to extend them is recorded.
                Logger.Log($@"Failed to fetch more screenshots: {e}", LoggingTarget.Network);

                Schedule(() => showMore.IsLoading = false);
            };

            API.PerformAsync(moreRequest);
        }

        private void onInitialFailure(Exception e, GetScreenshotsRequest initialRequest, Action retry)
        {
            if (e is OperationCanceledException || request != initialRequest)
                return;

            Logger.Log($@"Failed to fetch the screenshot listing: {e}", LoggingTarget.Network);

            // the callback is scheduled, so a newer listing may have been requested in the meantime, in which case
            // this failure no longer describes what the user is looking at.
            Schedule(() =>
            {
                if (request != initialRequest)
                    return;

                if (e.HasStatusCode(HttpStatusCode.NotFound))
                {
                    // assume the current server does not implement the private screenshot endpoints at all
                    // and avoid retrying until the client is restarted (which is required to switch servers anyway).
                    unsupported = true;
                    refreshButton.Enabled.Value = false;
                    refreshButton.FadeTo(0.3f, 200, Easing.OutQuint);
                    loadPlaceholder(new OnlineErrorPlaceholder(ScreenshotOverlayStrings.UnsupportedTitle, ScreenshotOverlayStrings.UnsupportedDescription));
                    return;
                }

                loadPlaceholder(new OnlineErrorPlaceholder(e, retry));
            });
        }

        private void onDeleteRequested(APIScreenshotInfo screenshot)
        {
            if (dialogOverlay == null)
                deleteScreenshot(screenshot);
            else
                dialogOverlay.Push(new ConfirmDialog(ScreenshotOverlayStrings.DeleteConfirmation, () => deleteScreenshot(screenshot)));
        }

        private void deleteScreenshot(APIScreenshotInfo screenshot)
        {
            var deleteRequest = new DeleteScreenshotRequest(screenshot.Id);
            deleteRequest.Success += () => Schedule(() =>
            {
                totalCount = Math.Max(0, totalCount - 1);

                if (display != null && display.RemoveScreenshot(screenshot))
                    notifications?.Post(new SimpleNotification { Text = ScreenshotOverlayStrings.Deleted });

                updateShowMoreVisibility();

                if (totalCount == 0)
                    loadPlaceholder(createEmptyState());
            });
            deleteRequest.Failure += e =>
            {
                if (e is OperationCanceledException)
                    return;

                Logger.Log($@"Failed to delete screenshot {screenshot.Id}: {e}", LoggingTarget.Network);

                notifications?.Post(new SimpleErrorNotification { Text = ScreenshotOverlayStrings.DeleteFailed });
            };

            API.PerformAsync(deleteRequest);
        }

        private void loadDisplay(ScreenshotDisplay newDisplay)
        {
            ScrollFlow.ScrollToStart();
            LoadComponentAsync(newDisplay, loaded =>
            {
                display = loaded;
                listContainer.Child = loaded;
                Loading.Hide();
                updateShowMoreVisibility();
            }, (cancellationToken = new CancellationTokenSource()).Token);
        }

        private void loadPlaceholder(Drawable placeholder)
        {
            ScrollFlow.ScrollToStart();
            LoadComponentAsync(placeholder, loaded =>
            {
                display = null;
                listContainer.Child = loaded;
                Loading.Hide();
            }, (cancellationToken = new CancellationTokenSource()).Token);
        }

        private static Drawable createEmptyState() => new ScreenshotEmptyState();

        private void updateShowMoreVisibility() =>
            showMore.Alpha = display != null && display.Count < totalCount ? 1 : 0;

        private void beginLoading(bool showLoadingOverlay)
        {
            request?.Cancel();
            cancellationToken?.Cancel();

            if (showLoadingOverlay)
                Loading.Show();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (screenshotManager != null)
                screenshotManager.ScreenshotUploaded -= InvalidateListing;

            request?.Cancel();
            cancellationToken?.Cancel();
            base.Dispose(isDisposing);
        }

        private partial class ScreenshotEmptyState : CompositeDrawable
        {
            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                RelativeSizeAxes = Axes.X;
                Height = OnlineErrorPlaceholder.HEIGHT;

                InternalChild = new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Icon = FontAwesome.Solid.Camera,
                            Size = new Vector2(28),
                            Colour = colourProvider.Content2,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = ScreenshotOverlayStrings.EmptyTitle,
                            Font = OsuFont.GetFont(size: 18, weight: FontWeight.SemiBold),
                        },
                        new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 14))
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            TextAnchor = Anchor.TopCentre,
                            Text = ScreenshotOverlayStrings.EmptyDescription,
                            Colour = colourProvider.Content2,
                        },
                    },
                };
            }
        }
    }
}
