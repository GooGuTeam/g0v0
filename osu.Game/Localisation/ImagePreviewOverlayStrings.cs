// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ImagePreviewOverlayStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.ImagePreviewOverlay";

        /// <summary>
        /// "Zoom out"
        /// </summary>
        public static LocalisableString ZoomOut => new TranslatableString(getKey(@"zoom_out"), @"Zoom out");

        /// <summary>
        /// "Zoom in"
        /// </summary>
        public static LocalisableString ZoomIn => new TranslatableString(getKey(@"zoom_in"), @"Zoom in");

        /// <summary>
        /// "Save image"
        /// </summary>
        public static LocalisableString SaveImage => new TranslatableString(getKey(@"save_image"), @"Save image");

        /// <summary>
        /// "Close preview"
        /// </summary>
        public static LocalisableString ClosePreview => new TranslatableString(getKey(@"close_preview"), @"Close preview");

        /// <summary>
        /// "Image saved! Click to open the containing folder.
        /// {0}"
        /// </summary>
        public static LocalisableString ImageSaved(string filename) => new TranslatableString(getKey(@"image_saved"), @"Image saved! Click to open the containing folder.
{0}", filename);

        /// <summary>
        /// "Failed to save image."
        /// </summary>
        public static LocalisableString ImageSaveFailed => new TranslatableString(getKey(@"image_save_failed"), @"Failed to save image.");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
