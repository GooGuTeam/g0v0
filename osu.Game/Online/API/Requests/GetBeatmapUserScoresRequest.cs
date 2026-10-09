// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.IO.Network;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;

namespace osu.Game.Online.API.Requests
{
    /// <summary>
    /// All personal scores on one beatmap, so a PP best can be selected independently of score ordering.
    /// </summary>
    public class GetBeatmapUserScoresRequest : APIRequest<APIScoresCollection>
    {
        private readonly int beatmapId;
        private readonly int userId;
        private readonly RulesetInfo ruleset;

        public GetBeatmapUserScoresRequest(int beatmapId, int userId, RulesetInfo ruleset)
        {
            this.beatmapId = beatmapId;
            this.userId = userId;
            this.ruleset = ruleset;
        }

        protected override string Target => $"beatmaps/{beatmapId}/scores/users/{userId}/all";

        protected override WebRequest CreateWebRequest()
        {
            var request = base.CreateWebRequest();
            request.AddParameter("mode", ruleset.ShortName);
            return request;
        }
    }
}
