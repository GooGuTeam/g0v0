// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Game.IO.Serialization;
using osu.Game.Replays;

namespace osu.Game.Rulesets.Replays
{
    /// <summary>
    /// Serialises ruleset-owned replay frames without reducing them to legacy mouse/button data.
    /// The installed ruleset chooses the frame type; replay files never supply CLR type names.
    /// </summary>
    public abstract class RulesetReplayFormat
    {
        public virtual int Version => 1;

        public abstract JArray Encode(Replay replay);

        public abstract Replay Decode(JArray frames, int version);
    }

    public class RulesetReplayFormat<TFrame> : RulesetReplayFormat
        where TFrame : ReplayFrame
    {
        public override JArray Encode(Replay replay) => JArray.FromObject(replay.Frames.Cast<TFrame>(), JsonSerializer.Create(JsonSerializableExtensions.CreateGlobalSettings()));

        public override Replay Decode(JArray frames, int version)
        {
            if (version != Version)
                throw new InvalidDataException("Unsupported ruleset replay frame version.");

            var decoded = frames.ToObject<TFrame?[]>(JsonSerializer.Create(JsonSerializableExtensions.CreateGlobalSettings()))!;
            if (decoded.Any(f => f == null || !double.IsFinite(f.Time)))
                throw new InvalidDataException("Invalid replay frame timing.");

            return new Replay { Frames = decoded.Cast<ReplayFrame>().ToList() };
        }
    }
}
