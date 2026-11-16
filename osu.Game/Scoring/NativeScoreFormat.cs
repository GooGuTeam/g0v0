// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Game.Beatmaps;
using osu.Game.IO.Serialization;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Scoring;
using osu.Game.Users;

namespace osu.Game.Scoring
{
    /// <summary>
    /// A versioned JSON replay envelope for rulesets which cannot be represented in the legacy .osr format.
    /// The .g0r container preserves score metadata and delegates frame encoding to the installed ruleset.
    /// </summary>
    public static class NativeScoreFormat
    {
        private const string envelope_key = "g0v0ReplayFormat";
        private const int version = 1;

        public static void Encode(Score score, Stream stream)
        {
            var info = score.ScoreInfo;
            var format = info.Ruleset.CreateInstance().CreateReplayFormat() ?? throw new InvalidOperationException("This ruleset has no native replay format.");
            var serializer = JsonSerializer.Create(JsonSerializableExtensions.CreateGlobalSettings());
            var document = new JObject
            {
                [envelope_key] = version,
                ["ruleset"] = info.Ruleset.ShortName,
                ["frame_format_version"] = format.Version,
                ["scoring_version"] = info.TotalScoreVersion,
                ["beatmap_md5"] = info.BeatmapInfo?.MD5Hash ?? throw new InvalidOperationException("A replay requires a beatmap."),
                ["beatmap_hash"] = info.BeatmapHash,
                ["total_score"] = info.TotalScore,
                ["total_score_without_mods"] = info.TotalScoreWithoutMods,
                ["accuracy"] = info.Accuracy,
                ["max_combo"] = info.MaxCombo,
                ["date"] = info.Date,
                ["passed"] = info.Passed,
                ["health_graph"] = JArray.FromObject(info.HealthGraph, serializer),
                ["username"] = info.User.Username,
                ["country_code"] = JToken.FromObject(info.User.CountryCode, serializer),
                ["user_id"] = info.User.OnlineID,
                ["online_id"] = info.OnlineID,
                ["mods"] = JArray.FromObject(info.APIMods, serializer),
                ["statistics"] = JObject.FromObject(info.Statistics, serializer),
                ["maximum_statistics"] = JObject.FromObject(info.MaximumStatistics, serializer),
                ["client_version"] = info.ClientVersion,
                ["rank"] = (int)info.Rank,
                ["pauses"] = JArray.FromObject(info.Pauses, serializer),
                ["frames"] = format.Encode(score.Replay)
            };
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true);
            using var json = new JsonTextWriter(writer);
            json.Formatting = Formatting.None;
            document.WriteTo(json);
        }

        public static Score Decode(Stream stream, Func<string, Ruleset?> resolveRuleset, Func<string, BeatmapInfo?> resolveBeatmap)
        {
            using var text = new StreamReader(stream, Encoding.UTF8, true, 1024, true);
            using var reader = new JsonTextReader(text);
            reader.DateParseHandling = DateParseHandling.DateTimeOffset;
            var document = JObject.Load(reader);
            if ((int?)document[envelope_key] != version)
                throw new InvalidDataException("Unsupported native replay version.");

            string shortName = document.Value<string>("ruleset") ?? throw new InvalidDataException("Native replay has no ruleset.");
            var ruleset = resolveRuleset(shortName) ?? throw new InvalidDataException($"Missing replay ruleset: {shortName}");
            var format = ruleset.CreateReplayFormat() ?? throw new InvalidDataException("This ruleset has no native replay format.");
            string md5 = document.Value<string>("beatmap_md5") ?? throw new InvalidDataException("Native replay has no beatmap hash.");
            var beatmap = resolveBeatmap(md5) ?? throw new ReplayBeatmapNotFoundException(md5);
            string hash = document.Value<string>("beatmap_hash") ?? string.Empty;
            if (beatmap.MD5Hash != md5 || beatmap.Hash != hash)
                throw new InvalidDataException("Native replay does not match the installed beatmap.");

            var serializer = JsonSerializer.Create(JsonSerializableExtensions.CreateGlobalSettings());
            var info = new ScoreInfo(beatmap, ruleset.RulesetInfo)
            {
                BeatmapHash = hash,
                TotalScoreVersion = document.Value<int>("scoring_version"),
                TotalScore = document.Value<long>("total_score"),
                TotalScoreWithoutMods = document.Value<long>("total_score_without_mods"),
                Accuracy = document.Value<double>("accuracy"),
                MaxCombo = document.Value<int>("max_combo"),
                Date = document.Value<DateTimeOffset>("date"),
                Passed = document.Value<bool>("passed"),
                HealthGraph = document["health_graph"]?.ToObject<List<HealthValue>>(serializer) ?? new List<HealthValue>(),
                User = new APIUser
                {
                    Id = document.Value<int>("user_id"),
                    Username = document.Value<string>("username") ?? string.Empty,
                    CountryCode = document["country_code"]?.ToObject<CountryCode>(serializer) ?? CountryCode.Unknown
                },
                OnlineID = document.Value<long>("online_id"),
                APIMods = document["mods"]?.ToObject<APIMod[]>(serializer) ?? Array.Empty<APIMod>(),
                Statistics = document["statistics"]?.ToObject<Dictionary<HitResult, int>>(serializer) ?? new Dictionary<HitResult, int>(),
                MaximumStatistics = document["maximum_statistics"]?.ToObject<Dictionary<HitResult, int>>(serializer) ?? new Dictionary<HitResult, int>(),
                ClientVersion = document.Value<string>("client_version") ?? string.Empty,
                Rank = (ScoreRank)document.Value<int>("rank"),
                IsLegacyScore = false
            };
            foreach (int pause in document["pauses"]?.ToObject<int[]>(serializer) ?? Array.Empty<int>())
                info.Pauses.Add(pause);

            return new Score
            {
                ScoreInfo = info,
                Replay = format.Decode(document["frames"] as JArray ?? throw new InvalidDataException("Native replay has no frames."), document.Value<int>("frame_format_version"))
            };
        }
    }
}
