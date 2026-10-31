// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

namespace osu.Game.Online
{
    public class ProductionEndpointConfiguration : EndpointConfiguration
    {
        public ProductionEndpointConfiguration()
        {
            WebsiteUrl = @"https://osu.g0v0.top";
            APIUrl = @"https://osu.g0v0.top";
            APIClientSecret = @"FGc9GAtyHzeQDshWP5Ah7dega8hJACAJpQtw6OXk";
            APIClientID = "5";
            SpectatorUrl = @"https://osu.g0v0.top/signalr/spectator";
            MultiplayerUrl = @"https://osu.g0v0.top/signalr/multiplayer";
            MetadataUrl = @"https://osu.g0v0.top/signalr/metadata";
            BeatmapSubmissionServiceUrl = @"https://osu.g0v0.top/beatmap-submission";
        }
    }
}
