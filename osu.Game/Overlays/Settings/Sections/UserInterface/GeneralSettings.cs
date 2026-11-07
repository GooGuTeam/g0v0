// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Overlays.Settings.Sections.UserInterface
{
    public partial class GeneralSettings : SettingsSubsection
    {
        protected override LocalisableString Header => CommonStrings.General;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                CreateItem(new CheckboxEntry
                {
                    Caption = UserInterfaceStrings.CursorRotation,
                    Current = config.GetBindable<bool>(OsuSetting.CursorRotation),
                    Keywords = [@"spin"],
                }),
                CreateItem(new SliderEntry<float>
                {
                    Caption = UserInterfaceStrings.MenuCursorSize,
                    Current = config.GetBindable<float>(OsuSetting.MenuCursorSize),
                    KeyboardStep = 0.01f,
                    LabelFormat = v => $"{v:0.##}x"
                }),
                CreateItem(new SliderEntry<float>
                {
                    Caption = UserInterfaceStrings.Parallax,
                    Current = config.GetBindable<float>(OsuSetting.MenuParallaxScale),
                    DisplayAsPercentage = true,
                    LabelFormat = v => v == 0 ? CommonStrings.Disabled : FormSliderBar<float>.DefaultLabelFormat(v, true),
                }),
                CreateItem(new SliderEntry<double>
                {
                    Caption = UserInterfaceStrings.HoldToConfirmActivationTime,
                    Current = config.GetBindable<double>(OsuSetting.UIHoldActivationDelay),
                    KeyboardStep = 50,
                    LabelFormat = v => $"{v:N0} ms",
                    Keywords = [@"delay"],
                    ClassicDefault = 0,
                }),
            };
        }
    }
}
