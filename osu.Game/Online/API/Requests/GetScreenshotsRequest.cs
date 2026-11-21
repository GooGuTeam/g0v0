// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using osu.Framework.IO.Network;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Online.API.Requests
{
    public class GetScreenshotsRequest : APIRequest<GetScreenshotsResponse>
    {
        public readonly int Limit;

        public readonly int Offset;

        /// <summary>
        /// Creates a request to list the current user's uploaded screenshots.
        /// </summary>
        /// <param name="limit">The number of screenshots to fetch (1-100, per server limits).</param>
        /// <param name="offset">The number of screenshots to skip.</param>
        public GetScreenshotsRequest(int limit = 20, int offset = 0)
        {
            Limit = limit;
            Offset = offset;
        }

        protected override string Route => @"private";

        protected override string Target => @"screenshots";

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();

            req.AddParameter(@"limit", Limit.ToString(CultureInfo.InvariantCulture));
            req.AddParameter(@"offset", Offset.ToString(CultureInfo.InvariantCulture));

            return req;
        }
    }
}
