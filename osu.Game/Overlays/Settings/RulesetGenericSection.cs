using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Sections.RulesetGeneric;

namespace osu.Game.Overlays.Settings
{
    public partial class RulesetGenericSection : SettingsSection
    {
        private readonly RulesetManagementPanel? managementPanel;

        public override LocalisableString Header => RulesetSettingsStrings.Rulesets;

        public RulesetGenericSection(RulesetManagementPanel? managementPanel = null)
        {
            this.managementPanel = managementPanel;
        }

        public override Drawable CreateIcon() => new SpriteIcon
        {
            Icon = OsuIcon.Rulesets
        };

        [BackgroundDependencyLoader]
        private void load()
        {
            if (managementPanel != null)
            {
                Children = new Drawable[]
                {
                    new RulesetManagementSettings(managementPanel),
                };
            }
        }
    }

    public partial class RulesetManagementSettings : SettingsSubsection
    {
        protected override LocalisableString Header => RulesetSettingsStrings.RulesetManagement;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { @"rulesets", @"manage" });

        public RulesetManagementSettings(RulesetManagementPanel panel)
        {
            Children = new Drawable[]
            {
                new SettingsButtonV2
                {
                    Text = BindingSettingsStrings.Configure,
                    Action = panel.ToggleVisibility,
                    Height = 60
                },
            };
        }
    }
}
