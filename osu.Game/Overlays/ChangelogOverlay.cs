// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Game.Input.Bindings;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Changelog;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    public partial class ChangelogOverlay : OnlineOverlay<ChangelogHeader>
    {
        public readonly Bindable<APIChangelogBuild> Current = new Bindable<APIChangelogBuild>();

        private List<APIChangelogBuild> builds;

        protected List<APIUpdateStream> Streams;

        public ChangelogOverlay()
            : base(OverlayColourScheme.Purple, false)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Header.Build.BindTarget = Current;

            Current.BindValueChanged(e =>
            {
                if (e.NewValue != null)
                    loadContent(new ChangelogSingleBuild(e.NewValue));
                else
                    loadContent(new ChangelogListing(builds));
            });
        }

        protected override ChangelogHeader CreateHeader() => new ChangelogHeader
        {
            ListingSelected = ShowListing,
        };

        protected override Color4 BackgroundColour => ColourProvider.Background4;

        public void ShowListing()
        {
            Current.Value = null;
            Show();
        }

        /// <summary>
        /// Fetches and shows a specific build from a specific update stream.
        /// </summary>
        /// <param name="build">Must contain at least <see cref="APIUpdateStream.Name"/> and
        /// <see cref="APIChangelogBuild.Version"/>. If <see cref="APIUpdateStream.DisplayName"/> and
        /// <see cref="APIChangelogBuild.DisplayVersion"/> are specified, the header will instantly display them.</param>
        public void ShowBuild([NotNull] APIChangelogBuild build)
        {
            ArgumentNullException.ThrowIfNull(build);

            Current.Value = build;
            Show();
        }

        public void ShowBuild([NotNull] string version)
        {
            ArgumentNullException.ThrowIfNull(version);

            Show();

            string[] split = version.Split('-');

            if (split.Length < 2)
                return;

            string versionPart = split[0];
            string updateStream = split[1];

            performAfterFetch(() =>
            {
                var build = builds.Find(b => b.Version == versionPart && b.UpdateStream.Name == updateStream)
                            ?? Streams.Find(s => s.Name == updateStream)?.LatestBuild;

                if (build != null)
                    ShowBuild(build);
            });
        }

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat)
                return false;

            switch (e.Action)
            {
                case GlobalAction.Back:
                    if (Current.Value == null)
                    {
                        Hide();
                    }
                    else
                    {
                        Current.Value = null;
                    }

                    return true;
            }

            return false;
        }

        protected override void PopIn()
        {
            base.PopIn();

            if (initialFetchTask == null)
                // fetch and refresh to show listing, if no other request was made via Show methods
                performAfterFetch(() => Current.TriggerChange());
        }

        /// <summary>
        /// The initial listing fetch, completing with the failure which prevented it (or <see langword="null"/> on success).
        /// </summary>
        private Task<Exception> initialFetchTask;

        private void performAfterFetch(Action action)
        {
            Debug.Assert(State.Value == Visibility.Visible);

            Schedule(() =>
            {
                fetchListing().ContinueWith(t => Schedule(() =>
                {
                    Exception failure = t.GetResultSafely();

                    if (failure == null)
                        action();
                    else
                        showFetchFailure(failure, action);
                }));
            });
        }

        private void showFetchFailure(Exception failure, Action retryAction)
        {
            Content.FadeIn(300, Easing.OutQuint);
            Content.Child = new OnlineErrorPlaceholder(failure, () =>
            {
                Content.Child = Empty();
                performAfterFetch(retryAction);
            });
        }

        private Task<Exception> fetchListing()
        {
            if (initialFetchTask != null)
                return initialFetchTask;

            return initialFetchTask = Task.Run(async () =>
            {
                var tcs = new TaskCompletionSource<Exception>();

                var req = new GetChangelogRequest();

                req.Success += res => Schedule(() =>
                {
                    // remap streams to builds to ensure model equality
                    res.Builds.ForEach(b => b.UpdateStream = res.Streams.Find(s => s.Id == b.UpdateStream.Id));
                    res.Streams.ForEach(s => s.LatestBuild.UpdateStream = res.Streams.Find(s2 => s2.Id == s.LatestBuild.UpdateStream.Id));

                    builds = res.Builds;
                    Streams = res.Streams;

                    Header.Populate(res.Streams);

                    tcs.SetResult(null);
                });

                req.Failure += e =>
                {
                    // allow a later attempt to fetch the listing again.
                    initialFetchTask = null;

                    Logger.Log($@"Failed to fetch the changelog listing: {e}", LoggingTarget.Network);

                    // the failure is reported to the user by the error state shown in `showFetchFailure()`, so the task
                    // itself completes successfully to avoid surfacing an unobserved exception.
                    tcs.SetResult(e);
                };

                await API.PerformAsync(req).ConfigureAwait(false);

                return await tcs.Task.ConfigureAwait(false);
            });
        }

        private CancellationTokenSource loadContentCancellation;

        private void loadContent(ChangelogContent newContent)
        {
            Content.FadeTo(0.2f, 300, Easing.OutQuint);

            loadContentCancellation?.Cancel();

            LoadComponentAsync(newContent, c =>
            {
                Content.FadeIn(300, Easing.OutQuint);

                c.BuildSelected = ShowBuild;
                Child = c;
            }, (loadContentCancellation = new CancellationTokenSource()).Token);
        }
    }
}
