// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ResultsScreenStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.ResultsScreen";

        /// <summary>
        /// "Achieved on {0:g}"
        /// </summary>
        public static LocalisableString AchievedOnDate(DateTimeOffset date) => new TranslatableString(getKey(@"achieved_on_date"), @"Achieved on {0:g}", date);

        /// <summary>
        /// "Performance points are not granted for this score because the beatmap is not ranked."
        /// </summary>
        public static LocalisableString NoPPForUnrankedBeatmaps =>
            new TranslatableString(getKey(@"no_pp_for_unranked_beatmaps"), @"Performance points are not granted for this score because the beatmap is not ranked.");

        /// <summary>
        /// "Performance points are not granted for this score because of unranked mods."
        /// </summary>
        public static LocalisableString NoPPForUnrankedMods =>
            new TranslatableString(getKey(@"no_pp_for_unranked_mods"), @"Performance points are not granted for this score because of unranked mods.");

        /// <summary>
        /// "Performance points are not granted for failed scores."
        /// </summary>
        public static LocalisableString NoPPForFailedScores => new TranslatableString(getKey(@"no_pp_for_failed_scores"), @"Performance points are not granted for failed scores.");

        /// <summary>
        /// "Performance"
        /// </summary>
        public static LocalisableString PerformancePointsHeader => new TranslatableString(getKey(@"performance_points_header"), "Performance");

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
        /// "Personal best"
        /// </summary>
        public static LocalisableString PersonalBest => new TranslatableString(getKey(@"personal_best"), @"Personal best");

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

        /// <summary>
        /// "if SS {0}pp"
        /// </summary>
        public static LocalisableString IfPerfect(double pp) => new TranslatableString(getKey(@"if_ss"), @"if SS {0}pp", pp);

        /// <summary>
        /// "if SSH {0}pp"
        /// </summary>
        public static LocalisableString IfPerfectHidden(double pp) => new TranslatableString(getKey(@"if_ssh"), @"if SSH {0}pp", pp);

        /// <summary>
        /// "PP you can gain if you achieve FC. Click to switch SS case."
        /// </summary>
        public static LocalisableString IfFullComboTooltip => new TranslatableString(getKey(@"if_full_combo_tooltip"), @"PP you can gain if you achieve FC. Click to switch SS case.");

        /// <summary>
        /// "PP you can gain if you achieve SS."
        /// </summary>
        public static LocalisableString IfPerfectTooltip => new TranslatableString(getKey(@"if_ss_tooltip"), @"PP you can gain if you achieve SS.");

        /// <summary>
        /// "PP you can gain if you achieve SSH."
        /// </summary>
        public static LocalisableString IfPerfectHiddenTooltip => new TranslatableString(getKey(@"if_ssh_tooltip"), @"PP you can gain if you achieve SSH.");

        /// <summary>
        /// "Click to switch to FC case."
        /// </summary>
        public static LocalisableString ClickToFullComboTooltip => new TranslatableString(getKey(@"click_to_full_combo_tooltip"), @"Click to switch to FC case.");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
