// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Rulesets;
using osu.Game.Scoring.Legacy;

namespace osu.Game.Scoring
{
    public class LegacyReplayFileFormat : ScoreReplayFormat
    {
        public override string Extension => ".osr";

        public override bool CanEncode(Ruleset ruleset) => ruleset.RulesetInfo.IsLegacyRuleset();

        public override void Encode(Score score, IBeatmap beatmap, Stream stream) => new LegacyScoreEncoder(score, beatmap).Encode(stream, leaveOpen: true);

        public override Score Decode(Stream stream, IRulesetStore rulesets, BeatmapManager beatmaps)
        {
            try
            {
                return new DatabasedLegacyScoreDecoder(rulesets, beatmaps).Parse(stream);
            }
            catch (LegacyScoreDecoder.BeatmapNotFoundException e)
            {
                throw new ReplayBeatmapNotFoundException(e.Hash);
            }
        }
    }
}
