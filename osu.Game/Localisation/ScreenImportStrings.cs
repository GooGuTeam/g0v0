// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ScreenImportStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.ScreenImport";

        /// <summary>
        /// "Import from"
        /// </summary>
        public static LocalisableString SourceLabel => new TranslatableString(getKey(@"source_label"), @"Import from");

        /// <summary>
        /// "stable installation"
        /// </summary>
        public static LocalisableString SourceStable => new TranslatableString(getKey(@"source_stable"), @"stable installation");

        /// <summary>
        /// "another g0v0! or lazer installation"
        /// </summary>
        public static LocalisableString SourceLazer => new TranslatableString(getKey(@"source_lazer"), @"another g0v0! or lazer installation");

        /// <summary>
        /// "You can also migrate content from another g0v0! or lazer installation. Select its data folder (the one which contains &quot;client.realm&quot;)."
        /// </summary>
        public static LocalisableString LazerDescription => new TranslatableString(getKey(@"lazer_description"), @"You can also migrate content from another g0v0! or lazer installation. Select its data folder (the one which contains ""client.realm"").");

        /// <summary>
        /// "previous installation"
        /// </summary>
        public static LocalisableString LocateDirectoryLabel => new TranslatableString(getKey(@"locate_directory_label"), @"previous installation");

        /// <summary>
        /// "Click to locate a previous installation"
        /// </summary>
        public static LocalisableString LocateDirectoryPlaceholder => new TranslatableString(getKey(@"locate_directory_placeholder"), @"Click to locate a previous installation");

        /// <summary>
        /// "A second copy of all files will be made during import. To avoid this, please make sure the g0v0! data folder is on the same drive as the installation you are importing from (and the file system supports hard links)."
        /// </summary>
        public static LocalisableString SecondCopyWillBeMade => new TranslatableString(getKey(@"second_copy_will_be_made"), @"A second copy of all files will be made during import. To avoid this, please make sure the g0v0! data folder is on the same drive as the installation you are importing from (and the file system supports hard links).");

        /// <summary>
        /// "The database of the selected installation could not be read. It may have been created by an incompatible version."
        /// </summary>
        public static LocalisableString DatabaseUnreadable => new TranslatableString(getKey(@"database_unreadable"), @"The database of the selected installation could not be read. It may have been created by an incompatible version.");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
