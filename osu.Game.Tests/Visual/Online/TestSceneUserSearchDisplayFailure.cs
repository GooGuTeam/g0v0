// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Net;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Overlays;
using osu.Game.Overlays.Dashboard.UserSearch;

namespace osu.Game.Tests.Visual.Online
{
    public partial class TestSceneUserSearchDisplayFailure : OsuTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        private DummyAPIAccess dummyAPI => (DummyAPIAccess)API;

        private UserSearchDisplay display = null!;

        private readonly List<SearchUsersRequest> requests = new List<SearchUsersRequest>();

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            // the test browser runs every test in this fixture on a single instance, so state left behind by a
            // previous test has to be cleared before each one.
            dummyAPI.HandleRequest = null;
            requests.Clear();

            Child = display = new UserSearchDisplay();
        });

        [Test]
        public void TestSearchFailureShowsPlaceholder()
        {
            installRequestHandler();

            searchFor("peppy");
            AddUntilStep("Search requested", () => requests.Count == 1);

            AddStep("Fail search", () => requests[0].TriggerFailure(new WebException(@"search failed")));

            AddUntilStep("Error placeholder shown", () => display.ChildrenOfType<OnlineErrorPlaceholder>().Any());
            AddAssert("Loading hidden", () => !display.Loading.Value);
        }

        [Test]
        public void TestSupersededSearchFailureIsIgnored()
        {
            installRequestHandler();

            searchFor("peppy");
            AddUntilStep("First search requested", () => requests.Count == 1);

            // the failure is scheduled, so changing the query within the same step supersedes it before it is applied.
            AddStep("Fail first search and change query", () =>
            {
                requests[0].TriggerFailure(new WebException(@"search failed"));
                setQuery("peppy2");
            });

            AddWaitStep("wait for the superseded failure", 5);

            AddAssert("Error placeholder not displayed", () => !display.ChildrenOfType<OnlineErrorPlaceholder>().Any());

            AddUntilStep("Second search requested", () => requests.Count == 2);
            AddAssert("Second search is for the new query", () => requests[1].Query == "peppy2");
        }

        [Test]
        public void TestClearingQueryDismissesPlaceholder()
        {
            installRequestHandler();

            searchFor("peppy");
            AddUntilStep("Search requested", () => requests.Count == 1);

            AddStep("Fail search", () => requests[0].TriggerFailure(new WebException(@"search failed")));
            AddUntilStep("Error placeholder shown", () => display.ChildrenOfType<OnlineErrorPlaceholder>().Any());

            // a failed search cannot be retried without a query, so the placeholder must not outlive the query.
            AddStep("Clear query", () => setQuery(string.Empty));
            AddUntilStep("Error placeholder hidden", () => !display.ChildrenOfType<OnlineErrorPlaceholder>().Any());
        }

        private void installRequestHandler()
            => AddStep("install request handler", () => dummyAPI.HandleRequest = request =>
            {
                if (request is not SearchUsersRequest searchRequest)
                    return false;

                // deliberately left pending, so that failures can be triggered at an exact point in time.
                requests.Add(searchRequest);
                return true;
            });

        private void searchFor(string query) => AddStep($@"search for ""{query}""", () => setQuery(query));

        private void setQuery(string query) => display.ChildrenOfType<BasicSearchTextBox>().Single().Current.Value = query;
    }
}
