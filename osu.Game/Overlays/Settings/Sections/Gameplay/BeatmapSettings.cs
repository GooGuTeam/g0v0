// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Overlays.Settings.Sections.Gameplay
{
    public partial class BeatmapSettings : SettingsSubsection
    {
        protected override LocalisableString Header => GameplaySettingsStrings.BeatmapHeader;

        private readonly BindableFloat comboColourNormalisation = new BindableFloat();

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.ComboColourNormalisationAmount, comboColourNormalisation);

            Children = new Drawable[]
            {
                CreateItem(new CheckboxEntry
                {
                    Caption = SkinSettingsStrings.BeatmapSkins,
                    Current = config.GetBindable<bool>(OsuSetting.BeatmapSkins)
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = SkinSettingsStrings.BeatmapColours,
                    Current = config.GetBindable<bool>(OsuSetting.BeatmapColours),
                    Keywords = new[] { "combo", "override", "color" },
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = SkinSettingsStrings.BeatmapHitsounds,
                    Current = config.GetBindable<bool>(OsuSetting.BeatmapHitsounds),
                    Keywords = new[] { "samples", "override" },
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = GraphicsSettingsStrings.StoryboardVideo,
                    Current = config.GetBindable<bool>(OsuSetting.ShowStoryboard)
                }),
                CreateItem(new SliderEntry<float>
                {
                    Caption = GraphicsSettingsStrings.ComboColourNormalisation,
                    Current = comboColourNormalisation,
                    DisplayAsPercentage = true,
                    Keywords = new[] { "color" },
                }),
            };
        }
    }
}
