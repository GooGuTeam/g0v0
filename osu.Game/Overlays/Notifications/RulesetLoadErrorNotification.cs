// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
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
                string? name = getDisplayName(e);

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

        /// <summary>
        /// Get the name to display for the ruleset a load error originated from.
        /// </summary>
        /// <remarks>
        /// The assembly (i.e. the ruleset's dll file name) is preferred over the ruleset's own display name,
        /// as a ruleset which failed to load cannot necessarily be trusted to report its name correctly.
        /// </remarks>
        private static string? getDisplayName(RulesetErrorEvent e)
        {
            if (e.Assembly != null)
                return e.Assembly.GetName().Name;

            if (!string.IsNullOrEmpty(e.Location))
                return Path.GetFileNameWithoutExtension(e.Location);

            return e.RulesetInfo?.Name;
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
