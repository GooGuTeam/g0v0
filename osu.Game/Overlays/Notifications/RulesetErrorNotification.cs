// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Rulesets;

namespace osu.Game.Overlays.Notifications
{
    public partial class RulesetErrorNotification : HeadingNotification
    {
        private readonly RulesetInfo rulesetInfo;
        private readonly Exception exception;

        public RulesetErrorNotification(RulesetInfo rulesetInfo, Exception exception)
            : base(FontAwesome.Solid.PuzzlePiece)
        {
            this.rulesetInfo = rulesetInfo;
            this.exception = exception;

            IsCritical = true;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours) => IconContent.Colour = colours.RedDark;

        protected override LocalisableString Heading => NotificationsStrings.RulesetError;

        protected override ColourInfo GetHeadingColour(OsuColour colours) => colours.Red0;

        protected override void AddBody()
        {
            AddBodyParagraph(NotificationsStrings.RulesetException(rulesetInfo.Name));
            AddBodyParagraph(exception.Message);
        }
    }
}
