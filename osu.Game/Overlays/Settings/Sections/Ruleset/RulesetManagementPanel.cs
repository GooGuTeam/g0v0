using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Ruleset
{
    public partial class RulesetManagementPanel : SettingsSubPanel
    {
        protected override Drawable CreateHeader()
            => new SettingsHeader(RulesetSettingsStrings.RulesetManagement, RulesetSettingsStrings.RulesetManagementSubheading);

        [BackgroundDependencyLoader]
        private void load()
        {
            AddSection(new LocalRulesetsSection());
        }
    }
}
