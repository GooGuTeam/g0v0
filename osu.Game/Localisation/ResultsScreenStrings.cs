// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ResultsScreenStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.ResultsScreen";

        /// <summary>
        /// "Performance points are not granted for this score because the beatmap is not ranked."
        /// </summary>
        public static LocalisableString NoPPForUnrankedBeatmaps => new TranslatableString(getKey(@"no_pp_for_unranked_beatmaps"), @"Performance points are not granted for this score because the beatmap is not ranked.");

        /// <summary>
        /// "Performance points are not granted for this score because of unranked mods."
        /// </summary>
        public static LocalisableString NoPPForUnrankedMods => new TranslatableString(getKey(@"no_pp_for_unranked_mods"), @"Performance points are not granted for this score because of unranked mods.");

        /// <summary>
        /// "Performance points are not granted for failed scores."
        /// </summary>
        public static LocalisableString NoPPForFailedScores => new TranslatableString(getKey(@"no_pp_for_failed_scores"), @"Performance points are not granted for failed scores.");

        /// <summary>
        /// "Leaderboard / More info"
        /// </summary>
        public static LocalisableString LeaderboardAndDetails => new TranslatableString(getKey(@"leaderboard_and_details"), @"Leaderboard / More info");

        /// <summary>
        /// "Score overview"
        /// </summary>
        public static LocalisableString ScoreOverview => new TranslatableString(getKey(@"score_overview"), @"Score overview");

        /// <summary>
        /// "More info"
        /// </summary>
        public static LocalisableString MoreInfo => new TranslatableString(getKey(@"more_info"), @"More info");

        /// <summary>
        /// "Play again"
        /// </summary>
        public static LocalisableString PlayAgain => new TranslatableString(getKey(@"play_again"), @"Play again");

        /// <summary>
        /// "Screenshot"
        /// </summary>
        public static LocalisableString Screenshot => new TranslatableString(getKey(@"screenshot"), @"Screenshot");

        /// <summary>
        /// "Upload SS"
        /// </summary>
        public static LocalisableString UploadScreenshotShort => new TranslatableString(getKey(@"upload_screenshot_short"), @"Upload SS");

        /// <summary>
        /// "by {0}"
        /// </summary>
        public static LocalisableString MappedBy(string mapper) => new TranslatableString(getKey(@"mapped_by"), @"by {0}", mapper);

        /// <summary>
        /// "Achieved on {0}"
        /// </summary>
        public static LocalisableString AchievedOn(string date) => new TranslatableString(getKey(@"achieved_on"), @"Achieved on {0}", date);

        /// <summary>
        /// "MODS"
        /// </summary>
        public static LocalisableString Mods => new TranslatableString(getKey(@"mods"), @"MODS");

        /// <summary>
        /// "No mods"
        /// </summary>
        public static LocalisableString NoMods => new TranslatableString(getKey(@"no_mods"), @"No mods");

        /// <summary>
        /// "Replay unavailable"
        /// </summary>
        public static LocalisableString ReplayUnavailable => new TranslatableString(getKey(@"replay_unavailable"), @"Replay unavailable");

        /// <summary>
        /// "Downloading replay…"
        /// </summary>
        public static LocalisableString DownloadingReplay => new TranslatableString(getKey(@"downloading_replay"), @"Downloading replay…");

        /// <summary>
        /// "Retry is available after playing this beatmap."
        /// </summary>
        public static LocalisableString RetryUnavailable => new TranslatableString(getKey(@"retry_unavailable"), @"Retry is available after playing this beatmap.");

        /// <summary>
        /// "PERSONAL BEST"
        /// </summary>
        public static LocalisableString PersonalBest => new TranslatableString(getKey(@"personal_best"), @"PERSONAL BEST");

        /// <summary>
        /// "Combo"
        /// </summary>
        public static LocalisableString Combo => new TranslatableString(getKey(@"combo"), @"Combo");

        /// <summary>
        /// "FEATURED ARTIST"
        /// </summary>
        public static LocalisableString FeaturedArtist => new TranslatableString(getKey(@"featured_artist"), @"FEATURED ARTIST");

        /// <summary>
        /// "if FC {0}pp"
        /// </summary>
        public static LocalisableString IfFullCombo(double pp) => new TranslatableString(getKey(@"if_full_combo"), @"if FC {0}pp", pp);

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
