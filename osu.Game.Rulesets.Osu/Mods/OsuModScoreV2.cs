// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Localisation.Osu;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModScoreV2 : ModScoreV2, IApplicableToScoreProcessor, IApplicableToHitObject, IApplicableToBeatmap
    {
        public override ModType Type => ModType.Conversion;

        public override LocalisableString Description => ModsStrings.ScoreV2Description;

        public override bool UserPlayable => true;

        public override bool ValidForMultiplayer => true;

        public override bool ValidForMultiplayerAsFreeMod => true;

        public override System.Type[] IncompatibleMods => base.IncompatibleMods.Concat(new[] { typeof(ModScoreV1) }).ToArray();

        public void ApplyToHitObject(HitObject hitObject)
        {
            switch (hitObject)
            {
                case Slider slider:
                    slider.ClassicSliderBehaviour = true;
                    slider.ClassicSliderJudgement = true;
                    slider.ScoreV2SliderBehaviour = true;
                    break;
            }
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            foreach (var spinner in beatmap.HitObjects.OfType<Spinner>())
                spinner.ApplyLegacyScoreV2(beatmap.ControlPointInfo, beatmap.Difficulty);
        }

        public void ApplyToScoreProcessor(ScoreProcessor scoreProcessor)
        {
            if (scoreProcessor is OsuScoreProcessor osuScoreProcessor)
                osuScoreProcessor.ScoreV2Active = true;
        }

        public ScoreRank AdjustRank(ScoreRank rank, double accuracy) => rank;
    }
}
