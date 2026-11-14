// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using Humanizer;
using NUnit.Framework;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Changelog;

namespace osu.Game.Tests.Visual.Online
{
    [TestFixture]
    public partial class TestSceneChangelogOverlay : OsuTestScene
    {
        private DummyAPIAccess dummyAPI => (DummyAPIAccess)API;

        private readonly Dictionary<string, APIUpdateStream> streams;
        private readonly Dictionary<string, APIChangelogBuild> builds;

        private APIChangelogBuild requestedBuild;
        private TestChangelogOverlay changelog;

        /// <summary>
        /// When set, the dummy API reports failed changelog listing requests.
        /// </summary>
        private bool failListingRequest;

        /// <summary>
        /// When set, the dummy API reports failed requests for the details of a single build.
        /// </summary>
        private bool failBuildRequest;

        /// <summary>
        /// The second line of the displayed error placeholder, i.e. the description below the title.
        /// </summary>
        private string errorPlaceholderDescription
        {
            get
            {
                var flow = changelog.ChildrenOfType<OnlineErrorPlaceholder>()
                                    .SingleOrDefault()?
                                    .ChildrenOfType<OsuTextFlowContainer>()
                                    .SingleOrDefault();

                if (flow == null)
                    return string.Empty;

                return string.Concat(flow.ChildrenOfType<SpriteText>().Select(sprite => sprite.Text));
            }
        }

        public TestSceneChangelogOverlay()
        {
            streams = APIUpdateStream.KNOWN_STREAMS.Keys.Select((stream, id) => new APIUpdateStream
            {
                Id = id + 1,
                Name = stream,
                DisplayName = stream.Humanize(), // not quite there, but good enough.
            }).ToDictionary(stream => stream.Name);

            string version = DateTimeOffset.Now.ToString("yyyy.Mdd.0", CultureInfo.InvariantCulture);
            builds = APIUpdateStream.KNOWN_STREAMS.Keys.Select(stream => new APIChangelogBuild
            {
                Version = version,
                DisplayVersion = version,
                UpdateStream = streams[stream],
                ChangelogEntries = new List<APIChangelogEntry>()
            }).ToDictionary(build => build.UpdateStream.Name);

            foreach (var stream in streams.Values)
                stream.LatestBuild = builds[stream.Name];
        }

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            requestedBuild = null;
            failListingRequest = false;
            failBuildRequest = false;

            dummyAPI.HandleRequest = request =>
            {
                switch (request)
                {
                    case GetChangelogRequest changelogRequest:
                        if (failListingRequest)
                        {
                            changelogRequest.TriggerFailure(new WebException("The remote server could not be reached."));
                            return true;
                        }

                        var changelogResponse = new APIChangelogIndex
                        {
                            Streams = streams.Values.ToList(),
                            Builds = builds.Values.ToList()
                        };
                        changelogRequest.TriggerSuccess(changelogResponse);
                        return true;

                    case GetChangelogBuildRequest buildRequest:
                        if (failBuildRequest)
                        {
                            buildRequest.TriggerFailure(new APIException("This build is not available", new WebException(), HttpStatusCode.BadRequest));
                            return true;
                        }

                        if (requestedBuild != null)
                            buildRequest.TriggerSuccess(requestedBuild);
                        return true;
                }

                return false;
            };

