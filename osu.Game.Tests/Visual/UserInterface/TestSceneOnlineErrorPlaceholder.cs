// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Net;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Overlays;

namespace osu.Game.Tests.Visual.UserInterface
{
    public partial class TestSceneOnlineErrorPlaceholder : OsuTestScene
    {
        /// <summary>
        /// The second line of the placeholder, i.e. the description below the title.
        /// </summary>
        /// <remarks>
        /// <see cref="TextFlowContainer.Text"/> is write-only, so the description is reassembled from the word sprites it
        /// was split into.
        /// </remarks>
        private string descriptionText
        {
            get
            {
                var flow = this.ChildrenOfType<OnlineErrorPlaceholder>()
                               .SingleOrDefault()?
                               .ChildrenOfType<OsuTextFlowContainer>()
                               .SingleOrDefault();

                if (flow == null)
                    return string.Empty;

                return string.Concat(flow.ChildrenOfType<SpriteText>().Select(sprite => sprite.Text));
            }
        }

        [Test]
        public void TestServerMessageShown()
        {
            AddStep("show placeholder", () => Child = new OnlineErrorPlaceholder(
                new APIException("Beatmapset not found", new WebException(), HttpStatusCode.NotFound)));

            AddUntilStep("server message shown", () => descriptionText == "Beatmapset not found");
        }

        [Test]
        public void TestGenericDescriptionWithoutServerMessage()
        {
            AddStep("show placeholder", () => Child = new OnlineErrorPlaceholder(
                new WebException("The remote server could not be reached.")));

            AddUntilStep("generic description shown", () => descriptionText == OnlineErrorStrings.LoadFailedDescription.ToString());
        }

        [Test]
        public void TestFallbackDescriptionWithoutServerMessage()
        {
            AddStep("show placeholder", () => Child = new OnlineErrorPlaceholder(
                new WebException("The remote server could not be reached."),
                OnlineErrorStrings.PageUnavailableDescription));

            AddUntilStep("fallback description shown", () => descriptionText == OnlineErrorStrings.PageUnavailableDescription.ToString());
        }

        [Test]
        public void TestEmptyServerMessageIgnored()
        {
            AddStep("show placeholder", () => Child = new OnlineErrorPlaceholder(
                new APIException(string.Empty, new WebException(), HttpStatusCode.NotFound)));

            AddUntilStep("generic description shown", () => descriptionText == OnlineErrorStrings.LoadFailedDescription.ToString());
        }
    }
}
