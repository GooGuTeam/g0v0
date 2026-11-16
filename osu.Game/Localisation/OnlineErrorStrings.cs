// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class OnlineErrorStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.OnlineError";

        /// <summary>
        /// "Couldn't load this content"
        /// </summary>
        public static LocalisableString LoadFailedTitle => new TranslatableString(getKey(@"load_failed_title"), @"Couldn't load this content");

        /// <summary>
        /// "The server could not be reached. Check your internet connection, then try again."
        /// </summary>
        public static LocalisableString LoadFailedDescription => new TranslatableString(getKey(@"load_failed_description"),
            @"The server could not be reached. Check your internet connection, then try again.");

        /// <summary>
        /// "This page may not exist, or the server could not be reached."
        /// </summary>
        public static LocalisableString PageUnavailableDescription => new TranslatableString(getKey(@"page_unavailable_description"),
            @"This page may not exist, or the server could not be reached.");

        /// <summary>
        /// "Retry"
        /// </summary>
        public static LocalisableString Retry => new TranslatableString(getKey(@"retry"), @"Retry");

        /// <summary>
        /// "Back to home"
        /// </summary>
        public static LocalisableString BackToHome => new TranslatableString(getKey(@"back_to_home"), @"Back to home");

        /// <summary>
        /// "Copy error details"
        /// </summary>
        public static LocalisableString CopyErrorDescription => new TranslatableString(getKey(@"copy_error_description"), @"Copy error details");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
