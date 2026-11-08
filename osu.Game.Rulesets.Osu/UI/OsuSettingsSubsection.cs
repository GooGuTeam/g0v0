// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.Osu.Skinning.Default;
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

            var viewportMode = config.GetBindable<SliderViewportMode>(OsuRulesetSetting.SliderViewportMode);
            var customSize = config.GetBindable<string>(OsuRulesetSetting.SliderViewportCustomSize);
            var customEntry = new TextBoxEntry
            {
                Caption = RulesetSettingsStrings.SliderViewportCustomSize,
                HintText = RulesetSettingsStrings.SliderViewportCustomSizeDescription,
                PlaceholderText = "1920x1080",
                Current = customSize,
                LengthLimit = 32,
                Commit = () =>
                {
                    if (LegacySliderViewport.TryParseSize(customSize.Value, out var size))
                        customSize.Value = $"{(int)size.X}x{(int)size.Y}";
                },
            };
            viewportMode.BindValueChanged(v => customEntry.CanBeShown.Value = v.NewValue == SliderViewportMode.Custom, true);
            customSize.BindValueChanged(v => customEntry.Note.Value = LegacySliderViewport.TryParseSize(v.NewValue, out _)
                ? null
                : new SettingsNote.Data(RulesetSettingsStrings.SliderViewportCustomSizeDescription, SettingsNote.Type.Warning), true);

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
                    Caption = RulesetSettingsStrings.KeepOverlapCirclesVisible,
                    HintText = RulesetSettingsStrings.KeepOverlapCirclesVisibleDescription,
                    Current = config.GetBindable<bool>(OsuRulesetSetting.KeepOverlapCirclesVisible),
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = RulesetSettingsStrings.ShowHiddenSliders,
                    HintText = RulesetSettingsStrings.ShowHiddenSlidersDescription,
                    Current = config.GetBindable<bool>(OsuRulesetSetting.ShowHiddenSliders),
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = RulesetSettingsStrings.LegacySliderTickFading,
                    HintText = RulesetSettingsStrings.LegacySliderTickFadingDescription,
                    Current = config.GetBindable<bool>(OsuRulesetSetting.LegacySliderTickFading),
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = RulesetSettingsStrings.LegacySliderTickVisibility,
                    HintText = RulesetSettingsStrings.LegacySliderTickVisibilityDescription,
                    Current = config.GetBindable<bool>(OsuRulesetSetting.LegacySliderTickVisibility),
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = RulesetSettingsStrings.HiddenHitFeedback,
                    HintText = RulesetSettingsStrings.HiddenHitFeedbackDescription,
                    Current = config.GetBindable<bool>(OsuRulesetSetting.HiddenHitFeedback),
                }),
                CreateItem(new DropdownEntry<SliderViewportMode>
                {
                    Caption = RulesetSettingsStrings.SliderViewportSize,
                    HintText = RulesetSettingsStrings.SliderViewportSizeDescription,
                    Current = viewportMode,
                    Items = new[] { SliderViewportMode.Default, SliderViewportMode.Custom },
                    ItemLabel = mode => mode switch
                    {
                        SliderViewportMode.Custom => RulesetSettingsStrings.SliderViewportCustom,
                        _ => RulesetSettingsStrings.SliderViewportDefault,
                    },
                }),
                CreateItem(customEntry),
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
