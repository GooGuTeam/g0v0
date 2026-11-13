// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Game.Extensions;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Online.API.Requests
{
    public class GetWikiRequest : APIRequest<APIWikiPage>
    {
        public readonly string Path;

        /// <summary>
        /// The language the page is requested in. This is not part of <see cref="Path"/>.
        /// </summary>
        internal Language RequestLanguage { get; }

        public GetWikiRequest(string path, Language language = Language.en)
        {
            Path = path;
            RequestLanguage = language;
        }

        protected override string Target => $"wiki/{RequestLanguage.ToCultureCode()}/{Path}";
    }
}
