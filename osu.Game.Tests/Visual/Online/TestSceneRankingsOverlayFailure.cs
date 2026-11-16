// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Net;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Overlays;
using osu.Game.Overlays.Rankings;
using osu.Game.Users;

namespace osu.Game.Tests.Visual.Online
{
    /// <summary>
    /// Covers the state <see cref="RankingsOverlay"/> falls into when the rankings cannot be retrieved.
    /// </summary>
    public partial class TestSceneRankingsOverlayFailure : OsuTestScene
    {
        private RankingsOverlay rankingsOverlay = null!;

        private bool failRequests;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("setup overlay", () => Child = rankingsOverlay = new RankingsOverlay { State = { Value = Visibility.Visible } });

            AddStep("initialize dummy", () =>
            {
                failRequests = false;

                ((DummyAPIAccess)API).HandleRequest = req =>
                {
                    if (req is not GetUserRankingsRequest rankingsRequest)
                        return false;

                    if (failRequests)
                        rankingsRequest.TriggerFailure(new WebException("The remote server could not be reached."));
                    else
                        rankingsRequest.TriggerSuccess(new GetTopUsersResponse { Users = new List<UserStatistics>() });

                    return true;
                };
            });
        }

        [Test]
        public void TestLoadFailure()
        {
            AddStep("make requests fail", () => failRequests = true);
            AddStep("switch to score scope", () => rankingsOverlay.Header.Current.Value = RankingsScope.Score);

            AddUntilStep("error placeholder shown", () => rankingsOverlay.ChildrenOfType<OnlineErrorPlaceholder>().Any(p => p.IsPresent));
            AddUntilStep("loading indicator hidden", () => !rankingsOverlay.ChildrenOfType<LoadingLayer>().Single().IsPresent);

            AddStep("make requests succeed", () => failRequests = false);
            AddStep("press retry", () => rankingsOverlay.ChildrenOfType<OnlineErrorPlaceholder>()
                                                        .Single()
                                                        .ChildrenOfType<RoundedButton>()
                                                        .Single(button => button.Text.ToString() == OnlineErrorStrings.Retry.ToString())
                                                        .TriggerClick());

            AddUntilStep("error placeholder hidden", () => !rankingsOverlay.ChildrenOfType<OnlineErrorPlaceholder>().Any(p => p.IsPresent));
        }
    }
}
