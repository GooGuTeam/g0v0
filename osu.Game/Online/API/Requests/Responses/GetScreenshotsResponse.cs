// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json;

namespace osu.Game.Online.API.Requests.Responses
{
    public class GetScreenshotsResponse
    {
        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("items")]
        public APIScreenshotInfo[] Screenshots { get; set; } = [];
    }
}
