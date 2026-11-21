// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Newtonsoft.Json;

namespace osu.Game.Online.API.Requests.Responses
{
    public class APIScreenshotInfo
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("sha256_hash")]
        public string Sha256Hash { get; set; } = string.Empty;

        /// <summary>
        /// The stable share URL of the screenshot.
        /// </summary>
        [JsonProperty("url")]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// A direct URL to the stored file. May be a short-lived pre-signed URL, and may be null.
        /// </summary>
        [JsonProperty("direct_url")]
        public string? DirectUrl { get; set; }

        /// <summary>
        /// How many times the share URL has been opened.
        /// </summary>
        [JsonProperty("hits")]
        public int Hits { get; set; }

        [JsonProperty("timestamp")]
        public DateTimeOffset Timestamp { get; set; }

        [JsonProperty("last_access")]
        public DateTimeOffset LastAccess { get; set; }
    }
}
