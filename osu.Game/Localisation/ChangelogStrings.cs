// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ChangelogStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.Changelog";

        /// <summary>
        /// "You can also do something!"
        /// </summary>
        public static LocalisableString PromotionText => new TranslatableString(getKey(@"promotion_text"), @"You can also do something!");

        /// <summary>
        /// "Bug reports, feature requests, even direct pull requests (AI open)... Your contribution can really shape the future of g0v0!."
        /// </summary>
        public static LocalisableString ContributionText => new TranslatableString(getKey(@"contribution_text"),
            @"Bug reports, feature requests, even direct pull requests (AI open)... Your contribution can really shape the future of g0v0!.");

        /// <summary>
        /// "See our contribution guide for details."
        /// </summary>
        public static LocalisableString SeeContributionGuideLink => new TranslatableString(getKey(@"see_contribution_guide_link"), @"See our contribution guide for details.");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
