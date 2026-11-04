// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class HitSoundingStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.HitSounding";

        /// <summary>
        /// "Objects"
        /// </summary>
        public static LocalisableString Objects => new TranslatableString(getKey(@"objects"), @"Objects");

        /// <summary>
        /// "Set {0}"
        /// </summary>
        public static LocalisableString Set(int set) => new TranslatableString(getKey(@"set"), @"Set {0}", set);

        /// <summary>
        /// "User skin"
        /// </summary>
        public static LocalisableString UserSkin => new TranslatableString(getKey(@"user_skin"), @"User skin");

        /// <summary>
        /// "Custom #{0}"
        /// </summary>
        public static LocalisableString CustomSet(int set) => new TranslatableString(getKey(@"custom_set"), @"Custom #{0}", set);

        /// <summary>
        /// "Manage sets"
        /// </summary>
        public static LocalisableString ManageSets => new TranslatableString(getKey(@"manage_sets"), @"Manage sets");

        /// <summary>
        /// "Collapse set"
        /// </summary>
        public static LocalisableString CollapseSet => new TranslatableString(getKey(@"collapse_set"), @"Collapse set");

        /// <summary>
        /// "Expand set"
        /// </summary>
        public static LocalisableString ExpandSet => new TranslatableString(getKey(@"expand_set"), @"Expand set");

        /// <summary>
        /// "Move set up"
        /// </summary>
        public static LocalisableString MoveSetUp => new TranslatableString(getKey(@"move_set_up"), @"Move set up");

        /// <summary>
        /// "Move set down"
        /// </summary>
        public static LocalisableString MoveSetDown => new TranslatableString(getKey(@"move_set_down"), @"Move set down");

        /// <summary>
        /// "Normal"
        /// </summary>
        public static LocalisableString Normal => new TranslatableString(getKey(@"normal"), @"Normal");

        /// <summary>
        /// "Soft"
        /// </summary>
        public static LocalisableString Soft => new TranslatableString(getKey(@"soft"), @"Soft");

        /// <summary>
        /// "Drum"
        /// </summary>
        public static LocalisableString Drum => new TranslatableString(getKey(@"drum"), @"Drum");

        /// <summary>
        /// "Sample point: {0} · {1} · set {2}"
        /// </summary>
        public static LocalisableString SamplePoint(string sounds, string bank, int set) => new TranslatableString(getKey(@"sample_point"), @"Sample point: {0} · {1} · set {2}", sounds, bank, set);

        /// <summary>
        /// "Object sample"
        /// </summary>
        public static LocalisableString BodySample => new TranslatableString(getKey(@"body_sample"), @"Object sample");

        /// <summary>
        /// "Left-click to edit · Ctrl-click to select · Drag to change bank · Right-click for options"
        /// </summary>
        public static LocalisableString EditHint => new TranslatableString(getKey(@"edit_hint"), @"Left-click to edit · Ctrl-click to select · Drag to change bank · Right-click for options");

        /// <summary>
        /// "Restore defaults"
        /// </summary>
        public static LocalisableString RestoreDefaults => new TranslatableString(getKey(@"restore_defaults"), @"Restore defaults");

        /// <summary>
        /// "Move up one set"
        /// </summary>
        public static LocalisableString MoveUpOneSet => new TranslatableString(getKey(@"move_up_one_set"), @"Move up one set");

        /// <summary>
        /// "Move down one set"
        /// </summary>
        public static LocalisableString MoveDownOneSet => new TranslatableString(getKey(@"move_down_one_set"), @"Move down one set");

        /// <summary>
        /// "Drag to change volume · Double-click for precise editing · Ctrl-click to edit from a sample point"
        /// </summary>
        public static LocalisableString VolumeHint => new TranslatableString(getKey(@"volume_hint"), @"Drag to change volume · Double-click for precise editing · Ctrl-click to edit from a sample point");

        /// <summary>
        /// "Ctrl-click to edit the volume from this sample point onwards"
        /// </summary>
        public static LocalisableString CtrlSplitHint => new TranslatableString(getKey(@"ctrl_split_hint"), @"Ctrl-click to edit the volume from this sample point onwards");

        /// <summary>
        /// "{0} - {1}"
        /// </summary>
        public static LocalisableString TimeRange(string start, string end) => new TranslatableString(getKey(@"time_range"), @"{0} - {1}", start, end);

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