            Child = changelog = new TestChangelogOverlay();
        });

        [Test]
        public void ShowWithNoFetch()
        {
            AddStep(@"Show", () => changelog.Show());
            AddUntilStep(@"wait for streams", () => changelog.Streams?.Count > 0);
            AddAssert(@"listing displayed", () => changelog.Current.Value == null);
            AddAssert(@"no stream selected", () => changelog.Header.Streams.Current.Value == null);
        }

        [Test]
        public void ShowWithListing()
        {
            AddStep(@"Show with listing", () => changelog.ShowListing());
            AddUntilStep(@"wait for streams", () => changelog.Streams?.Count > 0);
            AddAssert(@"listing displayed", () => changelog.Current.Value == null);
            AddAssert(@"no stream selected", () => changelog.Header.Streams.Current.Value == null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ShowWithBuild(bool isSupporter)
        {
            AddStep(@"set supporter", () => dummyAPI.LocalUser.Value.IsSupporter = isSupporter);
            showBuild(() => new APIChangelogBuild
            {
                Version = "2018.712.0",
                DisplayVersion = "2018.712.0",
                UpdateStream = streams["lazer"],
                CreatedAt = new DateTime(2018, 7, 12),
                ChangelogEntries = new List<APIChangelogEntry>
                {
                    new APIChangelogEntry
                    {
                        Type = ChangelogEntryType.Fix,
                        Category = "osu!",
                        Title = "Fix thing",
                        MessageHtml = "Additional info goes here.",
                        Repository = "osu",
                        GithubPullRequestId = 11100,
                        GithubUser = new APIChangelogUser
                        {
                            OsuUsername = "smoogipoo",
                            UserId = 1040328
                        }
                    },
                    new APIChangelogEntry
                    {
                        Type = ChangelogEntryType.Add,
                        Category = "osu!",
                        Title = "Add thing",
                        Major = true,
                        Repository = "ppy/osu-framework",
                        GithubPullRequestId = 4444,
                        GithubUser = new APIChangelogUser
                        {
                            DisplayName = "frenzibyte",
                            GithubUrl = "https://github.com/frenzibyte"
                        }
                    },
                    new APIChangelogEntry
                    {
                        Type = ChangelogEntryType.Misc,
                        Category = "Code quality",
                        Title = "Clean up thing",
                        GithubUser = new APIChangelogUser
                        {
                            DisplayName = "some dude"
                        }
                    },
                    new APIChangelogEntry
                    {
                        Type = ChangelogEntryType.Misc,
                        Category = "Code quality",
                        Title = "Clean up another thing"
                    },
                    new APIChangelogEntry
                    {
                        Type = ChangelogEntryType.Add,
                        Category = "osu!",
                        Title = "Add entry with news url",
                        Url = "https://osu.ppy.sh/home/news/2023-07-27-summer-splash"
                    },
                }
            });

            AddUntilStep(@"wait for streams", () => changelog.Streams?.Count > 0);
            AddAssert(@"correct build displayed", () => changelog.Current.Value.Version == "2018.712.0");
            AddAssert(@"correct stream selected", () => changelog.Header.Streams.Current.Value.Id == 5);
            AddUntilStep(@"wait for content load", () => changelog.ChildrenOfType<ChangelogSupporterPromo>().Any());
            AddAssert(@"supporter promo showed", () => changelog.ChildrenOfType<ChangelogSupporterPromo>().First().Alpha == (isSupporter ? 0 : 1));
        }

        [Test]
        public void TestHTMLUnescaping()
        {
            showBuild(() => new APIChangelogBuild
            {
                Version = "2019.920.0",
                DisplayVersion = "2019.920.0",
                CreatedAt = new DateTime(2019, 9, 20),
                UpdateStream = new APIUpdateStream
                {
                    Name = "Test",
                    DisplayName = "Test"
                },
                ChangelogEntries = new List<APIChangelogEntry>
                {
                    new APIChangelogEntry
                    {
                        Category = "Testing HTML strings unescaping",
                        Title = "Ensuring HTML strings are being unescaped",
                        MessageHtml = "&quot;&quot;&quot;This text should appear triple-quoted&quot;&quot;&quot;    &gt;_&lt;",
                        GithubUser = new APIChangelogUser
                        {
                            DisplayName = "Dummy",
                            OsuUsername = "Dummy",
                        }
                    },
                }
            });
        }

        [Test]
        public void TestMarkdown()
        {
            showBuild(() => new APIChangelogBuild
            {
                Version = "2026.620.0",
                DisplayVersion = "2026.620.0",
                CreatedAt = new DateTime(2026, 6, 20),
                UpdateStream = new APIUpdateStream
                {
                    Name = "Test",
                    DisplayName = "Test"
                },
                ChangelogEntries = new List<APIChangelogEntry>
                {
                    new APIChangelogEntry
                    {
                        Category = "Markdown",
                        Title = "Testing markdown container in changelog",
                        Message = @"This paragraph [has a link](https://osu.ppy.sh) to osu! website.

![](https://i.ppy.sh/e8b1f3cafa284aa347dd96b3977ceedd0e42d3c0/68747470733a2f2f6f73752e7070792e73682f77696b692f696d616765732f7368617265642f6e6577732f323032362d30362d30332d6d6f642d6d756c7469706c696572732d7375727665792d726573756c74732f6d6f642d6d756c7469706c696572732e706e673f32)

There is mod multiplayer image above.

<video width=""80%"" controls><source src=""https://github.com/user-attachments/assets/2a511e0d-51f8-4abf-a3ab-de0992618b6b"" type=""video/mp4"" preload=""none""></video>

Paragraph above only contains html video and should not be rendered."
                    }
                }
            });
        }

        [Test]
        public void TestListingFailure()
        {
            AddStep("make listing request fail", () => failListingRequest = true);
            AddStep("show listing", () => changelog.ShowListing());

            AddUntilStep("error placeholder shown", () => changelog.ChildrenOfType<OnlineErrorPlaceholder>().SingleOrDefault()?.IsPresent == true);

            AddStep("make listing request succeed", () => failListingRequest = false);
            AddStep("press retry", () => changelog.ChildrenOfType<OnlineErrorPlaceholder>()
                                                  .Single()
                                                  .ChildrenOfType<RoundedButton>()
                                                  .Single(button => button.Text.ToString() == OnlineErrorStrings.Retry.ToString())
                                                  .TriggerClick());

            AddUntilStep("listing loaded", () => changelog.Streams?.Count > 0);
            AddUntilStep("error placeholder hidden", () => !changelog.ChildrenOfType<OnlineErrorPlaceholder>().Any(d => d.IsPresent));
        }

        [Test]
        public void TestBuildFailureShowsServerMessage()
        {
            AddStep("make build request fail with a server message", () => failBuildRequest = true);

            showBuild(() => builds[APIUpdateStream.KNOWN_STREAMS.Keys.First()]);

            AddUntilStep("error placeholder shown", () => changelog.ChildrenOfType<OnlineErrorPlaceholder>().SingleOrDefault()?.IsPresent == true);
            AddUntilStep("server message shown", () => errorPlaceholderDescription == "This build is not available");
        }

        private void showBuild(Func<APIChangelogBuild> build)
        {
            AddStep("set up build", () => requestedBuild = build.Invoke());
            AddStep("show build", () => changelog.ShowBuild(requestedBuild));
        }

        private partial class TestChangelogOverlay : ChangelogOverlay
        {
            public new List<APIUpdateStream> Streams => base.Streams;

            public new ChangelogHeader Header => base.Header;
        }
    }
}
