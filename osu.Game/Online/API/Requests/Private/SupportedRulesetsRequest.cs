// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Online.API.Requests.Private.Responses;

namespace osu.Game.Online.API.Requests.Private
{
    public class SupportedRulesetsRequest : PrivateAPIRequest<SupportedRulesetsResponse>
    {
        protected override string Target => @"gamemodes";
    }
}
