// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;

namespace osu.Game.Online
{
    /// <summary>
    /// Identifies the official g0v0! deployment.
    /// </summary>
    public static class OfficialServer
    {
        /// <summary>
        /// The host of the official API server.
        /// </summary>
        public const string HOST = @"osu.g0v0.top";

        /// <summary>
        /// Hosts belonging to the official deployment.
        /// </summary>
        private static readonly string[] official_hosts = [HOST, @"lazer-api.g0v0.top"];

        /// <summary>
        /// Whether <paramref name="url"/> points at the official deployment.
        /// </summary>
        public static bool IsOfficialUrl(string? url)
            => Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && IsOfficialHost(uri.Host);

        /// <summary>
        /// Whether <paramref name="host"/> belongs to the official deployment.
        /// </summary>
        public static bool IsOfficialHost(string? host)
            => !string.IsNullOrEmpty(host) && official_hosts.Any(official => string.Equals(host, official, StringComparison.OrdinalIgnoreCase));
    }
}
