using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Rulesets;

namespace osu.Game.Overlays.Notifications
{
    public partial class RulesetLoadErrorNotification : SimpleNotification
    {
        private readonly List<string> causes = [];
        private readonly bool hasUnknownCauses;

        public RulesetLoadErrorNotification(IEnumerable<RulesetErrorEvent> causes)
        {
            foreach (var e in causes)
            {
                // Those exceptions are thrown at runtime.
                if (e.RulesetInfo != null)
                {
                    this.causes.Add(e.RulesetInfo.Name);
                    continue;
                }

                // Most exceptions are thrown upon loading, and they don't have an associated RulesetInfo.
                string? name = e.Assembly?.GetName().Name;

                if (name != null)
                {
                    this.causes.Add(name);
                    continue;
                }

                // What could happen then? We may not know without the log's help.
                hasUnknownCauses = true;
            }

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

            if (hasUnknownCauses)
            {
                TextFlow.AddParagraph(NotificationsStrings.RulesetUnknownException, cp);
            }

            TextFlow.AddParagraph(NotificationsStrings.RulesetLoadSuggestion, cp);
            return;

            void cp(SpriteText s) => s.Font = OsuFont.Style.Caption1;
        }
    }
}
