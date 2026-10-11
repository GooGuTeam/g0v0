// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Newtonsoft.Json;

namespace osu.Game.Online.API.Requests.Private.Responses
{
    [Serializable]
    public class APIRuleset
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("readable")]
        public string ReadableName { get; set; } = string.Empty;

        [JsonProperty("is_official")]
        public bool IsOfficial { get; set; }

        [JsonProperty("is_custom_ruleset")]
        public bool IsCustomRuleset { get; set; }
    }
}
