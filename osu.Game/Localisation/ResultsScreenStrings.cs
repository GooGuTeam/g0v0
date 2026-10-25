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

        /// <summary>
        /// "Overall"
        /// </summary>
        public static LocalisableString Overall => new TranslatableString(getKey(@"overall"), @"Overall");

        /// <summary>
        /// "Ranked Score"
        /// </summary>
        public static LocalisableString RankedScore => new TranslatableString(getKey(@"ranked_score"), @"Ranked Score");

        /// <summary>
        /// "Total Score"
        /// </summary>
        public static LocalisableString TotalScore => new TranslatableString(getKey(@"total_score"), @"Total Score");

        /// <summary>
        /// "Performance"
        /// </summary>
        public static LocalisableString Performance => new TranslatableString(getKey(@"performance"), @"Performance");

        /// <summary>
        /// "Beatmap Ranking"
        /// </summary>
        public static LocalisableString BeatmapRanking => new TranslatableString(getKey(@"beatmap_ranking"), @"Beatmap Ranking");

        /// <summary>
        /// "Overall Ranking"
        /// </summary>
        public static LocalisableString OverallRanking => new TranslatableString(getKey(@"overall_ranking"), @"Overall Ranking");

        /// <summary>
        /// "Online Ranking"
        /// </summary>
        public static LocalisableString OnlineRanking => new TranslatableString(getKey(@"online_ranking"), @"Online Ranking");

        /// <summary>
        /// "Save replay to Replays folder"
        /// </summary>
        public static LocalisableString SaveReplayToFolder => new TranslatableString(getKey(@"save_replay_to_folder"), @"Save replay to Replays folder");

        /// <summary>
        /// "Add as online favourite"
        /// </summary>
        public static LocalisableString AddAsOnlineFavourite => new TranslatableString(getKey(@"add_as_online_favourite"), @"Add as online favourite");

        /// <summary>
        /// "You achieved the #{0} score on local rankings!"
        /// </summary>
        public static LocalisableString LocalRankAchieved(int rank) => new TranslatableString(getKey(@"local_rank_achieved"), @"You achieved the #{0} score on local rankings!", rank);

        /// <summary>
        /// "your best: {0}"
        /// </summary>
        public static LocalisableString YourBest(LocalisableString value) => new TranslatableString(getKey(@"your_best"), @"your best: {0}", value);

        /// <summary>
        /// "no plays recorded on this beatmap yet"
        /// </summary>
        public static LocalisableString NoPlaysRecorded => new TranslatableString(getKey(@"no_plays_recorded"), @"no plays recorded on this beatmap yet");

        /// <summary>
        /// "{0:N0} plays"
        /// </summary>
        public static LocalisableString PlayCount(long plays) => new TranslatableString(getKey(@"play_count"), @"{0:N0} plays", plays);

        /// <summary>
        /// "since {0}"
        /// </summary>
        public static LocalisableString SubmittedSince(LocalisableString date) => new TranslatableString(getKey(@"submitted_since"), @"since {0}", date);

        /// <summary>
        /// "{0:0}% pass rate"
        /// </summary>
        public static LocalisableString PassRate(double passRate) => new TranslatableString(getKey(@"pass_rate"), @"{0:0}% pass rate", passRate);

        /// <summary>
        /// "Beatmap by {0}"
        /// </summary>
        public static LocalisableString BeatmapBy(LocalisableString mapper) => new TranslatableString(getKey(@"beatmap_by"), @"Beatmap by {0}", mapper);

        /// <summary>
        /// "Played by {0} on {1}."
        /// </summary>
        public static LocalisableString PlayedBy(LocalisableString player, LocalisableString date) => new TranslatableString(getKey(@"played_by"), @"Played by {0} on {1}.", player, date);

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
