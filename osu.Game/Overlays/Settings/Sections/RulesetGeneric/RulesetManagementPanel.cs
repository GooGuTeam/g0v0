// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.RulesetGeneric
{
    public partial class RulesetManagementPanel : SettingsSubPanel
    {
        protected override Drawable CreateHeader()
            => new SettingsHeader(RulesetSettingsStrings.RulesetManagement, RulesetSettingsStrings.RulesetManagementSubheading);

        [BackgroundDependencyLoader]
        private void load()
        {
            AddSection(new LocalRulesetsSection());
            AddSection(new OnlineRulesetsSection());
        }
    }
}
