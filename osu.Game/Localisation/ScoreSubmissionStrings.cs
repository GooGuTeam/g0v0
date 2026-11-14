// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ScoreSubmissionStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.ScoreSubmission";

        /// <summary>
        /// "Cannot start play"
        /// </summary>
        public static LocalisableString CannotStartPlay => new TranslatableString(getKey(@"cannot_start_play"), @"Cannot start play");

        /// <summary>
        /// "Score will not be submitted"
        /// </summary>
        public static LocalisableString NotSubmittingScore => new TranslatableString(getKey(@"not_submitting_score"), @"Score will not be submitted");

        /// <summary>
        /// "Failed to retrieve a score submission token."
        /// </summary>
        public static LocalisableString TokenRetrievalFailure => new TranslatableString(getKey(@"token_retrieval_failure"), @"Failed to retrieve a score submission token.");

        /// <summary>
        /// "Please ensure that you are using the latest version of the official game releases."
        /// </summary>
        public static LocalisableString InvalidClient => new TranslatableString(getKey(@"invalid_client"), @"Please ensure that you are using the latest version of the official game releases.");

        /// <summary>
        /// "This beatmap does not match the online version. Please update or redownload it."
        /// </summary>
        public static LocalisableString BeatmapMismatch
            => new TranslatableString(getKey(@"beatmap_mismatch"), @"This beatmap does not match the online version. Please update or redownload it.");

        /// <summary>
        /// "Your system clock is set incorrectly. Please check your system time, date and timezone."
        /// </summary>
        public static LocalisableString InvalidTime
            => new TranslatableString(getKey(@"invalid_time"), @"Your system clock is set incorrectly. Please check your system time, date and timezone.");

        /// <summary>
        /// "Online services are currently unavailable."
        /// </summary>
        public static LocalisableString OnlineUnavailable
            => new TranslatableString(getKey(@"online_unavailable"), @"Online services are currently unavailable.");

        /// <summary>
        /// "Your score will only be saved locally."
        /// </summary>
        public static LocalisableString SubmissionBlockedHint
            => new TranslatableString(getKey(@"submission_blocked_hint"), @"Your score will only be saved locally.");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
