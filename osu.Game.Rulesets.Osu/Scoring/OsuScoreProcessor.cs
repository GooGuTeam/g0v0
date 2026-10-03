// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Scoring
{
    public partial class OsuScoreProcessor : ScoreProcessor
    {
        public OsuScoreProcessor()
            : base(new OsuRuleset())
        {
        }

        public override ScoreRank RankFromScore(double accuracy, IReadOnlyDictionary<HitResult, int> results)
        {
            if (Mods.Value.Any(m => m is ModClassic))
            {
                int count300 = results.GetValueOrDefault(HitResult.Great);
                int count100 = results.GetValueOrDefault(HitResult.Ok);
                int count50 = results.GetValueOrDefault(HitResult.Meh);
                int countMiss = results.GetValueOrDefault(HitResult.Miss);

                int total = count300 + count100 + count50 + countMiss;

                if (total == 0)
                    return ScoreRank.X;

                if (count300 == total)
                    return ScoreRank.X;

                double ratio300 = (double)count300 / total;
                double ratio50 = (double)count50 / total;

                if (ratio300 > 0.9 && ratio50 < 0.01 && countMiss == 0)
                    return ScoreRank.S;

                if ((ratio300 > 0.8 && countMiss == 0) || ratio300 > 0.9)
                    return ScoreRank.A;

                if ((ratio300 > 0.7 && countMiss == 0) || ratio300 > 0.8)
                    return ScoreRank.B;

                if (ratio300 > 0.6)
                    return ScoreRank.C;

                return ScoreRank.D;
            }

            ScoreRank rank = base.RankFromScore(accuracy, results);

            switch (rank)
            {
                case ScoreRank.S:
                case ScoreRank.X:
                    if (results.GetValueOrDefault(HitResult.Miss) > 0)
                        rank = ScoreRank.A;
                    break;
            }

            return rank;
        }

        protected override HitEvent CreateHitEvent(JudgementResult result)
            => base.CreateHitEvent(result).With((result as OsuHitCircleJudgementResult)?.CursorPositionAtHit);

        protected override double GetComboScoreChange(JudgementResult result)
        {
            if (ScoreV2Active)
            {
                if (!result.IsHit)
                    return 0;

                double baseScore;

                if (result.HitObject is SliderTick)
                    baseScore = 10;
                else if (result.HitObject is SliderHeadCircle or SliderTailCircle or SliderRepeat)
                    baseScore = 30;
                else if (result.HitObject is HitCircle or Slider or Spinner)
                {
                    baseScore = result.Type switch
                    {
                        HitResult.Great => 300,
                        HitResult.Ok => 100,
                        HitResult.Meh => 50,
                        _ => 0
                    };
                }
                else
                {
                    baseScore = GetBaseScoreForResult(result.Type);
                }

                if (baseScore <= 0)
                    return 0;

                int combo = result.ComboAfterJudgement;

                if (result.HitObject is SliderTailCircle)
                    combo++;

                if (result.HitObject is SliderTick && IsSimulating)
                    combo = Math.Max(0, combo - 2);

                return baseScore * (1 + combo / 10.0);
            }

            return base.GetComboScoreChange(result);
        }

        protected override double ComputeTotalScore(double comboProgress, double accuracyProgress, double bonusPortion)
        {
            if (ScoreV2Active)
            {
                return 700000 * comboProgress +
                       300000 * Math.Pow(Accuracy.Value, 10) * accuracyProgress +
                       bonusPortion;
            }

            return base.ComputeTotalScore(comboProgress, accuracyProgress, bonusPortion);
        }
    }
}
