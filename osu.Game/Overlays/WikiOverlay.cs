// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Game.Extensions;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Wiki;

namespace osu.Game.Overlays
{
    public partial class WikiOverlay : OnlineOverlay<WikiHeader>
    {
        public const string INDEX_PATH = @"Main_page";

        public string CurrentPath => path.Value;

        private readonly Bindable<string> path = new Bindable<string>(INDEX_PATH);
        private readonly Bindable<APIWikiPage?> wikiData = new Bindable<APIWikiPage?>();

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        private GetWikiRequest? request;
        private CancellationTokenSource? cancellationToken;
        private WikiArticlePage? articlePage;

        private bool displayUpdateRequired = true;

        public WikiOverlay()
            : base(OverlayColourScheme.Orange, false)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            path.BindValueChanged(onPathChanged);
            wikiData.BindTo(Header.WikiPageData);

            Header.CurrentLanguage.Value = game.CurrentLanguage.Value;
            Header.CurrentLanguage.BindValueChanged(onLangChanged);
        }

        public void ShowPage(string pagePath = INDEX_PATH)
        {
            path.Value = pagePath.Trim('/');
            Show();
        }

        protected override WikiHeader CreateHeader() => new WikiHeader
        {
            ShowIndexPage = () => ShowPage(),
            ShowParentPage = showParentPage,
        };

        protected override void PopIn()
        {
            base.PopIn();

            if (displayUpdateRequired)
            {
                path.TriggerChange();
                displayUpdateRequired = false;
            }
        }

        protected override void PopOutComplete()
        {
            base.PopOutComplete();
            displayUpdateRequired = true;
        }

        protected void LoadDisplay(Drawable display)
        {
            ScrollFlow.ScrollToStart();
            LoadComponentAsync(display, loaded =>
            {
                Child = loaded;
                Loading.Hide();
            }, (cancellationToken = new CancellationTokenSource()).Token);
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            if (articlePage != null)
            {
                articlePage.SidebarContainer.Height = DrawHeight;
                articlePage.SidebarContainer.Y = (float)Math.Clamp(ScrollFlow.Current - Header.DrawHeight, 0, Math.Max(ScrollFlow.ScrollContent.DrawHeight - DrawHeight - Header.DrawHeight, 0));
            }
        }

        private void loadPage(string path, Language lang)
        {
            cancellationToken?.Cancel();
            request?.Cancel();

            // Language code + path, or just path1 + path2 in case
            string[] values = path.Split('/', 2);

            string pagePath = path;
            Language pageLanguage = lang;

            if (values.Length > 1 && LanguageExtensions.TryParseCultureCode(values[0], out var parsedLang))
            {
                pagePath = values[1];
                pageLanguage = parsedLang;
            }

            var newRequest = new GetWikiRequest(pagePath, pageLanguage);

            request = newRequest;

            Loading.Show();

            // the callbacks below are scheduled, so they must refer to the request they were created for rather than
            // to the mutable field, which may already point at a newer request by the time they run.
            newRequest.Success += response => Schedule(() =>
            {
                if (request != newRequest)
                    return;

                onSuccess(response);
            });

            newRequest.Failure += ex =>
            {
                if (ex is OperationCanceledException)
                    return;

                Schedule(() =>
                {
                    if (request != newRequest)
                        return;

                    // the language is passed on alongside the path because a language prefix in the path takes precedence
                    // over the language of the header, which may not be the one the failed page was requested in.
                    onFail(ex, pagePath, pageLanguage);
                });
            };

            api.PerformAsync(newRequest);
        }

        private void onPathChanged(ValueChangedEvent<string> e)
        {
            // the path could change as a result of redirecting to a newer location of the same page.
            // we already have the correct wiki data, so we can safely return here.
            if (e.NewValue == wikiData.Value?.Path)
                return;

            if (e.NewValue == "error")
                return;

            loadPage(e.NewValue, Header.CurrentLanguage.Value);
        }

        private void onLangChanged(ValueChangedEvent<Language> e)
        {
            // Path unmodified, just reload the page with new language value.
            loadPage(path.Value, e.NewValue);
        }

        private void onSuccess(APIWikiPage response)
        {
            wikiData.Value = response;
            path.Value = response.Path;

            if (response.Layout.Equals(INDEX_PATH, StringComparison.OrdinalIgnoreCase))
            {
                LoadDisplay(new WikiMainPage
                {
                    Markdown = response.Markdown,
                    Padding = new MarginPadding
                    {
                        Vertical = 20,
                        Horizontal = HORIZONTAL_PADDING,
                    },
                });
            }
            else
            {
                LoadDisplay(articlePage = new WikiArticlePage($@"{api.Endpoints.WebsiteUrl}/wiki/{path.Value}/",
                    response.Markdown,
                    Header.IsFallback.Value,
                    Header.CurrentLanguage.Value.GetDescription()));
            }
        }

        private void onFail(Exception exception, string pagePath, Language pageLanguage)
        {
            wikiData.Value = null;
            path.Value = "error";

            articlePage = null;

            // when the page is missing, show a button to navigate back.
            LoadDisplay(new OnlineErrorPlaceholder(
                exception,
                OnlineErrorStrings.PageUnavailableDescription,
                () => loadPage(pagePath, pageLanguage),
                new OnlineErrorAction(OnlineErrorStrings.BackToHome, () => ShowPage())));
        }

        private void showParentPage()
        {
            string parentPath = string.Join("/", path.Value.Split('/').SkipLast(1));
            ShowPage(parentPath);
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellationToken?.Cancel();
            request?.Cancel();
            base.Dispose(isDisposing);
        }
    }
}
