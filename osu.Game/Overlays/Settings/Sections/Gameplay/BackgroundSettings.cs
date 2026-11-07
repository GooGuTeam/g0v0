// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Overlays.Settings.Sections.Gameplay
{
    public partial class BackgroundSettings : SettingsSubsection
    {
        protected override LocalisableString Header => GameplaySettingsStrings.BackgroundHeader;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                CreateItem(new SliderEntry<double>
                {
                    Caption = GameplaySettingsStrings.BackgroundDim,
                    Current = config.GetBindable<double>(OsuSetting.DimLevel),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true
                }),
                CreateItem(new SliderEntry<double>
                {
                    Caption = GameplaySettingsStrings.BackgroundBlur,
                    Current = config.GetBindable<double>(OsuSetting.BlurLevel),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = GameplaySettingsStrings.LightenDuringBreaks,
                    Current = config.GetBindable<bool>(OsuSetting.LightenDuringBreaks),
                    Keywords = new[] { "dim", "level" }
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = GameplaySettingsStrings.FadePlayfieldWhenHealthLow,
                    Current = config.GetBindable<bool>(OsuSetting.FadePlayfieldWhenHealthLow),
                }),
            };
        }
    }
}
