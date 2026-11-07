// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Overlays.Settings.Sections.Input
{
    public partial class BindingSettings : SettingsSubsection
    {
        protected override LocalisableString Header => BindingSettingsStrings.ShortcutAndGameplayBindings;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { @"keybindings", @"controls", @"keyboard", @"keys", @"buttons" });

        private readonly KeyBindingPanel keyConfig;

        public BindingSettings(KeyBindingPanel keyConfig)
        {
            this.keyConfig = keyConfig;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                CreateItem(new ButtonEntry
                {
                    Caption = BindingSettingsStrings.Configure,
                    HintText = BindingSettingsStrings.ChangeBindingsButton,
                    Action = keyConfig.ToggleVisibility,
                    Large = true,
                }),
            };
        }
    }
}
