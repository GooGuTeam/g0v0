// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using Markdig.Syntax.Inlines;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.Containers.Markdown;
using osu.Game.Overlays.ImagePreview;

namespace osu.Game.Overlays.Wiki.Markdown
{
    public partial class WikiMarkdownImage : OsuMarkdownImage
    {
        public WikiMarkdownImage(LinkInline linkInline)
            : base(linkInline)
        {
        }

        protected override ImageContainer CreateImageContainer(string url)
        {
            // The idea is to replace "https://website.url/wiki/{path-to-image}" to "https://website.url/wiki/images/{path-to-image}"
            // "/wiki/images/*" is route to fetch wiki image from osu!web server (see: https://github.com/ppy/osu-web/blob/4205eb66a4da86bdee7835045e4bf28c35456e04/routes/web.php#L289)
            url = url.Replace("/wiki/", "/wiki/images/");

            var container = new WikiImageContainer(url);
            container.OnLoadComplete += d => d.FadeInFromZero(300, Easing.OutQuint);
            return container;
        }

        private partial class WikiImageContainer : ImageContainer
        {
            private readonly string url;

            public WikiImageContainer(string url)
                : base(url)
            {
                this.url = url;
            }

            protected override Sprite CreateImageSprite() => new PreviewableSprite(url);
        }
    }
}
