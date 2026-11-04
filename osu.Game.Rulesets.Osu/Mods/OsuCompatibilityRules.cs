// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Mods
{
    /// <summary>
    /// Resolves scoring and input compatibility separately. Score mods alone select scoring;
    /// Classic combined with a score mod selects the corresponding stable simulation profile.
    /// </summary>
    internal static class OsuCompatibilityRules
    {
        public static bool IsStableProfile(IEnumerable<Mod> mods) => mods.OfType<OsuModClassic>().Any()
                                                                    && mods.Any(mod => mod is OsuModScoreV1 or OsuModScoreV2);

        public static void ApplyClassic(OsuHitObject hitObject, bool checkpoints, bool headAccuracy)
        {
            hitObject.ClassicCompatibilityRequested = true;
            if (hitObject is Slider slider)
            {
                slider.ClassicSliderBehaviour = true;
                slider.ClassicSliderJudgement = checkpoints;
                slider.ScoreV2SliderBehaviour = headAccuracy;
            }
        }

        // Applied in IApplicableToBeatmap, after ALL IApplicableToHitObject callbacks.
        // This is intentionally independent of the order of Classic and score mods.
        public static void ApplyScoreVersion(IBeatmap beatmap, bool scoreV2)
        {
            foreach (var hitObject in beatmap.HitObjects.OfType<OsuHitObject>())
            {
                bool stable = hitObject.ClassicCompatibilityRequested;
                switch (hitObject)
                {
                    case Slider slider:
                        // The existing V1/V2 score processors consume a parent slider judgement.
                        // Aggregation is a scoring requirement, not a request for legacy tracking.
                        slider.ClassicSliderBehaviour = true;
                        slider.ScoreV2SliderBehaviour = scoreV2;
                        slider.ClassicSliderJudgement = stable;
                        break;

                    case Spinner spinner:
                        spinner.ExclusiveLegacyInput = stable;
                        if (scoreV2)
                            spinner.ApplyLegacyScoreV2(beatmap.ControlPointInfo, beatmap.Difficulty);
                        else
                            spinner.ApplyLegacyScoreV1(beatmap.ControlPointInfo, beatmap.Difficulty);
                        break;
                }
            }
        }
    }
}
