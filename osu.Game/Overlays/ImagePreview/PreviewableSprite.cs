// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;

namespace osu.Game.Overlays.ImagePreview
{
    /// <summary>
    /// A <see cref="Sprite"/> which presents a full-size preview of its texture when clicked.
    /// </summary>
    /// <remarks>
    /// This is the simplest way of hooking an image up to the <see cref="ImagePreviewOverlay"/>.
    /// The preview overlay is resolved via dependency injection, so this will do nothing when no overlay is available.
    /// </remarks>
    public partial class PreviewableSprite : Sprite
    {
        /// <summary>
        /// The url of the image, used when copying a link to it or saving it to disk.
        /// </summary>
        /// <remarks>
        /// Settable after construction because a container deriving from <c>ImageContainer</c> creates its sprite from
        /// its own constructor, which runs before the fields of the deriving type are assigned. Such a container can
        /// therefore only hand the url over once its constructor body runs.
        /// </remarks>
        public string? Url { get; set; }

        /// <summary>
        /// An optional title to be displayed above the image in the preview.
        /// </summary>
        public LocalisableString? Title { get; set; }

        [Resolved]
        private IImagePreviewOverlay? previewOverlay { get; set; }

        public PreviewableSprite(string? url = null, LocalisableString? title = null)
        {
            Url = url;
            Title = title;
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (Texture == null || string.IsNullOrEmpty(Url))
                return false;

            previewOverlay?.ShowImage(new ImagePreviewRequest(Texture, Url, Title, this));
            return true;
        }
    }
}
