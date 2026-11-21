// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Net.Http;
using osu.Framework.IO.Network;

namespace osu.Game.Online.API.Requests
{
    public class DeleteScreenshotRequest : APIRequest
    {
        public readonly int ScreenshotId;

        public DeleteScreenshotRequest(int screenshotId)
        {
            ScreenshotId = screenshotId;
        }

        protected override string Route => @"private";

        protected override string Target => $@"screenshots/{ScreenshotId}";

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();
            req.Method = HttpMethod.Delete;
            return req;
        }
    }
}
