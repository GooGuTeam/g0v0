// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// Precomputed export difficulty, available before the first simulated judgement.
    /// Avoids wall-clock-dependent background calculation during fast replay export.
    /// </summary>
    public sealed class ReplayExportDifficulty
    {
        public List<TimedDifficultyAttributes> Attributes { get; }

        public StarDifficulty Difficulty { get; }

        public ReplayExportDifficulty(WorkingBeatmap beatmap, Ruleset ruleset, Mod[] mods, ScoreProcessor scoreProcessor)
        {
            if (!beatmap.CanCalculateDifficulty)
            {
                Attributes = new List<TimedDifficultyAttributes>();
                Difficulty = new StarDifficulty(beatmap.BeatmapInfo.StarRating, scoreProcessor.MaximumCombo);
                return;
            }

            var calculator = ruleset.CreateDifficultyCalculator(beatmap);
            Attributes = calculator.CalculateTimed(mods);
            var difficulty = calculator.Calculate(mods);
            var perfectScore = new ScoreInfo(beatmap.BeatmapInfo, ruleset.RulesetInfo)
            {
                Passed = true,
                Accuracy = 1,
                Mods = mods,
                MaxCombo = scoreProcessor.MaximumCombo,
                Combo = scoreProcessor.MaximumCombo,
                TotalScore = scoreProcessor.MaximumTotalScore,
                Statistics = scoreProcessor.MaximumStatistics,
                MaximumStatistics = scoreProcessor.MaximumStatistics,
            };
            var performance = ruleset.CreatePerformanceCalculator()?.Calculate(perfectScore, difficulty) ?? new PerformanceAttributes();
            Difficulty = new StarDifficulty(difficulty, performance);
        }
    }
}
