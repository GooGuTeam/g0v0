// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ActivityStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.Activity";

        /// <summary>
        /// " has become a supporter - thanks for your generosity!"
        /// </summary>
        public static LocalisableString SupporterFirst => new TranslatableString(getKey(@"supporter_first"), @" has become a supporter - thanks for your generosity!");

        /// <summary>
        /// " has received the gift of supporter status!"
        /// </summary>
        public static LocalisableString SupporterGift => new TranslatableString(getKey(@"supporter_gift"), @" has received the gift of supporter status!");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
