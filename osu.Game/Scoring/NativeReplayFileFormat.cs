// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;

namespace osu.Game.Scoring
{
    public class NativeReplayFileFormat : ScoreReplayFormat
    {
        public override string Extension => ".g0r";

        public override bool CanEncode(Ruleset ruleset) => ruleset.CreateReplayFormat() != null;

        public override void Encode(Score score, IBeatmap beatmap, Stream stream) => NativeScoreFormat.Encode(score, stream);

        public override Score Decode(Stream stream, IRulesetStore rulesets, BeatmapManager beatmaps) => NativeScoreFormat.Decode(stream,
            name => rulesets.AvailableRulesets.FirstOrDefault(r => r.ShortName == name)?.CreateInstance(),
            md5 => beatmaps.QueryBeatmap(b => b.MD5Hash == md5));
    }
}
