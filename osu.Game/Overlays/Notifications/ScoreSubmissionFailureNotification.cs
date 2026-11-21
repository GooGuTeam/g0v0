// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;

namespace osu.Game.Overlays.Notifications
{
    public partial class ScoreSubmissionFailureNotification : HeadingNotification
    {
        private readonly LocalisableString heading;
        private readonly LocalisableString reason;

        public ScoreSubmissionFailureNotification(LocalisableString heading, LocalisableString reason)
            : base(FontAwesome.Solid.Unlink)
        {
            this.heading = heading;
            this.reason = reason;

            IsCritical = true;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours) => IconContent.Colour = colours.RedDark;

        protected override LocalisableString Heading => heading;

        protected override ColourInfo GetHeadingColour(OsuColour colours) => colours.Red0;

        protected override void AddBody() => AddBodyParagraph(reason);
    }
}
