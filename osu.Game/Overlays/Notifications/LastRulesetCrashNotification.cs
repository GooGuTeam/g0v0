// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Notifications
{
    public partial class LastRulesetCrashNotification : HeadingNotification
    {
        private readonly string cause;

        public LastRulesetCrashNotification(string cause)
            : base(FontAwesome.Solid.Bomb)
        {
            this.cause = cause;
            IsCritical = true;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours) => IconContent.Colour = colours.YellowDark;

        protected override LocalisableString Heading => NotificationsStrings.ExceptionTitle;

        protected override ColourInfo GetHeadingColour(OsuColour colours) => colours.Yellow;

        protected override void AddBody()
        {
            AddBodyParagraph(NotificationsStrings.RulesetCausedCrash(cause));
            AddBodyParagraph(NotificationsStrings.RulesetCrashSuggestion);
        }
    }
}
