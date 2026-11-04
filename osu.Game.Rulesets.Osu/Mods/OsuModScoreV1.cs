// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Localisation.Osu;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModScoreV1 : ModScoreV1, IApplicableToScoreProcessor, IApplicableToBeatmap
    {
        public override ModType Type => ModType.Conversion;

        public override LocalisableString Description => ModsStrings.ScoreV1Description;

        public override bool UserPlayable => true;

        public override bool ValidForMultiplayer => true;

        public override bool ValidForMultiplayerAsFreeMod => true;

        public override Type[] IncompatibleMods => base.IncompatibleMods.Concat(new[] { typeof(ModScoreV2) }).ToArray();

        public void ApplyToBeatmap(IBeatmap beatmap) => OsuCompatibilityRules.ApplyScoreVersion(beatmap, scoreV2: false);

        public void ApplyToScoreProcessor(ScoreProcessor scoreProcessor)
        {
            if (scoreProcessor is OsuScoreProcessor osuScoreProcessor)
                osuScoreProcessor.ScoreV1Active = true;
        }

        public ScoreRank AdjustRank(ScoreRank rank, double accuracy) => rank;
    }
}
