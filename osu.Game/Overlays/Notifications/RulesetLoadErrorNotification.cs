using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Notifications
{
    public partial class RulesetLoadErrorNotification : SimpleNotification
    {
        private readonly IEnumerable<string> causes;

        public RulesetLoadErrorNotification(IEnumerable<string> causes)
        {
            this.causes = causes;

            IsCritical = true;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Icon = FontAwesome.Solid.PuzzlePiece;
            IconContent.Colour = colours.YellowDark;

            TextFlow.Clear();
            TextFlow.AddText(NotificationsStrings.RulesetError.ToUpper(), s =>
            {
                s.Font = OsuFont.Style.Caption2.With(weight: FontWeight.Bold);
                s.Colour = colours.Yellow;
            });

            TextFlow.AddParagraph(NotificationsStrings.RulesetUnableToLoad);
            TextFlow.NewParagraph();

            causes.ForEach(name =>
            {
                TextFlow.NewLine();
                TextFlow.AddText(name, cp);
            });

            TextFlow.AddParagraph(NotificationsStrings.RulesetLoadSuggestion, cp);
            return;

            void cp(SpriteText s) => s.Font = OsuFont.Style.Caption1;
        }
    }
}
