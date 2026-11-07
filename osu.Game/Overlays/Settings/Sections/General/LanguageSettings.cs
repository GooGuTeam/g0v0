// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Overlays.Settings.Sections.General
{
    public partial class LanguageSettings : SettingsSubsection
    {
        protected override LocalisableString Header => GeneralSettingsStrings.LanguageHeader;

        [BackgroundDependencyLoader]
        private void load(OsuGameBase game, OsuConfigManager config, FrameworkConfigManager frameworkConfig)
        {
            Children = new Drawable[]
            {
                CreateItem(new EnumDropdownEntry<Language>
                {
                    Caption = GeneralSettingsStrings.LanguageDropdown,
                    Current = game.CurrentLanguage,
                    AlwaysShowSearchBar = true,
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = GeneralSettingsStrings.PreferOriginalMetadataLanguage,
                    Current = frameworkConfig.GetBindable<bool>(FrameworkSetting.ShowUnicode)
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = GeneralSettingsStrings.Prefer24HourTimeDisplay,
                    Current = config.GetBindable<bool>(OsuSetting.Prefer24HourTime)
                }),
            };
        }
    }
}
