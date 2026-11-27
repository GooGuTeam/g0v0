// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Rulesets;

namespace osu.Game.Overlays.Notifications
{
    public partial class RulesetLoadErrorNotification : HeadingNotification
    {
        private readonly List<string> causes = [];
        private readonly bool hasUnknownCauses;

        public RulesetLoadErrorNotification(IEnumerable<RulesetErrorEvent> causes)
            : base(FontAwesome.Solid.PuzzlePiece)
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
        private void load(OsuColour colours) => IconContent.Colour = colours.YellowDark;

        protected override LocalisableString Heading => NotificationsStrings.RulesetError;

        protected override ColourInfo GetHeadingColour(OsuColour colours) => colours.Yellow;

        protected override void AddBody()
        {
            AddBodyParagraph(NotificationsStrings.RulesetUnableToLoad);
            TextFlow.NewParagraph();

            causes.ForEach(name =>
            {
                TextFlow.NewLine();
                TextFlow.AddText(name, s => s.Font = OsuFont.Style.Caption1);
            });

            if (hasUnknownCauses)
                AddBodyParagraph(NotificationsStrings.RulesetUnknownException);

            AddBodyParagraph(NotificationsStrings.RulesetLoadSuggestion);
        }
    }
}
