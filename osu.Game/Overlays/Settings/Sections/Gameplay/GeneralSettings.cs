// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Rulesets.Scoring;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Overlays.Settings.Sections.Gameplay
{
    public partial class GeneralSettings : SettingsSubsection
    {
        protected override LocalisableString Header => CommonStrings.General;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                CreateItem(new EnumDropdownEntry<ScoringMode>
                {
                    Caption = GameplaySettingsStrings.ScoreDisplayMode,
                    Current = config.GetBindable<ScoringMode>(OsuSetting.ScoreDisplayMode),
                    Keywords = new[] { "scoring" },
                    ClassicDefault = ScoringMode.Classic,
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = GraphicsSettingsStrings.HitLighting,
                    Current = config.GetBindable<bool>(OsuSetting.HitLighting)
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = GameplaySettingsStrings.StarFountains,
                    Current = config.GetBindable<bool>(OsuSetting.StarFountains)
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = GameplaySettingsStrings.UseModSpecificCalculation,
                    Current = config.GetBindable<bool>(OsuSetting.UseModSpecificCalculation),
                    HintText = GameplaySettingsStrings.UseModSpecificCalculationDescription,
                    Keywords = new[] { "mod", "pp", "performance", "difficulty", "star", "relax", "algorithm" },
                }),
            };
        }
    }
}
