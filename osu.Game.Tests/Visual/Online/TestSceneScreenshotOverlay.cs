// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Screenshots;

namespace osu.Game.Tests.Visual.Online
{
    public partial class TestSceneScreenshotOverlay : OsuTestScene
    {
        private const string direct_url_prefix = @"https://cdn.example.town/screenshots/";

        private DummyAPIAccess dummyAPI => (DummyAPIAccess)API;

        private ScreenshotOverlay overlay;

        private RecordingLargeTextureStore textureStore;

        private int requestCount;

        [Resolved]
        private GameHost host { get; set; }

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            requestCount = 0;

            // the cases below assume a signed-in user, but a previous test may have signed out.
            if (!dummyAPI.IsLoggedIn)
                logIn();

            Child = new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = new (Type, object)[]
                {
                    (typeof(LargeTextureStore), textureStore = new RecordingLargeTextureStore(host.Renderer)),
                },
                Child = overlay = new ScreenshotOverlay(),
            };
        });

        [Test]
        public void TestListingAndPagination()
        {
            setServerResponse(response(total: 120, from: 0, count: 50));
            AddStep("show", () => overlay.Show());

            AddUntilStep("first page shown", tileCount, () => Is.EqualTo(50));
            AddUntilStep("show more visible", () => showMore.Alpha == 1);

            setServerResponse(response(total: 120, from: 50, count: 50), "set second page response");
            AddStep("click show more", () => showMore.TriggerClick());

            AddUntilStep("second page appended", tileCount, () => Is.EqualTo(100));
            AddUntilStep("show more still visible", () => showMore.Alpha == 1);

            setServerResponse(response(total: 120, from: 100, count: 20), "set final page response");
            AddStep("click show more", () => showMore.TriggerClick());

            AddUntilStep("all screenshots shown", tileCount, () => Is.EqualTo(120));
            AddUntilStep("show more hidden", () => showMore.Alpha == 0);
        }

        [Test]
        public void TestUnsupportedServer()
        {
            setFailure(HttpStatusCode.NotFound, "set 404 response");
            AddStep("show", () => overlay.Show());

            AddUntilStep("placeholder shown", () => overlay.ChildrenOfType<OnlineErrorPlaceholder>().Any());
            AddUntilStep("no tiles shown", tileCount, () => Is.Zero);

            AddStep("hide and show again", () =>
            {
                overlay.Hide();
                overlay.Show();
            });

            AddAssert("no further requests issued", () => requestCount, () => Is.EqualTo(1));
        }

        [Test]
        public void TestDelete()
        {
            setServerResponse(response(total: 2, from: 0, count: 2));
            AddStep("show", () => overlay.Show());
            AddUntilStep("screenshots shown", tileCount, () => Is.EqualTo(2));

            AddStep("prepare delete handler", () =>
            {
                dummyAPI.AddHandler(request =>
                {
                    if (request is not DeleteScreenshotRequest deleteRequest)
                        return false;

                    deleteRequest.TriggerSuccess();
                    return true;
                });
            });

            AddStep("invoke delete on the first tile", invokeDeleteOnFirstTile);

            AddUntilStep("tile removed", tileCount, () => Is.EqualTo(1));
            AddUntilStep("show more hidden", () => showMore.Alpha == 0);
        }

        [Test]
        public void TestDeleteFailureKeepsTile()
        {
            setServerResponse(response(total: 2, from: 0, count: 2));
            AddStep("show", () => overlay.Show());
            AddUntilStep("screenshots shown", tileCount, () => Is.EqualTo(2));

            AddStep("prepare failing delete handler", () =>
            {
                dummyAPI.AddHandler(request =>
                {
                    if (request is not DeleteScreenshotRequest deleteRequest)
                        return false;

                    deleteRequest.TriggerFailure(new APIException(@"denied", null, HttpStatusCode.Forbidden));
                    return true;
                });
            });

            AddStep("invoke delete on the first tile", invokeDeleteOnFirstTile);

            AddAssert("tile kept", tileCount, () => Is.EqualTo(2));
        }

        [Test]
        public void TestGridLayout()
        {
            setServerResponse(response(total: 6, from: 0, count: 6));
            AddStep("show", () => overlay.Show());
            AddUntilStep("screenshots shown", tileCount, () => Is.EqualTo(6));

            AddUntilStep("tiles laid out", () => tiles().Select(t => t.DrawWidth).Distinct().Count(), () => Is.EqualTo(1));
            AddAssert("three tiles per row", firstRowCount, () => Is.EqualTo(3));
            AddAssert("first row fills the display", () => tiles().Take(3).Sum(t => t.DrawWidth) + 2 * 10, () => Is.EqualTo(display.DrawWidth).Within(1));
        }

        [Test]
        public void TestListingRefreshedAfterUpload()
        {
            setServerResponse(response(total: 1, from: 0, count: 1));
            AddStep("show", () => overlay.Show());
            AddUntilStep("screenshots shown", tileCount, () => Is.EqualTo(1));

            setServerResponse(response(total: 2, from: 0, count: 2), "set response including the uploaded screenshot");

            AddStep("hide", () => overlay.Hide());
            AddUntilStep("overlay hidden", () => overlay.State.Value, () => Is.EqualTo(Visibility.Hidden));

            AddStep("mark listing stale as an upload would", () => overlay.InvalidateListing());

            AddStep("show again", () => overlay.Show());
            AddUntilStep("listing refreshed", tileCount, () => Is.EqualTo(2));
            AddAssert("listing fetched again", () => requestCount, () => Is.EqualTo(2));
        }

        [Test]
        public void TestNoRequestWhileLoggedOut()
        {
            AddStep("sign out", () => dummyAPI.Logout());
            setServerResponse(response(total: 1, from: 0, count: 1));

            AddStep("show", () => overlay.Show());
            AddWaitStep("wait for a request that should not happen", 5);
            AddAssert("no request issued", () => requestCount, () => Is.Zero);
            AddAssert("no tiles shown", tileCount, () => Is.Zero);

            AddStep("sign in", logIn);

            AddUntilStep("listing loaded after signing in", tileCount, () => Is.EqualTo(1));
        }

        [Test]
        public void TestImagesLoadedFromDirectLink()
        {
            // the share link records a hit every time it is opened, so anything merely displaying an image has to use
            // the direct link. otherwise the hit count of every screenshot climbs each time the overlay is opened.
            setServerResponse(response(total: 2, from: 0, count: 2));
            AddStep("show", () => overlay.Show());

            AddUntilStep("images requested", () => textureStore.Requested.Count, () => Is.EqualTo(2));
            AddAssert("direct links requested", () => textureStore.Requested.All(u => u.StartsWith(direct_url_prefix, StringComparison.Ordinal)));
            AddAssert("share links not requested", () => textureStore.Requested.All(u => !u.Contains("/ss/")));
        }

        [Test]
        public void TestShareLinkUsedWhenNoDirectLinkIsAvailable()
        {
            AddAssert("direct link preferred", () => imageUrlFor(@"https://example.town/ss/0001", @"https://cdn.example.town/0001.jpg"), () => Is.EqualTo(@"https://cdn.example.town/0001.jpg"));
            AddAssert("share link used as fallback", () => imageUrlFor(@"https://example.town/ss/0001", null), () => Is.EqualTo(@"https://example.town/ss/0001"));
            AddAssert("share link used for an empty direct link", () => imageUrlFor(@"https://example.town/ss/0001", string.Empty), () => Is.EqualTo(@"https://example.town/ss/0001"));
        }

        private ScreenshotGridTile[] tiles() => overlay.ChildrenOfType<ScreenshotGridTile>().ToArray();

        private ScreenshotDisplay display => overlay.ChildrenOfType<ScreenshotDisplay>().Single();

        /// <summary>
        /// The number of tiles sharing the vertical position of the first tile, i.e. the size of the first row.
        /// </summary>
        private int firstRowCount()
        {
            var all = tiles();
            float firstRowY = all[0].Y;

            return all.Count(t => Math.Abs(t.Y - firstRowY) < 1);
        }

        private void logIn()
        {
            // the second factor would otherwise leave the dummy api in a state where the user is not fully signed in.
            dummyAPI.SessionVerificationMethod = null;
            dummyAPI.Login(string.Empty, string.Empty);
        }

        private int tileCount() => overlay.ChildrenOfType<ScreenshotGridTile>().Count();

        private ShowMoreButton showMore => overlay.ChildrenOfType<ShowMoreButton>().Single();

        private void invokeDeleteOnFirstTile()
        {
            var tile = overlay.ChildrenOfType<ScreenshotGridTile>().First();
            tile.ChildrenOfType<IconButton>().Single(b => b.Icon.Equals(FontAwesome.Solid.Trash)).TriggerClick();
        }

        private void setServerResponse(GetScreenshotsResponse r, string stepName = "set server response")
            => AddStep(stepName, () =>
            {
                dummyAPI.AddHandler(request =>
                {
                    if (request is not GetScreenshotsRequest getRequest)
                        return false;

                    Interlocked.Increment(ref requestCount);
                    getRequest.TriggerSuccess(r);
                    return true;
                });
            });

        private void setFailure(HttpStatusCode statusCode, string stepName)
            => AddStep(stepName, () =>
            {
                dummyAPI.AddHandler(request =>
                {
                    if (request is not GetScreenshotsRequest getRequest)
                        return false;

                    Interlocked.Increment(ref requestCount);
                    getRequest.TriggerFailure(new APIException(@"request failed", null, statusCode));
                    return true;
                });
            });

        private static string imageUrlFor(string url, string directUrl) => new APIScreenshotInfo
        {
            Url = url,
            DirectUrl = directUrl,
        }.ImageUrl;

        private static GetScreenshotsResponse response(int total, int from, int count) => new GetScreenshotsResponse
        {
            Total = total,
            Screenshots = Enumerable.Range(from, count).Select(i => new APIScreenshotInfo
            {
                Id = i,
                Url = $@"https://example.town/ss/{i:D4}",
                DirectUrl = $@"{direct_url_prefix}{i:D4}.jpg",
                Hits = i,
                Timestamp = DateTimeOffset.Now.AddMinutes(-i),
                LastAccess = DateTimeOffset.Now,
            }).ToArray(),
        };

        /// <summary>
        /// A texture store which records what it was asked for instead of loading anything, as online images cannot be
        /// fetched in the test environment.
        /// </summary>
        private partial class RecordingLargeTextureStore : LargeTextureStore
        {
            public readonly ConcurrentQueue<string> Requested = new ConcurrentQueue<string>();

            public RecordingLargeTextureStore(IRenderer renderer)
                : base(renderer)
            {
            }

            public override Texture Get(string name, WrapMode wrapModeS, WrapMode wrapModeT)
            {
                Requested.Enqueue(name);
                return null;
            }
        }
    }
}
