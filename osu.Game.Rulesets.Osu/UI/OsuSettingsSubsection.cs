// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.UI;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Rulesets.Osu.UI
{
    public partial class OsuSettingsSubsection : RulesetSettingsSubsection
    {
        public OsuSettingsSubsection(Ruleset ruleset)
            : base(ruleset)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var config = (OsuRulesetConfigManager)Config;

            Children = new Drawable[]
            {
                CreateItem(new CheckboxEntry
                {
                    Caption = RulesetSettingsStrings.SnakingInSliders,
                    Current = config.GetBindable<bool>(OsuRulesetSetting.SnakingInSliders)
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = RulesetSettingsStrings.SnakingOutSliders,
                    Current = config.GetBindable<bool>(OsuRulesetSetting.SnakingOutSliders),
                    ClassicDefault = false,
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = RulesetSettingsStrings.HitAnimations,
                    HintText = RulesetSettingsStrings.HitAnimationsOsuTooltip,
                    Current = config.GetBindable<bool>(OsuRulesetSetting.HitAnimations)
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = RulesetSettingsStrings.CursorTrail,
                    Current = config.GetBindable<bool>(OsuRulesetSetting.ShowCursorTrail)
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = RulesetSettingsStrings.CursorRipples,
                    Current = config.GetBindable<bool>(OsuRulesetSetting.ShowCursorRipples)
                }),
                CreateItem(new EnumDropdownEntry<PlayfieldBorderStyle>
                {
                    Caption = RulesetSettingsStrings.PlayfieldBorderStyle,
                    Current = config.GetBindable<PlayfieldBorderStyle>(OsuRulesetSetting.PlayfieldBorderStyle),
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = RulesetSettingsStrings.ShowSliderHeadJudgements,
                    HintText = RulesetSettingsStrings.ShowSliderHeadJudgementsDescription,
                    Current = config.GetBindable<bool>(OsuRulesetSetting.ShowSliderHeadJudgements),
                }),
            };
        }
    }
}
