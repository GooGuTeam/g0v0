// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace osu.Game.Online.API.Requests.Private.Responses
{
    /// <summary>
    /// Version and release information of a custom ruleset registered on a private server.
    /// </summary>
    [Serializable]
    public class APIRulesetVersion
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("readable")]
        public string ReadableName { get; set; } = string.Empty;

        /// <summary>
        /// The version the server considers the most recent one.
        /// </summary>
        [JsonProperty("latest_version")]
        public string LatestVersion { get; set; } = string.Empty;

        /// <summary>
        /// Mapping of a ruleset version to the MD5 hash of the ruleset assembly released with it.
        /// Used to work out which version is installed locally.
        /// </summary>
        [JsonProperty("versions")]
        public Dictionary<string, string> Versions { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Release link of <see cref="LatestVersion"/>, if the server provides one.
        /// </summary>
        [JsonProperty("download_url")]
        public string? DownloadUrl { get; set; }
    }
}
