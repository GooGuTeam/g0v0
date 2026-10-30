// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Overlays.ImagePreview
{
    /// <summary>
    /// A globally accessible overlay used to present images in full size, with zooming and save-to-disk capabilities.
    /// </summary>
    /// <remarks>
    /// Resolve this interface via dependency injection to hook any image up to the preview overlay.
    /// </remarks>
    public interface IImagePreviewOverlay
    {
        /// <summary>
        /// Display the image described by the provided <paramref name="request"/>.
        /// </summary>
        /// <param name="request">The image to display.</param>
        void ShowImage(ImagePreviewRequest request);
    }
}
