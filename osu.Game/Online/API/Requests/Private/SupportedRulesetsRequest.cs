using osu.Game.Online.API.Requests.Private.Responses;

namespace osu.Game.Online.API.Requests.Private
{
    public class SupportedRulesetsRequest : PrivateAPIRequest<SupportedRulesetsResponse>
    {
        protected override string Target => @"gamemodes";
    }
}
