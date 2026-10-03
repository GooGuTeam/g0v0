// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Scoring
{
    public partial class OsuScoreProcessor : ScoreProcessor
    {
        private int difficultyMultiplier = 1;

        private long currentScoreV1BaseScore;
        private long currentScoreV1ComboScoreWithoutMods;
        private long currentScoreV1ComboScoreWithMods;

        public OsuScoreProcessor()
            : base(new OsuRuleset())
        {
            Beatmap.ValueChanged += b => updateDifficultyMultiplier(b.NewValue);
        }

        protected override bool CheckScoreV1Active(IReadOnlyList<Mod> mods)
            => mods.Any(m => m is ModScoreV1);

        public override void ApplyBeatmap(IBeatmap beatmap)
        {
            updateDifficultyMultiplier(beatmap);
            base.ApplyBeatmap(beatmap);
        }

        private void updateDifficultyMultiplier(IBeatmap? beatmap)
        {
            if (beatmap != null)
                difficultyMultiplier = LegacyScoreUtils.CalculateDifficultyPeppyStars(beatmap);
            else
                difficultyMultiplier = 1;
        }

        protected override void Reset(bool storeResults)
        {
            base.Reset(storeResults);

            currentScoreV1BaseScore = 0;
            currentScoreV1ComboScoreWithoutMods = 0;
            currentScoreV1ComboScoreWithMods = 0;
        }

        public override ScoreRank RankFromScore(double accuracy, IReadOnlyDictionary<HitResult, int> results)
        {
            if (ScoreV1Active)
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

        protected override double GetBonusScoreChange(JudgementResult result)
        {
            if (ScoreV1Active)
            {
                if (result.HitObject is SpinnerBonusTick)
                    return 1100;

                if (result.HitObject is SpinnerTick)
                    return 100;
            }

            return base.GetBonusScoreChange(result);
        }

        private (long baseScore, long comboScore) getScoreV1Change(JudgementResult result, double modMultiplier)
        {
            if (!result.IsHit || result.Type.IsBonus())
                return (0, 0);

            int baseScore;
            bool addScoreComboMultiplier = false;

            if (result.HitObject is SliderTick)
            {
                baseScore = 10;
            }
            else if (result.HitObject is SliderHeadCircle or SliderTailCircle or SliderRepeat)
            {
                baseScore = 30;
            }
            else if (result.HitObject is HitCircle or Slider or Spinner)
            {
                baseScore = result.Type switch
                {
                    HitResult.Great => 300,
                    HitResult.Ok => 100,
                    HitResult.Meh => 50,
                    _ => 0
                };
                addScoreComboMultiplier = true;
            }
            else
            {
                baseScore = GetBaseScoreForResult(result.Type);
            }

            if (baseScore <= 0)
                return (0, 0);

            long comboScore = 0;

            if (addScoreComboMultiplier)
            {
                // In osu!stable: Math.Max(0, combo - 1) * (baseScore / 25 * difficultyMultiplier * modMultiplier)
                // The classic slider's final judgement supplies the tail combo itself.
                // stable includes that combo when awarding the slider's final score.
                int combo = result.HitObject is Slider ? result.ComboAfterJudgement : result.ComboAtJudgement;
                comboScore = (long)(Math.Max(0, combo - 1) * (baseScore / 25 * (difficultyMultiplier * modMultiplier)));
            }

            return (baseScore, comboScore);
        }

        protected override void ApplyScoreChange(JudgementResult result)
        {
            base.ApplyScoreChange(result);

            if (ScoreV1Active)
            {
                var (baseScore, comboScoreWithoutMods) = getScoreV1Change(result, 1.0);
                var (_, comboScoreWithMods) = getScoreV1Change(result, ScoreMultiplier);

                currentScoreV1BaseScore += baseScore;
                currentScoreV1ComboScoreWithoutMods += comboScoreWithoutMods;
                currentScoreV1ComboScoreWithMods += comboScoreWithMods;
            }
        }

        protected override void RemoveScoreChange(JudgementResult result)
        {
            base.RemoveScoreChange(result);

            if (ScoreV1Active)
            {
                var (baseScore, comboScoreWithoutMods) = getScoreV1Change(result, 1.0);
                var (_, comboScoreWithMods) = getScoreV1Change(result, ScoreMultiplier);

                currentScoreV1BaseScore -= baseScore;
                currentScoreV1ComboScoreWithoutMods -= comboScoreWithoutMods;
                currentScoreV1ComboScoreWithMods -= comboScoreWithMods;
            }
        }

        protected override void UpdateScore()
        {
            base.UpdateScore();

            if (ScoreV1Active)
            {
                TotalScoreWithoutMods.Value = currentScoreV1BaseScore + currentScoreV1ComboScoreWithoutMods + (long)CurrentBonusPortion;
                TotalScore.Value = currentScoreV1BaseScore + currentScoreV1ComboScoreWithMods + (long)CurrentBonusPortion;
            }
        }

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
