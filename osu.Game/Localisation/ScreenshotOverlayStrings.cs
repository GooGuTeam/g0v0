// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ScreenshotOverlayStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.ScreenshotOverlay";

        /// <summary>
        /// "Screenshots"
        /// </summary>
        public static LocalisableString Title => new TranslatableString(getKey(@"title"), @"Screenshots");

        /// <summary>
        /// "View and manage your uploaded screenshots"
        /// </summary>
        public static LocalisableString Description => new TranslatableString(getKey(@"description"), @"View and manage your uploaded screenshots");

        /// <summary>
        /// "Screenshots which have not been accessed for a while are periodically removed from the server."
        /// </summary>
        public static LocalisableString RetentionNote => new TranslatableString(getKey(@"retention_note"), @"Screenshots which have not been accessed for a while are periodically removed from the server.");

        /// <summary>
        /// "No screenshots yet"
        /// </summary>
        public static LocalisableString EmptyTitle => new TranslatableString(getKey(@"empty_title"), @"No screenshots yet");

        /// <summary>
        /// "Take a screenshot and upload it to see it here."
        /// </summary>
        public static LocalisableString EmptyDescription => new TranslatableString(getKey(@"empty_description"), @"Take a screenshot and upload it to see it here.");

        /// <summary>
        /// "Screenshots are not supported"
        /// </summary>
        public static LocalisableString UnsupportedTitle => new TranslatableString(getKey(@"unsupported_title"), @"Screenshots are not supported");

        /// <summary>
        /// "The current server does not support managing screenshots."
        /// </summary>
        public static LocalisableString UnsupportedDescription => new TranslatableString(getKey(@"unsupported_description"), @"The current server does not support managing screenshots.");

        /// <summary>
        /// "Delete this screenshot? Existing links will stop working."
        /// </summary>
        public static LocalisableString DeleteConfirmation => new TranslatableString(getKey(@"delete_confirmation"), @"Delete this screenshot? Existing links will stop working.");

        /// <summary>
        /// "Screenshot deleted."
        /// </summary>
        public static LocalisableString Deleted => new TranslatableString(getKey(@"deleted"), @"Screenshot deleted.");

        /// <summary>
        /// "Failed to delete the screenshot."
        /// </summary>
        public static LocalisableString DeleteFailed => new TranslatableString(getKey(@"delete_failed"), @"Failed to delete the screenshot.");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
