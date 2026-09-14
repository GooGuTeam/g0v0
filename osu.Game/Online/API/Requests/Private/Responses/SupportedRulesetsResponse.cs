using System;
using Newtonsoft.Json;

namespace osu.Game.Online.API.Requests.Private.Responses
{
    [Serializable]
    public class SupportedRulesetsResponse
    {
        [JsonProperty("gamemodes")]
        public APIRuleset[] Rulesets { get; set; } = [];

        [JsonProperty("total")]
        public int TotalCount { get; set; }

        [JsonProperty("enable_rx")]
        public bool SupportsRelax { get; set; }

        [JsonProperty("enable_ap")]
        public bool SupportsAutopilot { get; set; }
    }
}
