// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Scoring
{
    /// <summary>
    /// Builds an FC estimate without changing the played score. Misses become the best basic judgement;
    /// combo-affecting ticks are restored, while lower judgements and non-combo bonuses are retained.
    /// </summary>
    public static class FullComboScoreInfo
    {
        public static ScoreInfo? Create(ScoreInfo score)
        {
            if (!score.Passed || score.Rank == ScoreRank.F || score.GetMaximumAchievableCombo() <= 0)
                return null;

            var maximumBasic = score.MaximumStatistics.Where(s => s.Key.IsBasic() && s.Key.IsHit() && s.Value > 0).ToArray();
            // Do not invent missing objects for incomplete or unavailable beatmaps.
            if (maximumBasic.Length != 1 || score.Statistics.Where(s => s.Key.IsBasic()).Sum(s => s.Value) != maximumBasic[0].Value)
                return null;

            var result = score.DeepClone();
            using var processor = score.Ruleset.CreateInstance().CreateScoreProcessor();
            processor.Mods.Value = result.Mods;
            if (score.IsLegacyScore)
                processor.ScoreV1Active = true;

            double maximumAccuracyScore = score.MaximumStatistics.Where(s => processor.ResultAffectsAccuracy(s.Key))
                                               .Sum(s => (double)processor.GetBaseScoreForResult(s.Key) * s.Value);
            double oldAccuracyScore = accuracyScore(score);

            HitResult bestResult = maximumBasic[0].Key;
            result.Statistics.TryGetValue(HitResult.Miss, out int misses);
            result.Statistics.TryGetValue(bestResult, out int bestCount);
            result.Statistics[bestResult] = bestCount + misses;
            result.Statistics[HitResult.Miss] = 0;
            result.Statistics[HitResult.LargeTickMiss] = 0;
            result.Statistics[HitResult.ComboBreak] = 0;

            foreach (var (judgement, count) in score.MaximumStatistics.Where(s => s.Key.IsTick() && s.Key.IncreasesCombo()))
                result.Statistics[judgement] = count;

            result.MaxCombo = score.GetMaximumAchievableCombo();
            // Avoid deriving a new accuracy from rounded display values. Bonus ticks never affect this delta.
            result.Accuracy = maximumAccuracyScore > 0
                ? Math.Clamp(score.Accuracy + (accuracyScore(result) - oldAccuracyScore) / maximumAccuracyScore, 0, 1)
                : score.Accuracy;
            result.Rank = processor.RankFromScore(result.Accuracy, result.Statistics);
            result.PP = null;
            // The original classic total would otherwise make the PP calculator infer the old slider breaks.
            result.LegacyTotalScore = null;
            return result;

            double accuracyScore(ScoreInfo info) => info.Statistics.Where(s => processor.ResultAffectsAccuracy(s.Key))
                                            .Sum(s => (double)processor.GetBaseScoreForResult(s.Key) * s.Value);
        }
    }
}
