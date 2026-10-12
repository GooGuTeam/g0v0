// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Scoring
{
    /// <summary>
    /// Builds a perfect (SS) estimate without changing the played score. Every basic judgement
    /// and tick is set to its maximum count, so the estimate always lands on 100% accuracy with
    /// the maximum achievable combo.
    /// </summary>
    public static class PerfectScoreInfo
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

            // A perfect play attains the maximum count of every basic judgement and tick.
            foreach (var (judgement, count) in score.MaximumStatistics.Where(s => s.Key.IsBasic() || s.Key.IsTick()))
                result.Statistics[judgement] = count;

            // Anything else the played score still carries which cannot occur in a perfect play is zeroed
            // (lower judgements and missed ticks of legacy scores are not present in the maximum statistics).
            foreach (var (judgement, _) in result.Statistics.Where(s => s.Key.IsBasic() || s.Key.IsTick()).ToArray())
            {
                if (!score.MaximumStatistics.ContainsKey(judgement))
                    result.Statistics[judgement] = 0;
            }

            result.Statistics[HitResult.ComboBreak] = 0;

            result.MaxCombo = score.GetMaximumAchievableCombo();
            result.Accuracy = 1;
            result.Rank = processor.RankFromScore(result.Accuracy, result.Statistics);
            result.PP = null;
            // The original classic total would otherwise make the PP calculator infer the old slider breaks.
            result.LegacyTotalScore = null;
            return result;
        }
    }
}
