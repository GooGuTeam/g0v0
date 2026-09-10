using osu.Framework.Allocation;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Notifications
{
    public partial class LastRulesetCrashNotification : SimpleNotification
    {
        private readonly string cause;

        public LastRulesetCrashNotification(string cause)
        {
            this.cause = cause;
            IsCritical = true;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Icon = FontAwesome.Solid.Bomb;
            IconContent.Colour = colours.YellowDark;

            TextFlow.Clear();
            TextFlow.AddText(NotificationsStrings.ExceptionTitle.ToUpper(), s =>
            {
                s.Font = OsuFont.Style.Caption2.With(weight: FontWeight.Bold);
                s.Colour = colours.Yellow;
            });

            TextFlow.AddParagraph(NotificationsStrings.RulesetCausedCrash(cause));
            TextFlow.AddParagraph(NotificationsStrings.RulesetCrashSuggestion, cp);
            return;

            void cp(SpriteText s) => s.Font = OsuFont.Style.Caption1;
        }
    }
}
