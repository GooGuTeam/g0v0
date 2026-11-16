// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.BeatmapListing;
using osu.Game.Scoring;
using osuTK.Input;
using APIUser = osu.Game.Online.API.Requests.Responses.APIUser;

namespace osu.Game.Tests.Visual.Online
{
    public partial class TestSceneBeatmapListingOverlay : OsuManualInputManagerTestScene
    {
        private readonly List<APIBeatmapSet> setsForResponse = new List<APIBeatmapSet>();

        private BeatmapListingOverlay overlay;

        private BeatmapListingSearchControl searchControl => overlay.ChildrenOfType<BeatmapListingSearchControl>().Single();

        private OsuConfigManager localConfig;

        private bool returnCursorOnResponse;

        /// <summary>
        /// The second line of the displayed error placeholder, i.e. the description below the title.
        /// </summary>
        private string errorPlaceholderDescription
        {
            get
            {
                var flow = overlay.ChildrenOfType<OnlineErrorPlaceholder>()
                                  .SingleOrDefault()?
                                  .ChildrenOfType<OsuTextFlowContainer>()
                                  .SingleOrDefault();

                if (flow == null)
                    return string.Empty;

                return string.Concat(flow.ChildrenOfType<SpriteText>().Select(sprite => sprite.Text));
            }
        }

        /// <summary>
        /// When set, the dummy API reports failed search requests instead of returning <see cref="setsForResponse"/>.
        /// </summary>
        private bool failSearchRequests;

        /// <summary>
        /// When set, failed search requests report this as the message returned by the server.
        /// </summary>
        private string serverErrorMessage;

        [BackgroundDependencyLoader]
        private void load()
        {
            Dependencies.Cache(localConfig = new OsuConfigManager(LocalStorage));
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("setup overlay", () =>
            {
                Child = overlay = new BeatmapListingOverlay { State = { Value = Visibility.Visible } };
                setsForResponse.Clear();
                failSearchRequests = false;
                serverErrorMessage = null;
            });

            AddStep("initialize dummy", () =>
            {
                var api = (DummyAPIAccess)API;

                api.HandleRequest = req =>
                {
                    if (!(req is SearchBeatmapSetsRequest searchBeatmapSetsRequest)) return false;

                    if (failSearchRequests)
                    {
                        searchBeatmapSetsRequest.TriggerFailure(serverErrorMessage == null
                            ? new WebException("The remote server could not be reached.")
                            : new APIException(serverErrorMessage, new WebException(), HttpStatusCode.BadRequest));

                        return true;
                    }

                    searchBeatmapSetsRequest.TriggerSuccess(new SearchBeatmapSetsResponse
                    {
                        BeatmapSets = setsForResponse,
                        Cursor = returnCursorOnResponse ? new Cursor() : null,
                    });

                    return true;
                };

                // non-supporter user
                api.LocalUser.Value = new APIUser
                {
                    Username = "TestBot",
                    Id = API.LocalUser.Value.Id + 1,
                };
            });

            AddStep("reset size", () => localConfig.SetValue(OsuSetting.BeatmapListingCardSize, BeatmapCardSize.Normal));
        }

