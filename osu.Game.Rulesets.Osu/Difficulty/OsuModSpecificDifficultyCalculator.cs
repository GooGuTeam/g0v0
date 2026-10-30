// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    /// <summary>
    /// The mod-specific difficulty calculator for the osu! ruleset.
    /// </summary>
    /// <remarks>
    /// This extends the upstream calculator with fork-specific mod handling, most notably
    /// the dedicated <see cref="Relax"/> skill used by the Relax mod.
    /// </remarks>
    public class OsuModSpecificDifficultyCalculator : OsuDifficultyCalculator
    {
        public OsuModSpecificDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
        {
            // The upstream Aim and Speed skills contain their own Relax handling. This implementation
            // replaces it with the dedicated Relax skill, so those skills must not see the mod.
            Mod[] modsWithoutRelax = mods.Where(m => m is not OsuModRelax).ToArray();

            var skills = new List<Skill>
            {
                new Aim(modsWithoutRelax, true),
                new Aim(modsWithoutRelax, false),
                new Speed(modsWithoutRelax),
                new Reading(mods)
            };

            if (mods.Any(h => h is OsuModFlashlight))
                skills.Add(new Flashlight(mods, beatmap.HitObjects.Count));

            if (mods.Any(h => h is OsuModRelax))
                skills.Add(new Relax(mods, true));

            return skills.ToArray();
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
        {
            var attributes = (OsuDifficultyAttributes)base.CreateDifficultyAttributes(beatmap, mods, skills);

            if (beatmap.HitObjects.Count == 0 || skills.OfType<Relax>().SingleOrDefault() is not Relax relax)
                return attributes;

            // Recover the aim-without-sliders rating from the values the upstream calculation produced,
            // so the slider factor keeps describing the relax aim difficulty.
            double aimNoSlidersRating = attributes.SliderFactor * attributes.AimDifficulty;

            attributes.AimDifficulty = calculateAimDifficultyRating(relax.DifficultyValue());
            attributes.AimDifficultSliderCount = relax.GetDifficultSliders();
            attributes.SliderFactor = attributes.AimDifficulty > 0 ? aimNoSlidersRating / attributes.AimDifficulty : 1;

            double baseAimPerformance = OsuPerformanceCalculator.DifficultyToPerformance(attributes.AimDifficulty);
            double baseSpeedPerformance = HarmonicSkill.DifficultyToPerformance(attributes.SpeedDifficulty);
            double baseReadingPerformance = HarmonicSkill.DifficultyToPerformance(attributes.ReadingDifficulty);
            double baseFlashlightPerformance = attributes.FlashlightDifficulty != null ? Flashlight.DifficultyToPerformance(attributes.FlashlightDifficulty.Value) : 0.0;
            double baseCognitionPerformance = SumCognitionDifficulty(baseReadingPerformance, baseFlashlightPerformance);
            double basePerformance = DiffUtils.Norm(OsuPerformanceCalculator.PERFORMANCE_NORM_EXPONENT, baseAimPerformance, baseSpeedPerformance, baseCognitionPerformance);

            attributes.StarRating = Math.Cbrt(basePerformance * OsuPerformanceCalculator.PERFORMANCE_BASE_MULTIPLIER);

            return attributes;
        }

        protected override Mod[] DifficultyAdjustmentMods => new Mod[]
        {
            new OsuModTouchDevice(),
            new OsuModDoubleTime(),
            new OsuModHalfTime(),
            new OsuModEasy(),
            new OsuModHardRock(),
            new OsuModRelax(),
            new OsuModFlashlight(),
            new OsuModHidden(),
        };

        private static double calculateAimDifficultyRating(double difficultyValue) => DiffUtils.Pow(difficultyValue, 0.63) * 0.02275;
    }
}
