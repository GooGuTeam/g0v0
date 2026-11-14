// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ThemeMusicStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.ThemeMusic";

        /// <summary>
        /// "Theme music"
        /// </summary>
        public static LocalisableString ThemeMusicHeader => new TranslatableString(getKey(@"theme_music_header"), @"Theme music");

        /// <summary>
        /// "Use custom theme music"
        /// </summary>
        public static LocalisableString UseCustomThemeMusic => new TranslatableString(getKey(@"use_custom_theme_music"), @"Use custom theme music");

        /// <summary>
        /// "Plays the audio of the beatmap selected as theme music on startup instead of the g0v0! music theme."
        /// </summary>
        public static LocalisableString UseCustomThemeMusicDescription => new TranslatableString(getKey(@"use_custom_theme_music_description"),
            @"Plays the audio of the beatmap selected as theme music on startup instead of the g0v0! music theme.");

        /// <summary>
        /// "Start theme music from preview point"
        /// </summary>
        public static LocalisableString StartFromPreviewPoint => new TranslatableString(getKey(@"start_from_preview_point"), @"Start theme music from preview point");

        /// <summary>
        /// "Only applies to custom theme music."
        /// </summary>
        public static LocalisableString StartFromPreviewPointDescription => new TranslatableString(getKey(@"start_from_preview_point_description"), @"Only applies to custom theme music.");

        /// <summary>
        /// "Set as theme music"
        /// </summary>
        public static LocalisableString UseAsThemeMusic => new TranslatableString(getKey(@"use_as_theme_music"), @"Set as theme music");

        /// <summary>
        /// "Current theme music: {0}"
        /// </summary>
        public static LocalisableString CurrentThemeMusic(string beatmapSet) => new TranslatableString(getKey(@"current_theme_music"), @"Current theme music: {0}", beatmapSet);

        /// <summary>
        /// "No beatmap is currently set as theme music."
        /// </summary>
        public static LocalisableString NoThemeMusic => new TranslatableString(getKey(@"no_theme_music"), @"No beatmap is currently set as theme music.");

        /// <summary>
        /// "The beatmapset set as theme music could not be found."
        /// </summary>
        public static LocalisableString ThemeMusicNotFound => new TranslatableString(getKey(@"theme_music_not_found"), @"The beatmapset set as theme music could not be found.");

        /// <summary>
        /// "Theme music unavailable, as the beatmap was deleted or has no audio: {0}"
        /// </summary>
        public static LocalisableString ThemeMusicUnavailable(string beatmapSet) => new TranslatableString(getKey(@"theme_music_unavailable"),
            @"Theme music unavailable, as the beatmap was deleted or has no audio: {0}", beatmapSet);

        /// <summary>
        /// "Reset theme music"
        /// </summary>
        public static LocalisableString ResetThemeMusic => new TranslatableString(getKey(@"reset_theme_music"), @"Reset theme music");

        /// <summary>
        /// "This will clear the selected theme music and revert to the default music theme."
        /// </summary>
        public static LocalisableString ResetThemeMusicConfirmation => new TranslatableString(getKey(@"reset_theme_music_confirmation"),
            @"This will clear the selected theme music and revert to the default music theme.");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
