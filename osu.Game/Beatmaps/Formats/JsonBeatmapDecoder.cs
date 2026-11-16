// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using Newtonsoft.Json.Linq;
using osu.Game.IO;
using osu.Game.IO.Serialization;
using osu.Game.Rulesets.Formats;

namespace osu.Game.Beatmaps.Formats
{
    public class JsonBeatmapDecoder : Decoder<Beatmap>
    {
        public static void Register()
        {
            AddDecoder<Beatmap>("{", _ => new JsonBeatmapDecoder());
        }

        protected override void ParseStreamInto(LineBufferedReader stream, bool isPrimaryStream, Beatmap output)
        {
            string json = stream.ReadToEnd();
            // Ruleset-native charts are stored as envelopes which only their owning format understands.
            if (!RulesetBeatmapFormat.TryDecode(JObject.Parse(json), output))
                json.DeserializeInto(output);

            foreach (var hitObject in output.HitObjects)
                hitObject.ApplyDefaults(output.ControlPointInfo, output.Difficulty);
        }
    }
}
