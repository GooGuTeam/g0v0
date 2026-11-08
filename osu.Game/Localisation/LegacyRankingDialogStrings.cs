// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    /// <summary>
    /// The text of stable's online ranking page below the results (<c>RankingDialog</c>).
    /// </summary>
    public static class LegacyRankingDialogStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.LegacyRankingDialog";

        /// <summary>
        /// "You achieved the #{0} score on local rankings!"
        /// </summary>
        public static LocalisableString LocalRanking(int position) => new TranslatableString(getKey(@"local_ranking"), @"You achieved the #{0} score on local rankings!", position);

        /// <summary>
        /// "Save replay to Replays folder"
        /// </summary>
        public static LocalisableString SaveReplay => new TranslatableString(getKey(@"save_replay"), @"Save replay to Replays folder");

        /// <summary>
        /// "Add as online favourite"
        /// </summary>
        public static LocalisableString AddOnlineFavourite => new TranslatableString(getKey(@"add_online_favourite"), @"Add as online favourite");

        /// <summary>
        /// "Loading results..."
        /// </summary>
        public static LocalisableString LoadingResults => new TranslatableString(getKey(@"loading_results"), @"Loading results...");

        /// <summary>
        /// "Overall"
        /// </summary>
        public static LocalisableString Overall => new TranslatableString(getKey(@"overall"), @"Overall");

        /// <summary>
        /// "Accuracy"
        /// </summary>
        public static LocalisableString Accuracy => new TranslatableString(getKey(@"accuracy"), @"Accuracy");

        /// <summary>
        /// "Ranked Score"
        /// </summary>
        public static LocalisableString RankedScore => new TranslatableString(getKey(@"ranked_score"), @"Ranked Score");

        /// <summary>
        /// "Total Score"
        /// </summary>
        public static LocalisableString TotalScore => new TranslatableString(getKey(@"total_score"), @"Total Score");

        /// <summary>
        /// "Max Combo"
        /// </summary>
        public static LocalisableString MaxCombo => new TranslatableString(getKey(@"max_combo"), @"Max Combo");

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
        /// "new!"
        /// </summary>
        public static LocalisableString NewMark => new TranslatableString(getKey(@"new_mark"), @"new!");

        /// <summary>
        /// "your best: {0}"
        /// </summary>
        public static LocalisableString YourBest(string best) => new TranslatableString(getKey(@"your_best"), @"your best: {0}", best);

        /// <summary>
        /// "{0:#,0} plays since {1}"
        /// </summary>
        public static LocalisableString PlaysSince(int plays, string since) => new TranslatableString(getKey(@"plays_since"), @"{0:#,0} plays since {1}", plays, since);

        /// <summary>
        /// "{0}% pass rate"
        /// </summary>
        public static LocalisableString PassRate(int rate) => new TranslatableString(getKey(@"pass_rate"), @"{0}% pass rate", rate);

        /// <summary>
        /// "Rating is not available."
        /// </summary>
        public static LocalisableString RatingNotAvailable => new TranslatableString(getKey(@"rating_not_available"), @"Rating is not available.");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
