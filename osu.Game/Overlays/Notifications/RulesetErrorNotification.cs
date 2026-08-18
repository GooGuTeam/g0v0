using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Rulesets;

namespace osu.Game.Overlays.Notifications
{
    public partial class RulesetErrorNotification : SimpleNotification
    {
        private readonly RulesetInfo rulesetInfo;
        private readonly Exception exception;

        public RulesetErrorNotification(RulesetInfo rulesetInfo, Exception exception)
        {
            this.rulesetInfo = rulesetInfo;
            this.exception = exception;

            IsCritical = true;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Icon = FontAwesome.Solid.PuzzlePiece;
            IconContent.Colour = colours.RedDark;

            TextFlow.Clear();
            TextFlow.AddText(NotificationsStrings.RulesetError.ToUpper(), s =>
            {
                s.Font = OsuFont.Style.Caption2.With(weight: FontWeight.Bold);
                s.Colour = colours.Red0;
            });

            TextFlow.AddParagraph(NotificationsStrings.RulesetException(rulesetInfo.Name), cp);
            TextFlow.AddParagraph(exception.Message, cp);
            return;

            void cp(SpriteText s) => s.Font = OsuFont.Style.Caption1;
        }
    }
}
