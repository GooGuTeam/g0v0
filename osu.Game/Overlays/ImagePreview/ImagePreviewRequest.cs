// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;

namespace osu.Game.Overlays.ImagePreview
{
    /// <summary>
    /// Describes an image to be presented in an <see cref="ImagePreviewOverlay"/>.
    /// </summary>
    /// <remarks>
    /// Any component wishing to offer full-size image previews should construct one of these and pass it to
    /// <see cref="IImagePreviewOverlay.ShowImage"/>. Providing <see cref="Source"/> allows the preview to transition
    /// from (and back to) the location the image was clicked at.
    /// </remarks>
    public class ImagePreviewRequest
    {
        /// <summary>
        /// The texture to be displayed in full size.
        /// </summary>
        public Texture Texture { get; }

        /// <summary>
        /// The canonical url of the image, used when copying a link or saving the image to disk.
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// An optional title to be displayed above the image.
        /// </summary>
        public LocalisableString? Title { get; }

        /// <summary>
        /// An optional drawable representing the image in its original location, used for transition animations.
        /// </summary>
        public Drawable? Source { get; }

        public ImagePreviewRequest(Texture texture, string url, LocalisableString? title = null, Drawable? source = null)
        {
            Texture = texture;
            Url = url;
            Title = title;
            Source = source;
        }
    }
}
