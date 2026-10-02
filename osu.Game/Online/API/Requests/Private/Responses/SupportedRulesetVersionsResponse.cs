// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Newtonsoft.Json;

namespace osu.Game.Online.API.Requests.Private.Responses
{
    [Serializable]
    public class SupportedRulesetVersionsResponse
    {
        [JsonProperty("gamemodes")]
        public APIRulesetVersion[] Rulesets { get; set; } = [];

        [JsonProperty("total")]
        public int TotalCount { get; set; }
    }
}
