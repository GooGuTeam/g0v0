// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class BeatmapOverlayStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.BeatmapOverlayStrings";

        /// <summary>
        /// "I understand"
        /// </summary>
        public static LocalisableString UserContentConfirmButtonText => new TranslatableString(getKey(@"understood"), @"I understand");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
