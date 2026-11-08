// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

#nullable disable

using System;
using System.Threading;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Game.Online.API.Requests;
using osu.Game.Overlays.News;
using osu.Game.Overlays.News.Displays;
using osu.Game.Overlays.News.Sidebar;

namespace osu.Game.Overlays
{
    public partial class NewsOverlay : OnlineOverlay<NewsHeader>
    {
        private readonly Bindable<string> article = new Bindable<string>();

        private readonly Container sidebarContainer;
        private readonly NewsSidebar sidebar;
        private readonly Container content;

        private GetNewsRequest request;

        private Cursor lastCursor;

        /// <summary>
        /// The year currently being displayed. If null, the main listing is being displayed.
        /// </summary>
        private int? displayedYear;

        private CancellationTokenSource cancellationToken;

        private bool displayUpdateRequired = true;

        public NewsOverlay()
            : base(OverlayColourScheme.Purple, false)
        {
            Child = new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.AutoSize)
                },
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension()
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        sidebarContainer = new Container
                        {
                            AutoSizeAxes = Axes.X,
                            Child = sidebar = new NewsSidebar()
                        },
                        content = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // should not be run until first pop-in to avoid requesting data before user views.
            article.BindValueChanged(a =>
            {
                if (a.NewValue == null)
                    loadListing();
                else
                    loadArticle(a.NewValue);
            });
        }

        protected override NewsHeader CreateHeader() => new NewsHeader { ShowFrontPage = ShowFrontPage };

        protected override void PopIn()
        {
            base.PopIn();

            if (displayUpdateRequired)
            {
                article.TriggerChange();
                displayUpdateRequired = false;
            }
        }

        protected override void PopOutComplete()
        {
            base.PopOutComplete();
            displayUpdateRequired = true;
        }

        public void ShowFrontPage()
        {
            article.Value = null;
            Show();
        }

        public void ShowYear(int year)
        {
            loadListing(year);
            Show();
        }

        public void ShowArticle(string slug)
        {
            article.Value = slug;
            Show();
        }

        protected void LoadDisplay(Drawable display)
        {
            ScrollFlow.ScrollToStart();
            LoadComponentAsync(display, loaded =>
            {
                content.Child = loaded;
                Loading.Hide();
            }, (cancellationToken = new CancellationTokenSource()).Token);
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();
            sidebarContainer.Height = DrawHeight;
            sidebarContainer.Y = (float)Math.Clamp(ScrollFlow.Current - Header.DrawHeight, 0, Math.Max(ScrollFlow.ScrollContent.DrawHeight - DrawHeight - Header.DrawHeight, 0));
        }

        private void loadListing(int? year = null)
        {
            Header.SetFrontPage();

            displayedYear = year;
            lastCursor = null;

            beginLoading(true);

            var listingRequest = request = new GetNewsRequest(displayedYear);
            listingRequest.Success += response => Schedule(() =>
            {
                lastCursor = response.Cursor;
                sidebar.Metadata.Value = response.SidebarMetadata;

                var listing = new ArticleListing(getMorePosts);
                listing.AddPosts(response.NewsPosts, response.Cursor != null);
                LoadDisplay(listing);
            });
            listingRequest.Failure += e =>
            {
                if (e is OperationCanceledException || request != listingRequest)
                    return;

                Logger.Log($@"Failed to fetch the news listing: {e}", level: LogLevel.Important);

                // the listing is the whole content of this view, so an error state is all that can be shown without it.
                Schedule(() => LoadDisplay(new OnlineErrorPlaceholder(e, () => loadListing(displayedYear))));
            };

            API.PerformAsync(listingRequest);
        }

        private void getMorePosts()
        {
            beginLoading(false);

            var postsRequest = request = new GetNewsRequest(displayedYear, lastCursor);
            postsRequest.Success += response => Schedule(() =>
            {
                lastCursor = response.Cursor;
                if (content.Child is ArticleListing listing)
                    listing.AddPosts(response.NewsPosts, response.Cursor != null);
            });
            postsRequest.Failure += e =>
            {
                if (e is OperationCanceledException || request != postsRequest)
                    return;

                // the already displayed posts remain usable, so only the failure to extend them is recorded.
                Logger.Log($@"Failed to fetch more news posts: {e}", level: LogLevel.Important);
            };

            API.PerformAsync(postsRequest);
        }

        private void loadArticle(string article)
        {
            // This is not yet implemented nor called from anywhere.
            beginLoading(true);

            Header.SetArticle(article);
            LoadDisplay(Empty());
        }

        private void beginLoading(bool showLoadingOverlay)
        {
            request?.Cancel();
            cancellationToken?.Cancel();

            if (showLoadingOverlay)
                Loading.Show();
        }

        protected override void Dispose(bool isDisposing)
        {
            request?.Cancel();
            cancellationToken?.Cancel();
            base.Dispose(isDisposing);
        }
    }
}
