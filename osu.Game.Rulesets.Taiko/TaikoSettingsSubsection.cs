// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Taiko.Configuration;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Rulesets.Taiko
{
    public partial class TaikoSettingsSubsection : RulesetSettingsSubsection
    {
        public TaikoSettingsSubsection(TaikoRuleset ruleset)
            : base(ruleset)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var config = (TaikoRulesetConfigManager)Config;

            CheckboxEntry rateAdjustedAnimations;
            CheckboxEntry hitAnimations;

            Children = new Drawable[]
            {
                CreateItem(new EnumDropdownEntry<TaikoTouchControlScheme>
                {
                    Caption = RulesetSettingsStrings.TouchControlScheme,
                    Current = config.GetBindable<TaikoTouchControlScheme>(TaikoRulesetSetting.TouchControlScheme)
                }),
                CreateItem(hitAnimations = new CheckboxEntry
                {
                    Caption = RulesetSettingsStrings.HitAnimations,
                    HintText = RulesetSettingsStrings.HitAnimationsTaikoTooltip,
                    Current = config.GetBindable<bool>(TaikoRulesetSetting.HitAnimations)
                }),
                CreateItem(rateAdjustedAnimations = new CheckboxEntry
                {
                    Caption = RulesetSettingsStrings.RateAdjustedHitAnimation,
                    HintText = RulesetSettingsStrings.RateAdjustedHitAnimationTooltip,
                    Current = config.GetBindable<bool>(TaikoRulesetSetting.RateAdjustedHitAnimation),
                    ClassicDefault = false,
                })
            };

            hitAnimations.Current.BindValueChanged(val =>
            {
                rateAdjustedAnimations.Current.Disabled = !val.NewValue;
            }, true);
        }
    }
}