        [Test]
        public void TestHideViaBack()
        {
            AddAssert("is visible", () => overlay.State.Value == Visibility.Visible);
            AddStep("hide", () => InputManager.Key(Key.Escape));
            AddUntilStep("is hidden", () => overlay.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestHideViaBackWithSearch()
        {
            AddAssert("is visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("search something", () => overlay.ChildrenOfType<SearchTextBox>().First().Text = "search");

            AddStep("kill search", () => InputManager.Key(Key.Escape));

            AddAssert("search textbox empty", () => string.IsNullOrEmpty(overlay.ChildrenOfType<SearchTextBox>().First().Text));
            AddAssert("is visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("hide", () => InputManager.Key(Key.Escape));
            AddUntilStep("is hidden", () => overlay.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestHideViaBackWithScrolledSearch()
        {
            AddAssert("is visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("show many results", () => fetchFor(getManyBeatmaps(100).ToArray()));

            AddUntilStep("placeholder hidden", () => !overlay.ChildrenOfType<BeatmapListingOverlay.NotFoundDrawable>().Any(d => d.IsPresent));

            AddStep("scroll to bottom", () => overlay.ChildrenOfType<OverlayScrollContainer>().First().ScrollToEnd());

            AddStep("kill search", () => InputManager.Key(Key.Escape));

            AddUntilStep("search textbox empty", () => string.IsNullOrEmpty(overlay.ChildrenOfType<SearchTextBox>().First().Text));
            AddUntilStep("is scrolled to top", () => overlay.ChildrenOfType<OverlayScrollContainer>().First().Current == 0);
            AddAssert("is visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("hide", () => InputManager.Key(Key.Escape));
            AddUntilStep("is hidden", () => overlay.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestCorrectOldContentExpiration()
        {
            AddAssert("is visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("show many results", () => fetchFor(getManyBeatmaps(100).ToArray()));
            assertAllCardsOfType<BeatmapCardNormal>(100);

            AddStep("show more results", () => fetchFor(getManyBeatmaps(30).ToArray()));
            assertAllCardsOfType<BeatmapCardNormal>(30);
        }

        [Test]
        public void TestCardSizeSwitching([Values] bool viaConfig)
        {
            AddAssert("is visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("show many results", () => fetchFor(getManyBeatmaps(100).ToArray()));
            assertAllCardsOfType<BeatmapCardNormal>(100);

            setCardSize(BeatmapCardSize.Extra, viaConfig);
            assertAllCardsOfType<BeatmapCardExtra>(100);

            setCardSize(BeatmapCardSize.Normal, viaConfig);
            assertAllCardsOfType<BeatmapCardNormal>(100);

            AddStep("fetch for 0 beatmaps", () => fetchFor());
            AddUntilStep("placeholder shown", () => overlay.ChildrenOfType<BeatmapListingOverlay.NotFoundDrawable>().SingleOrDefault()?.IsPresent == true);

            setCardSize(BeatmapCardSize.Extra, viaConfig);
            AddAssert("placeholder shown", () => overlay.ChildrenOfType<BeatmapListingOverlay.NotFoundDrawable>().SingleOrDefault()?.IsPresent == true);
        }

        [Test]
        public void TestNoBeatmapsPlaceholder()
        {
            AddStep("fetch for 0 beatmaps", () => fetchFor());
            placeholderShown();

            AddStep("show many results", () => fetchFor(getManyBeatmaps(100).ToArray()));
            AddUntilStep("wait for loaded", () => this.ChildrenOfType<BeatmapCard>().Count() == 100);
            AddUntilStep("placeholder hidden", () => !overlay.ChildrenOfType<BeatmapListingOverlay.NotFoundDrawable>().Any(d => d.IsPresent));

            AddStep("fetch for 0 beatmaps", () => fetchFor());
            placeholderShown();

            // fetch once more to ensure nothing happens in displaying placeholder again when it already is present.
            AddStep("fetch for 0 beatmaps again", () => fetchFor());
            placeholderShown();

            void placeholderShown() =>
                AddUntilStep("placeholder shown", () =>
                {
                    var notFoundDrawable = overlay.ChildrenOfType<BeatmapListingOverlay.NotFoundDrawable>().SingleOrDefault();
                    return notFoundDrawable != null && notFoundDrawable.IsPresent && notFoundDrawable.Parent!.DrawHeight > 0;
                });
        }

        [Test]
        public void TestSearchFailure()
        {
            AddStep("make search requests fail", () => failSearchRequests = true);
            AddStep("search", () => searchControl.Query.Value = "failing search");

            AddUntilStep("error placeholder shown", () => overlay.ChildrenOfType<OnlineErrorPlaceholder>().SingleOrDefault()?.IsPresent == true);
            AddAssert("loading indicator hidden", () => !overlay.ChildrenOfType<LoadingLayer>().Single().IsPresent);

            AddStep("make search requests succeed", () =>
            {
                failSearchRequests = false;
                setSearchResponse(getManyBeatmaps(3), false);
            });

            AddStep("press retry", () => overlay.ChildrenOfType<OnlineErrorPlaceholder>()
                                                .Single()
                                                .ChildrenOfType<RoundedButton>()
                                                .Single(button => button.Text.ToString() == OnlineErrorStrings.Retry.ToString())
                                                .TriggerClick());

            AddUntilStep("results shown", () => this.ChildrenOfType<BeatmapCard>().Count(card => card.IsLoaded) == 3);
            AddUntilStep("error placeholder hidden", () => !overlay.ChildrenOfType<OnlineErrorPlaceholder>().Any(d => d.IsPresent));
        }

        [Test]
        public void TestSearchFailureShowsServerMessage()
        {
            AddStep("make search requests fail with a server message", () =>
            {
                serverErrorMessage = "You have reached the maximum attempts for this room";
                failSearchRequests = true;
            });

            AddStep("search", () => searchControl.Query.Value = "failing search");

            AddUntilStep("error placeholder shown", () => overlay.ChildrenOfType<OnlineErrorPlaceholder>().SingleOrDefault()?.IsPresent == true);
            AddUntilStep("server message shown", () => errorPlaceholderDescription == "You have reached the maximum attempts for this room");
        }

        [Test]
        public void TestPageFailureKeepsResultsAndCanBeRetried()
        {
            AddStep("show many results", () => fetchFor(getManyBeatmaps(50).ToArray(), true));
            AddUntilStep("wait for loaded", () => this.ChildrenOfType<BeatmapCard>().Count(card => card.IsLoaded) == 50);

            AddStep("make next page fail", () => failSearchRequests = true);
            AddStep("scroll to end", () => overlay.ChildrenOfType<OverlayScrollContainer>().Single().ScrollToEnd());

            AddUntilStep("error placeholder shown", () => overlay.ChildrenOfType<OnlineErrorPlaceholder>().SingleOrDefault()?.IsPresent == true);
            AddAssert("results kept", () => this.ChildrenOfType<BeatmapCard>().Count(card => card.IsLoaded) == 50);

            AddStep("make requests succeed with another page", () =>
            {
                failSearchRequests = false;
                setSearchResponse(getManyBeatmaps(20), false);
            });

            AddStep("press retry", () => overlay.ChildrenOfType<OnlineErrorPlaceholder>()
                                                .Single()
                                                .ChildrenOfType<RoundedButton>()
                                                .Single(button => button.Text.ToString() == OnlineErrorStrings.Retry.ToString())
                                                .TriggerClick());

            AddUntilStep("more results shown", () => this.ChildrenOfType<BeatmapCard>().Count(card => card.IsLoaded) == 70);
            AddUntilStep("error placeholder hidden", () => !overlay.ChildrenOfType<OnlineErrorPlaceholder>().Any(d => d.IsPresent));
        }

        /// <summary>
        /// During pagination, the first beatmap of the second page may be a duplicate of the last beatmap from the previous page.
        /// This is currently the case with osu!web API due to ES relevance score's presence in the response cursor.
        /// See: https://github.com/ppy/osu-web/issues/9270
        /// </summary>
        [Test]
        public void TestDuplicatedBeatmapOnlyShowsOnce()
        {
            APIBeatmapSet beatmapSet = null;

            AddStep("show many results", () =>
            {
                beatmapSet = CreateAPIBeatmapSet(Ruleset.Value);
                beatmapSet.Title = "last beatmap of first page";

                fetchFor(getManyBeatmaps(49).Append(new APIBeatmapSet { Title = "last beatmap of first page", OnlineID = beatmapSet.OnlineID }).ToArray(), true);
            });
            AddUntilStep("wait for loaded", () => this.ChildrenOfType<BeatmapCard>().Count() == 50);

            AddStep("set next page", () => setSearchResponse(getManyBeatmaps(49).Prepend(new APIBeatmapSet { Title = "this shouldn't show up", OnlineID = beatmapSet.OnlineID }).ToArray(), false));
            AddStep("scroll to end", () => overlay.ChildrenOfType<OverlayScrollContainer>().Single().ScrollToEnd());
            AddUntilStep("wait for loaded", () => this.ChildrenOfType<BeatmapCard>().Count() >= 99);

            AddAssert("beatmap not duplicated", () => overlay.ChildrenOfType<BeatmapCard>().Count(c => c.BeatmapSet.Equals(beatmapSet)) == 1);
        }

        [Test]
        public void TestUserWithoutSupporterUsesSupporterOnlyFiltersWithoutResults()
        {
            AddStep("fetch for 0 beatmaps", () => fetchFor());

            AddStep("set dummy as non-supporter", () => ((DummyAPIAccess)API).LocalUser.Value.IsSupporter = false);

            // only Rank Achieved filter
            setRankAchievedFilter(new[] { ScoreRank.XH });
            supporterRequiredPlaceholderShown();

            setRankAchievedFilter(Array.Empty<ScoreRank>());
            notFoundPlaceholderShown();

            // only Played filter
            setPlayedFilter(SearchPlayed.Played);
            supporterRequiredPlaceholderShown();

            setPlayedFilter(SearchPlayed.Any);
            notFoundPlaceholderShown();

            // both RankAchieved and Played filters
            setRankAchievedFilter(new[] { ScoreRank.XH });
            setPlayedFilter(SearchPlayed.Played);
            supporterRequiredPlaceholderShown();

            setRankAchievedFilter(Array.Empty<ScoreRank>());
            setPlayedFilter(SearchPlayed.Any);
            notFoundPlaceholderShown();
        }

        [Test]
        public void TestUserWithSupporterUsesSupporterOnlyFiltersWithoutResults()
        {
            AddStep("fetch for 0 beatmaps", () => fetchFor());
            AddStep("set dummy as supporter", () => ((DummyAPIAccess)API).LocalUser.Value.IsSupporter = true);

            // only Rank Achieved filter
            setRankAchievedFilter(new[] { ScoreRank.XH });
            notFoundPlaceholderShown();

            setRankAchievedFilter(Array.Empty<ScoreRank>());
            notFoundPlaceholderShown();

            // only Played filter
            setPlayedFilter(SearchPlayed.Played);
            notFoundPlaceholderShown();

            setPlayedFilter(SearchPlayed.Any);
            notFoundPlaceholderShown();

            // both Rank Achieved and Played filters
            setRankAchievedFilter(new[] { ScoreRank.XH });
            setPlayedFilter(SearchPlayed.Played);
            notFoundPlaceholderShown();

            setRankAchievedFilter(Array.Empty<ScoreRank>());
            setPlayedFilter(SearchPlayed.Any);
            notFoundPlaceholderShown();
        }

        [Test]
        public void TestUserWithoutSupporterUsesSupporterOnlyFiltersWithResults()
        {
            AddStep("fetch for 1 beatmap", () => fetchFor(CreateAPIBeatmapSet(Ruleset.Value)));

            noPlaceholderShown();

            AddStep("set dummy as non-supporter", () => ((DummyAPIAccess)API).LocalUser.Value.IsSupporter = false);

            // only Rank Achieved filter
            setRankAchievedFilter(new[] { ScoreRank.XH });
            supporterRequiredPlaceholderShown();

            setRankAchievedFilter(Array.Empty<ScoreRank>());
            noPlaceholderShown();

            // only Played filter
            setPlayedFilter(SearchPlayed.Played);
            supporterRequiredPlaceholderShown();

            setPlayedFilter(SearchPlayed.Any);
            noPlaceholderShown();

            // both Rank Achieved and Played filters
            setRankAchievedFilter(new[] { ScoreRank.XH });
            setPlayedFilter(SearchPlayed.Played);
            supporterRequiredPlaceholderShown();

            setRankAchievedFilter(Array.Empty<ScoreRank>());
            setPlayedFilter(SearchPlayed.Any);
            noPlaceholderShown();
        }

        [Test]
        public void TestUserWithSupporterUsesSupporterOnlyFiltersWithResults()
        {
            AddStep("fetch for 1 beatmap", () => fetchFor(CreateAPIBeatmapSet(Ruleset.Value)));

            noPlaceholderShown();

            AddStep("set dummy as supporter", () => ((DummyAPIAccess)API).LocalUser.Value.IsSupporter = true);

            // only Rank Achieved filter
            setRankAchievedFilter(new[] { ScoreRank.XH });
            noPlaceholderShown();

            setRankAchievedFilter(Array.Empty<ScoreRank>());
            noPlaceholderShown();

            // only Played filter
            setPlayedFilter(SearchPlayed.Played);
            noPlaceholderShown();

            setPlayedFilter(SearchPlayed.Any);
            noPlaceholderShown();

            // both Rank Achieved and Played filters
            setRankAchievedFilter(new[] { ScoreRank.XH });
            setPlayedFilter(SearchPlayed.Played);
            noPlaceholderShown();

            setRankAchievedFilter(Array.Empty<ScoreRank>());
            setPlayedFilter(SearchPlayed.Any);
            noPlaceholderShown();
        }

        [Test]
        public void TestExpandedCardContentNotClipped()
        {
            AddAssert("is visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("show result with many difficulties", () =>
            {
                var beatmapSet = CreateAPIBeatmapSet(Ruleset.Value);
                beatmapSet.Beatmaps = Enumerable.Repeat(beatmapSet.Beatmaps.First(), 100).ToArray();
                fetchFor(beatmapSet);
            });
            assertAllCardsOfType<BeatmapCardNormal>(1);

            AddStep("hover extra info row", () =>
            {
                var difficultyArea = this.ChildrenOfType<BeatmapCardExtraInfoRow>().Single();
                InputManager.MoveMouseTo(difficultyArea);
            });
            AddUntilStep("wait for expanded", () => this.ChildrenOfType<BeatmapCardNormal>().Single().Expanded.Value);
            AddAssert("expanded content not clipped", () =>
            {
                var cardContainer = this.ChildrenOfType<ReverseChildIDFillFlowContainer<BeatmapCard>>().Single().Parent;
                var expandedContent = this.ChildrenOfType<ExpandedContentScrollContainer>().Single();
                return expandedContent.ScreenSpaceDrawQuad.GetVertices().ToArray().All(v => cardContainer!.ScreenSpaceDrawQuad.Contains(v));
            });
        }

        private static int searchCount;

        private APIBeatmapSet[] getManyBeatmaps(int count) => Enumerable.Range(0, count).Select(_ => CreateAPIBeatmapSet(Ruleset.Value)).ToArray();

        private void fetchFor(params APIBeatmapSet[] beatmaps) => fetchFor(beatmaps, false);

        private void fetchFor(APIBeatmapSet[] beatmaps, bool hasNextPage)
        {
            setSearchResponse(beatmaps, hasNextPage);

            // trigger arbitrary change for fetching.
            searchControl.Query.Value = $"search {searchCount++}";
        }

        private void setSearchResponse(APIBeatmapSet[] beatmaps, bool hasNextPage)
        {
            setsForResponse.Clear();
            setsForResponse.AddRange(beatmaps);
            returnCursorOnResponse = hasNextPage;
        }

        private void setRankAchievedFilter(ScoreRank[] ranks)
        {
            AddStep($"set Rank Achieved filter to [{string.Join(',', ranks)}]", () =>
            {
                searchControl.Ranks.Clear();
                searchControl.Ranks.AddRange(ranks);
            });
        }

        private void setPlayedFilter(SearchPlayed played)
        {
            AddStep($"set Played filter to {played}", () => searchControl.Played.Value = played);
        }

        private void supporterRequiredPlaceholderShown()
        {
            AddUntilStep("\"supporter required\" placeholder shown", () => overlay.ChildrenOfType<BeatmapListingOverlay.SupporterRequiredDrawable>().SingleOrDefault()?.IsPresent == true);
        }

        private void notFoundPlaceholderShown()
        {
            AddUntilStep("\"no maps found\" placeholder shown", () => overlay.ChildrenOfType<BeatmapListingOverlay.NotFoundDrawable>().SingleOrDefault()?.IsPresent == true);
        }

        private void noPlaceholderShown()
        {
            AddUntilStep("\"supporter required\" placeholder not shown", () => !overlay.ChildrenOfType<BeatmapListingOverlay.SupporterRequiredDrawable>().Any(d => d.IsPresent));
            AddUntilStep("\"no maps found\" placeholder not shown", () => !overlay.ChildrenOfType<BeatmapListingOverlay.NotFoundDrawable>().Any(d => d.IsPresent));
        }

        private void setCardSize(BeatmapCardSize cardSize, bool viaConfig) => AddStep($"set card size to {cardSize}", () =>
        {
            if (viaConfig)
                localConfig.SetValue(OsuSetting.BeatmapListingCardSize, cardSize);
            else
                overlay.ChildrenOfType<BeatmapListingCardSizeTabControl>().Single().Current.Value = cardSize;
        });

        private void assertAllCardsOfType<T>(int expectedCount)
            where T : BeatmapCard =>
            AddUntilStep($"all loaded beatmap cards are {typeof(T)}", () =>
            {
                int loadedCorrectCount = this.ChildrenOfType<BeatmapCard>().Count(card => card.IsLoaded && card.GetType() == typeof(T));
                return loadedCorrectCount > 0 && loadedCorrectCount == expectedCount;
            });

        protected override void Dispose(bool isDisposing)
        {
            localConfig?.Dispose();
            base.Dispose(isDisposing);
        }
    }
}
