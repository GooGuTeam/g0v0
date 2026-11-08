// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Game.Configuration;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Osu.Configuration
{
    public class OsuRulesetConfigManager : RulesetConfigManager<OsuRulesetSetting>
    {
        public OsuRulesetConfigManager(SettingsStore? settings, RulesetInfo ruleset, int? variant = null)
            : base(settings, ruleset, variant)
        {
        }

        protected override void InitialiseDefaults()
        {
            base.InitialiseDefaults();
            SetDefault(OsuRulesetSetting.SnakingInSliders, true);
            SetDefault(OsuRulesetSetting.SnakingOutSliders, true);
            SetDefault(OsuRulesetSetting.HitAnimations, true);
            SetDefault(OsuRulesetSetting.ShowCursorTrail, true);
            SetDefault(OsuRulesetSetting.ShowCursorRipples, false);
            SetDefault(OsuRulesetSetting.PlayfieldBorderStyle, PlayfieldBorderStyle.None);
            SetDefault(OsuRulesetSetting.ShowSliderHeadJudgements, false);
            SetDefault(OsuRulesetSetting.KeepOverlapCirclesVisible, false);
            SetDefault(OsuRulesetSetting.ShowHiddenSliders, true);
            SetDefault(OsuRulesetSetting.LegacySliderTickFading, false);
            SetDefault(OsuRulesetSetting.LegacySliderTickVisibility, false);
            SetDefault(OsuRulesetSetting.HiddenHitFeedback, false);
            SetDefault(OsuRulesetSetting.SliderViewportSize, 0, 0, 65536);
            SetDefault(OsuRulesetSetting.SliderViewportMode, SliderViewportMode.Default);
            SetDefault(OsuRulesetSetting.SliderViewportCustomSize, "1920x1080");

            SetDefault(OsuRulesetSetting.ReplayClickMarkersEnabled, false);
            SetDefault(OsuRulesetSetting.ReplayFrameMarkersEnabled, false);
            SetDefault(OsuRulesetSetting.ReplayCursorPathEnabled, false);
            SetDefault(OsuRulesetSetting.ReplayCursorHideEnabled, false);
            SetDefault(OsuRulesetSetting.ReplayAnalysisDisplayLength, 800);
        }
    }

    public enum OsuRulesetSetting
    {
        SnakingInSliders,
        SnakingOutSliders,
        HitAnimations,
        ShowCursorTrail,
        ShowCursorRipples,
        PlayfieldBorderStyle,
        ShowSliderHeadJudgements,

        // Replay
        ReplayClickMarkersEnabled,
        ReplayFrameMarkersEnabled,
        ReplayCursorPathEnabled,
        ReplayCursorHideEnabled,
        ReplayAnalysisDisplayLength,
        KeepOverlapCirclesVisible,
        ShowHiddenSliders,
        SliderViewportSize,
        LegacySliderTickFading,
        LegacySliderTickVisibility,
        HiddenHitFeedback,
        SliderViewportMode,
        SliderViewportCustomSize,
    }
}
