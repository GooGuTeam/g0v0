// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Online.API.Requests.Private.Responses;

namespace osu.Game.Online.API.Requests.Private
{
    /// <summary>
    /// Retrieves the version, hash and release link details of the custom rulesets registered on a private server.
    /// </summary>
    /// <remarks>
    /// Only custom rulesets which have been registered by the server are returned; official game modes ship with
    /// the client and therefore never have version information.
    /// </remarks>
    public class SupportedRulesetVersionsRequest : PrivateAPIRequest<SupportedRulesetVersionsResponse>
    {
        protected override string Target => @"gamemodes/versions";
    }
}
