// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using Markdig.Syntax.Inlines;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Containers.Markdown;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;
using osu.Game.Online.API;

namespace osu.Game.Graphics.Containers.Markdown
{
    public partial class OsuMarkdownImage : MarkdownImage, IHasTooltip
    {
        public LocalisableString TooltipText { get; }

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        public OsuMarkdownImage(LinkInline linkInline)
            : base(linkInline.Url)
        {
            TooltipText = linkInline.Title;
        }

        /// <summary>
        /// Defers the creation of the image until the wrapper actually loads it.
        /// </summary>
        /// <remarks>
        /// <see cref="MarkdownImage"/> builds its content from its own <see cref="BackgroundDependencyLoaderAttribute"/>
        /// method, and the loaders of a base type run before dependencies are injected into the types above it,
        /// so <see cref="api"/> is still <c>null</c> while that happens. The factory below is only invoked from
        /// <see cref="DelayedLoadWrapper"/>'s update loop, which runs after loading has fully completed.
        /// </remarks>
        protected override Drawable CreateContent(string url) => new DelayedLoadWrapper(() => CreateImageContainer(url));

        /// <summary>
        /// Wraps the image URL into the media endpoint of the server the client is connected to.
        /// </summary>
        protected sealed override ImageContainer CreateImageContainer(string url)
            => CreateImageContainerCore($@"{api.Endpoints.APIUrl}/media-url?url={ResolveImageUrl(url)}");

        /// <summary>
        /// Rewrites the image URL before it is wrapped by the media endpoint.
        /// </summary>
        /// <param name="url">The image URL as resolved from the Markdown.</param>
        protected virtual string ResolveImageUrl(string url) => url;

        /// <summary>
        /// Creates the container which displays the image served by the media endpoint.
        /// </summary>
        /// <param name="mediaUrl">The URL to request the image from.</param>
        protected virtual ImageContainer CreateImageContainerCore(string mediaUrl) => base.CreateImageContainer(mediaUrl);
    }
}
