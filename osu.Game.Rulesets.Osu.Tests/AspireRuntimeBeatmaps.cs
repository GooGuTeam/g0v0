// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Text;
using osu.Game.Beatmaps;
using osu.Game.IO;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using Decoder = osu.Game.Beatmaps.Formats.Decoder;

namespace osu.Game.Rulesets.Osu.Tests
{
    internal static class AspireRuntimeBeatmaps
    {
        public static Beatmap Decode(string objects, double overallDifficulty = 5)
        {
            string contents = FormattableString.Invariant($@"osu file format v14

[General]
StackLeniency:0.7

[Difficulty]
HPDrainRate:5
OverallDifficulty:{overallDifficulty}
ApproachRate:5
SliderMultiplier:1
SliderTickRate:1

[TimingPoints]
0,1000,4,1,0,100,1,0

[HitObjects]
{objects}
");
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(contents));
            using var reader = new LineBufferedReader(stream);
            return Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
        }

        public static Mod[] Mods(bool classic, int scoreVersion)
        {
            Mod[] scoreMods = scoreVersion switch
            {
                1 => new Mod[] { new OsuModScoreV1() },
                2 => new Mod[] { new OsuModScoreV2() },
                _ => Array.Empty<Mod>()
            };

            return classic ? [new OsuModClassic(), .. scoreMods] : scoreMods;
        }
    }
}
