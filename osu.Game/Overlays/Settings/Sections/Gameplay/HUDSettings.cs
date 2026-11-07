// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Overlays.Settings.Sections.Gameplay
{
    public partial class HUDSettings : SettingsSubsection
    {
        protected override LocalisableString Header => GameplaySettingsStrings.HUDHeader;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                CreateItem(new EnumDropdownEntry<HUDVisibilityMode>
                {
                    Caption = GameplaySettingsStrings.HUDVisibilityMode,
                    Current = config.GetBindable<HUDVisibilityMode>(OsuSetting.HUDVisibilityMode)
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = GameplaySettingsStrings.ShowReplaySettingsOverlay,
                    Current = config.GetBindable<bool>(OsuSetting.ReplaySettingsOverlay),
                    Keywords = new[] { "hide" },
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = GameplaySettingsStrings.AlwaysShowKeyOverlay,
                    Current = config.GetBindable<bool>(OsuSetting.KeyOverlay),
                    Keywords = new[] { "counter" },
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = GameplaySettingsStrings.AlwaysShowGameplayLeaderboard,
                    Current = config.GetBindable<bool>(OsuSetting.GameplayLeaderboard),
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = GameplaySettingsStrings.AlwaysRequireHoldForMenu,
                    Current = config.GetBindable<bool>(OsuSetting.AlwaysRequireHoldingForPause),
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = GameplaySettingsStrings.AlwaysShowHoldForMenuButton,
                    Current = config.GetBindable<bool>(OsuSetting.AlwaysShowHoldForMenuButton),
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = GameplaySettingsStrings.ShowHealthDisplayWhenCantFail,
                    Current = config.GetBindable<bool>(OsuSetting.ShowHealthDisplayWhenCantFail),
                    Keywords = new[] { "hp", "bar" },
                    ClassicDefault = false,
                }),
            };
        }
    }
}
