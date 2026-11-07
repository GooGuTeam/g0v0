// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Overlays.Settings.Sections.Gameplay
{
    public partial class InputSettings : SettingsSubsection
    {
        protected override LocalisableString Header => GameplaySettingsStrings.InputHeader;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                CreateItem(new SliderEntry<float>
                {
                    Caption = SkinSettingsStrings.GameplayCursorSize,
                    Current = config.GetBindable<float>(OsuSetting.GameplayCursorSize),
                    KeyboardStep = 0.01f,
                    LabelFormat = v => $"{v:0.##}x"
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = SkinSettingsStrings.AutoCursorSize,
                    Current = config.GetBindable<bool>(OsuSetting.AutoCursorSize)
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = SkinSettingsStrings.GameplayCursorDuringTouch,
                    Current = config.GetBindable<bool>(OsuSetting.GameplayCursorDuringTouch),
                    Keywords = new[] { @"touchscreen" },
                }),
            };

            if (RuntimeInfo.OS == RuntimeInfo.Platform.Windows)
            {
                Add(CreateItem(new CheckboxEntry
                {
                    Caption = GameplaySettingsStrings.DisableWinKey,
                    Current = config.GetBindable<bool>(OsuSetting.GameplayDisableWinKey)
                }));
            }
        }
    }
}
