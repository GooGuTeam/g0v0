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
        private readonly string url;
        private readonly LocalisableString? title;

        [Resolved]
        private IImagePreviewOverlay? previewOverlay { get; set; }

        public PreviewableSprite(string url, LocalisableString? title = null)
        {
            this.url = url;
            this.title = title;
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (Texture == null || string.IsNullOrEmpty(url))
                return false;

            previewOverlay?.ShowImage(new ImagePreviewRequest(Texture, url, title, this));
            return true;
        }
    }
}
