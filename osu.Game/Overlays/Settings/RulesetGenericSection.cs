// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Sections.RulesetGeneric;
using osu.Game.Overlays.Settings.Entries;

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

        private readonly RulesetManagementPanel panel;

        public RulesetManagementSettings(RulesetManagementPanel panel)
        {
            this.panel = panel;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                CreateItem(new ButtonEntry
                {
                    Caption = BindingSettingsStrings.Configure,
                    Action = panel.ToggleVisibility,
                    Large = true,
                }),
            };
        }
    }
}
