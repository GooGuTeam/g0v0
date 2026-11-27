// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Notifications
{
    public partial class OutageNotification : HeadingNotification
    {
        private readonly LocalisableString message;

        public OutageNotification(LocalisableString message)
            : base(FontAwesome.Solid.FireExtinguisher)
        {
            this.message = message;

            IsCritical = true;
        }

        [BackgroundDependencyLoader]
        private void load() =>
            IconContent.Colour = ColourInfo.GradientVertical(Colour4.Orange, Colour4.OrangeRed);

        protected override LocalisableString Heading => NotificationsStrings.ServerOutageInProgress;

        protected override ColourInfo GetHeadingColour(OsuColour colours) => Colour4.Orange;

        protected override void AddBody() => AddBodyParagraph(message);
    }
}
