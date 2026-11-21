// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class NotificationsStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.Notifications";

        /// <summary>
        /// "notifications"
        /// </summary>
        public static LocalisableString HeaderTitle => new TranslatableString(getKey(@"header_title"), @"notifications");

        /// <summary>
        /// "waiting for &#39;ya"
        /// </summary>
        public static LocalisableString HeaderDescription => new TranslatableString(getKey(@"header_description"), @"waiting for 'ya");

        /// <summary>
        /// "Running Tasks"
        /// </summary>
        public static LocalisableString RunningTasks => new TranslatableString(getKey(@"running_tasks"), @"Running Tasks");

        /// <summary>
        /// "Clear All"
        /// </summary>
        public static LocalisableString ClearAll => new TranslatableString(getKey(@"clear_all"), @"Clear All");

        /// <summary>
        /// "Your battery level is low! Charge your device to prevent interruptions during gameplay."
        /// </summary>
        public static LocalisableString BatteryLow => new TranslatableString(getKey(@"battery_low"), @"Your battery level is low! Charge your device to prevent interruptions during gameplay.");

        /// <summary>
        /// "Your device is running on battery power. Connect the power adapter for consistent gameplay performance."
        /// </summary>
        public static LocalisableString PowerNotConnected => new TranslatableString(getKey(@"power_not_connected"), @"Your device is running on battery power. Connect the power adapter for consistent gameplay performance.");

        /// <summary>
        /// "The power adapter has been disconnected. Your device is now running on battery power, which may affect gameplay performance."
        /// </summary>
        public static LocalisableString PowerDisconnected => new TranslatableString(getKey(@"power_disconnected"), @"The power adapter has been disconnected. Your device is now running on battery power, which may affect gameplay performance.");

        /// <summary>
        /// "Your game volume is too low to hear anything! Click here to restore it."
        /// </summary>
        public static LocalisableString GameVolumeTooLow => new TranslatableString(getKey(@"game_volume_too_low"), @"Your game volume is too low to hear anything! Click here to restore it.");

        /// <summary>
        /// "The current ruleset doesn&#39;t have an autoplay mod available!"
        /// </summary>
        public static LocalisableString NoAutoplayMod => new TranslatableString(getKey(@"no_autoplay_mod"), @"The current ruleset doesn't have an autoplay mod available!");

        /// <summary>
        /// "g0v0! doesn&#39;t seem to be able to play audio correctly.
        ///
        /// Please try changing your audio device to a working setting."
        /// </summary>
        public static LocalisableString AudioPlaybackIssue => new TranslatableString(getKey(@"audio_playback_issue"), @"g0v0! doesn't seem to be able to play audio correctly.

Please try changing your audio device to a working setting.");

        /// <summary>
        /// "The score overlay is currently disabled. You can toggle this by pressing {0}."
        /// </summary>
        public static LocalisableString ScoreOverlayDisabled(LocalisableString arg0) => new TranslatableString(getKey(@"score_overlay_disabled"), @"The score overlay is currently disabled. You can toggle this by pressing {0}.", arg0);

        /// <summary>
        /// "The URL {0} has an unsupported or dangerous protocol and will not be opened."
        /// </summary>
        public static LocalisableString UnsupportedOrDangerousUrlProtocol(string url) => new TranslatableString(getKey(@"unsupported_or_dangerous_url_protocol"), @"The URL {0} has an unsupported or dangerous protocol and will not be opened.", url);

        /// <summary>
        /// "Subsequent messages have been logged. Click to view log files."
        /// </summary>
        public static LocalisableString SubsequentMessagesLogged => new TranslatableString(getKey(@"subsequent_messages_logged"), @"Subsequent messages have been logged. Click to view log files.");

        /// <summary>
        /// "Disabling tablet support due to error: &quot;{0}&quot;"
        /// </summary>
        public static LocalisableString TabletSupportDisabledDueToError(string message) => new TranslatableString(getKey(@"tablet_support_disabled_due_to_error"), @"Disabling tablet support due to error: ""{0}""", message);

        /// <summary>
        /// "Encountered tablet warning, your tablet may not function correctly. Click here for a list of all tablets supported."
        /// </summary>
        public static LocalisableString EncounteredTabletWarning => new TranslatableString(getKey(@"encountered_tablet_warning"), @"Encountered tablet warning, your tablet may not function correctly. Click here for a list of all tablets supported.");

        /// <summary>
        /// "This link type is not yet supported!"
        /// </summary>
        public static LocalisableString LinkTypeNotSupported => new TranslatableString(getKey(@"unsupported_link_type"), @"This link type is not yet supported!");

        /// <summary>
        /// "{0} invited you to the multiplayer match &quot;{1}&quot;! Click to join."
        /// </summary>
        public static LocalisableString InvitedYouToTheMultiplayer(string username, string roomName) => new TranslatableString(getKey(@"invited_you_to_the_multiplayer"), @"{0} invited you to the multiplayer match ""{1}""! Click to join.", username, roomName);

        /// <summary>
        /// "You do not have the beatmap for this replay."
        /// </summary>
        public static LocalisableString MissingBeatmapForReplay => new TranslatableString(getKey(@"missing_beatmap_for_replay"), @"You do not have the beatmap for this replay.");

        /// <summary>
        /// "Downloading missing beatmap for this replay..."
        /// </summary>
        public static LocalisableString DownloadingBeatmapForReplay => new TranslatableString(getKey(@"downloading_beatmap_for_replay"), @"Downloading missing beatmap for this replay...");

        /// <summary>
        /// "Your local copy of the beatmap for this replay appears to be different than expected. You may need to update or re-download it."
        /// </summary>
        public static LocalisableString MismatchingBeatmapForReplay => new TranslatableString(getKey(@"mismatching_beatmap_for_replay"), @"Your local copy of the beatmap for this replay appears to be different than expected. You may need to update or re-download it.");

        /// <summary>
        /// "You are now running g0v0! {0}.
        /// Click to see what&#39;s new!"
        /// </summary>
        public static LocalisableString GameVersionAfterUpdate(string version) => new TranslatableString(getKey(@"game_version_after_update"), @"You are now running g0v0! {0}.
Click to see what's new!", version);

        /// <summary>
        /// "Update ready to install. Click to restart!"
        /// </summary>
        public static LocalisableString UpdateReadyToInstall => new TranslatableString(getKey(@"update_ready_to_install"), @"Update ready to install. Click to restart!");

        /// <summary>
        /// "This is not an official build of the game. Scores will not be submitted and other online systems may not work as intended."
        /// </summary>
        public static LocalisableString NotOfficialBuild => new TranslatableString(getKey(@"not_official_build"), @"This is not an official build of the game. Scores will not be submitted and other online systems may not work as intended.");

        /// <summary>
        /// "Downloading update..."
        /// </summary>
        public static LocalisableString DownloadingUpdate => new TranslatableString(getKey(@"downloading_update"), @"Downloading update...");

        /// <summary>
        /// "This multiplayer room has ended. Click to display room results."
        /// </summary>
        public static LocalisableString MultiplayerRoomEnded => new TranslatableString(getKey(@"multiplayer_room_ended"), @"This multiplayer room has ended. Click to display room results.");

        /// <summary>
        /// "Mention"
        /// </summary>
        public static LocalisableString Mention => new TranslatableString(getKey(@"mention"), @"Mention");

        /// <summary>
        /// "{0} in {1}"
        /// </summary>
        public static LocalisableString MentionDetails(string user, string channelName) => new TranslatableString(getKey(@"mention_details"), @"{0} in {1}", user, channelName);

        /// <summary>
        /// "Online: {0}"
        /// </summary>
        public static LocalisableString FriendOnline(string info) => new TranslatableString(getKey(@"friend_online"), @"Online: {0}", info);

        /// <summary>
        /// "Offline: {0}"
        /// </summary>
        public static LocalisableString FriendOffline(string info) => new TranslatableString(getKey(@"friend_offline"), @"Offline: {0}", info);

        /// <summary>
        /// "Connection to online services was interrupted. g0v0! will be operating with limited functionality."
        /// </summary>
        public static LocalisableString APIConnectionInterrupted => new TranslatableString(getKey(@"api_connection_interrupted"), @"Connection to online services was interrupted. g0v0! will be operating with limited functionality.");

        /// <summary>
        /// "You have been logged out on this device due to a login to your account on another device."
        /// </summary>
        public static LocalisableString AnotherDeviceDisconnect => new TranslatableString(getKey(@"another_device_disconnect"), @"You have been logged out on this device due to a login to your account on another device.");

        /// <summary>
        /// "You have been logged out due to a change to your account. Please log in again."
        /// </summary>
        public static LocalisableString AccountChangeDisconnect => new TranslatableString(getKey(@"account_change_disconnect"), @"You have been logged out due to a change to your account. Please log in again.");

        /// <summary>
        /// "Downloading {0}"
        /// </summary>
        public static LocalisableString Downloading(string info) => new TranslatableString(getKey(@"downloading"), @"Downloading {0}", info);

        /// <summary>
        /// "Collections import is initialising..."
        /// </summary>
        public static LocalisableString CollectionsImportInitialising => new TranslatableString(getKey(@"collections_import_initialising"), @"Collections import is initialising...");

        /// <summary>
        /// "Reading collections..."
        /// </summary>
        public static LocalisableString ReadingCollections => new TranslatableString(getKey(@"reading_collections"), @"Reading collections...");

        /// <summary>
        /// "Imported {0} collections"
        /// </summary>
        public static LocalisableString CollectionsImportProgress(int count) => new TranslatableString(getKey(@"collections_import_progress"), @"Imported {0} collections", count);

        /// <summary>
        /// "Imported {0} of {1} collections"
        /// </summary>
        public static LocalisableString CollectionsImportProgressTotal(int count, int totalCount) => new TranslatableString(getKey(@"collections_import_progress_total"), @"Imported {0} of {1} collections", count, totalCount);

        /// <summary>
        /// "This error has been automatically reported to the dev team."
        /// </summary>
        public static LocalisableString ErrorAutomaticallyReported => new TranslatableString(getKey(@"error_automatically_reported"), @"This error has been automatically reported to the dev team.");

        /// <summary>
        /// "A newer release of g0v0! has been found ({0} → {1})."
        /// </summary>
        public static LocalisableString UpdateAvailable(string oldVersion, string newVersion) => new TranslatableString(getKey(@"update_available"), @"A newer release of g0v0! has been found ({0} → {1}).", oldVersion, newVersion);

        /// <summary>
        /// "Click here to download the new version, which can be installed over the top of your existing installation."
        /// </summary>
        public static LocalisableString UpdateAvailableManualInstall => new TranslatableString(getKey(@"update_available_manual_install"), @"Click here to download the new version, which can be installed over the top of your existing installation.");

        /// <summary>
        /// "Check with your package manager / provider to bring g0v0! up-to-date!"
        /// </summary>
        public static LocalisableString UpdateAvailablePackageManaged => new TranslatableString(getKey(@"update_available_package_managed"), @"Check with your package manager / provider to bring g0v0! up-to-date!");

        /// <summary>
        /// "An action was interrupted due to a dialog being displayed."
        /// </summary>
        public static LocalisableString ActionInterruptedByDialog => new TranslatableString(getKey(@"action_interrupted_by_dialog"), @"An action was interrupted due to a dialog being displayed.");

        /// <summary>
        /// "Exporting {0}..."
        /// </summary>
        public static LocalisableString FileExportOngoing(string filename) => new TranslatableString(getKey(@"file_export_ongoing"), @"Exporting {0}...", filename);

        /// <summary>
        /// "Exported {0}! Click to view."
        /// </summary>
        public static LocalisableString FileExportFinished(string filename) => new TranslatableString(getKey(@"file_export_finished"), @"Exported {0}! Click to view.", filename);

        /// <summary>
        /// "Exporting logs..."
        /// </summary>
        public static LocalisableString LogsExportOngoing => new TranslatableString(getKey(@"logs_export_ongoing"), @"Exporting logs...");

        /// <summary>
        /// "Exported logs! Click to view."
        /// </summary>
        public static LocalisableString LogsExportFinished => new TranslatableString(getKey(@"logs_export_finished"), @"Exported logs! Click to view.");

        /// <summary>
        /// "Running g0v0! as {0} does not improve performance, may break integrations and poses a security risk. Please run the game as a normal user."
        /// </summary>
        public static LocalisableString ElevatedPrivileges(LocalisableString user) => new TranslatableString(getKey(@"elevated_privileges"), @"Running g0v0! as {0} does not improve performance, may break integrations and poses a security risk. Please run the game as a normal user.", user);

        /// <summary>
        /// "On macOS, installing g0v0! to a directory other than /Applications or {0}/Applications can cause issues with updating the game. Please move your game installation to one of these locations."
        /// </summary>
        public static LocalisableString MacOSAppLocation(LocalisableString userProfile) => new TranslatableString(getKey(@"macos_app_location"), @"On macOS, installing g0v0! to a directory other than /Applications or {0}/Applications can cause issues with updating the game. Please move your game installation to one of these locations.", userProfile);

        /// <summary>
        /// "Screenshot saved! Click to view.
        /// {0}"
        /// </summary>
        public static LocalisableString ScreenshotSaved(string filename) => new TranslatableString(getKey(@"screenshot_saved"), @"Screenshot saved! Click to view.
{0}", filename);

        /// <summary>
        /// "Uploading screenshot..."
        /// </summary>
        public static LocalisableString UploadingScreenshot => new TranslatableString(getKey(@"uploading_screenshot"), @"Uploading screenshot...");

        /// <summary>
        /// "Screenshot uploaded! The link has been copied to the clipboard."
        /// </summary>
        public static LocalisableString ScreenshotUploadSuccess => new TranslatableString(getKey(@"screenshot_upload_success"), @"Screenshot uploaded! The link has been copied to the clipboard.");

        /// <summary>
        /// "Failed to upload screenshot."
        /// </summary>
        public static LocalisableString ScreenshotUploadFailure => new TranslatableString(getKey(@"screenshot_upload_failure"), @"Failed to upload screenshot.");

        /// <summary>
        /// "You are trying to upload screenshots too fast!"
        /// </summary>
        public static LocalisableString ScreenshotTooManyUploads => new TranslatableString(getKey(@"screenshot_too_many_uploads"), @"You are trying to upload screenshots too fast!");

        /// <summary>
        /// "The multiplayer server will be right back..."
        /// </summary>
        public static LocalisableString MultiplayerServerShuttingDownImmediately => new TranslatableString(getKey(@"multiplayer_server_shutting_down_immediately"), @"The multiplayer server will be right back...");

        /// <summary>
        /// "The multiplayer server is restarting in {0}."
        /// </summary>
        public static LocalisableString MultiplayerServerShuttingDownRemaining(string remainingTime) => new TranslatableString(getKey(@"multiplayer_server_shutting_down_remaining"), @"The multiplayer server is restarting in {0}.", remainingTime);

        /// <summary>
        /// "Created new collection &quot;{0}&quot; with {1} beatmaps."
        /// </summary>
        public static LocalisableString CollectionCreated(string name, int beatmapsCount) => new TranslatableString(getKey(@"collection_created"), @"Created new collection ""{0}"" with {1} beatmaps.", name, beatmapsCount);

        /// <summary>
        /// "Added {0} beatmaps to collection &quot;{1}&quot;."
        /// </summary>
        public static LocalisableString CollectionBeatmapsAdded(string name, int beatmapsCount) => new TranslatableString(getKey(@"collection_beatmaps_added"), @"Added {0} beatmaps to collection ""{1}"".", beatmapsCount, name);

        /// <summary>
        /// "Ruleset error"
        /// </summary>
        public static LocalisableString RulesetError => new TranslatableString(getKey(@"ruleset_error"), @"Ruleset error");

        /// <summary>
        /// "The following rulesets could not be loaded:"
        /// </summary>
        public static LocalisableString RulesetUnableToLoad =>
            new TranslatableString(getKey(@"ruleset_unable_to_load"), @"The following rulesets could not be loaded:");

        /// <summary>
        /// "There are other exceptions caused by unknown sources. View the log for details."
        /// </summary>
        public static LocalisableString RulesetUnknownException =>
            new TranslatableString(getKey(@"ruleset_unknown_exception"),
                @"There are other exceptions caused by unknown sources. View the log for details.");

        /// <summary>
        /// "You may ask for updates from their developers, or report issues to them."
        /// </summary>
        public static LocalisableString RulesetLoadSuggestion =>
            new TranslatableString(getKey(@"ruleset_load_suggestion"), @"You may ask for updates from their developers, or report issues to them.");

        /// <summary>
        /// "Something went wrong with the ruleset &quot;{0}&quot;."
        /// </summary>
        public static LocalisableString RulesetException(string name) =>
            new TranslatableString(getKey(@"ruleset_exception"), @"Something went wrong with the ruleset ""{0}"".", name);

        /// <summary>
        /// "It seems that the ruleset &quot;{0}&quot; caused the game to crash, and we have temporarily disabled it to avoid further problems."
        /// </summary>
        public static LocalisableString RulesetCausedCrash(string name)
            => new TranslatableString(getKey(@"ruleset_caused_crash"), @"It seems that the ruleset ""{0}"" caused the game to crash, and we have temporarily disabled it to avoid further problems.", name);

        /// <summary>
        /// "You can restore it anytime from the settings, but it's suggested to report this issue to the ruleset's developer first."
        /// </summary>
        public static LocalisableString RulesetCrashSuggestion
            => new TranslatableString(getKey(@"ruleset_crash_suggestion"), @"You can restore it anytime from the settings, but it's suggested to report this issue to the ruleset's developer first.");

        /// <summary>
        /// "Boom!"
        /// </summary>
        public static LocalisableString ExceptionTitle => new TranslatableString(getKey(@"exception_title"), @"Boom!");

        /// <summary>
        /// "Your device has an older processor. The game may stutter."
        /// </summary>
        public static LocalisableString LimitedProcessorAcceleration => new TranslatableString(getKey(@"limited_processor_acceleration"), @"Your device has an older processor. The game may stutter.");

        /// <summary>
        /// "Cannot start play"
        /// </summary>
        public static LocalisableString CannotStartPlay => new TranslatableString(getKey(@"cannot_start_play"), @"Cannot start play");

        /// <summary>
        /// "Score will not be submitted"
        /// </summary>
        public static LocalisableString ScoreWillNotBeSubmitted => new TranslatableString(getKey(@"score_will_not_be_submitted"), @"Score will not be submitted");

        /// <summary>
        /// "Failed to retrieve a score submission token."
        /// </summary>
        public static LocalisableString FailedToRetrieveScoreSubmissionToken => new TranslatableString(getKey(@"failed_to_retrieve_score_submission_token"), @"Failed to retrieve a score submission token.");

        /// <summary>
        /// "Please ensure that you are using the latest version of the official game releases."
        /// </summary>
        public static LocalisableString PreventSubmittingWithOutdatedClient => new TranslatableString(getKey(@"prevent_submitting_with_outdated_client"), @"Please ensure that you are using the latest version of the official game releases.");

        /// <summary>
        /// "This beatmap does not match the online version. Please update or redownload it."
        /// </summary>
        public static LocalisableString PreventSubmittingWithInvalidBeatmapHash => new TranslatableString(getKey(@"prevent_submitting_with_invalid_beatmap_hash"), @"This beatmap does not match the online version. Please update or redownload it.");

        /// <summary>
        /// "Your system clock is set incorrectly. Please check your system time, date and timezone."
        /// </summary>
        public static LocalisableString PreventSubmittingWithExpiredToken => new TranslatableString(getKey(@"prevent_submitting_with_expired_token"), @"Your system clock is set incorrectly. Please check your system time, date and timezone.");

        /// <summary>
        /// "Online functionality is not available."
        /// </summary>
        public static LocalisableString OnlineFunctionalityIsNotAvailable => new TranslatableString(getKey(@"online_functionality_is_not_available"), @"Online functionality is not available.");

        /// <summary>
        /// "Server outage in progress"
        /// </summary>
        public static LocalisableString ServerOutageInProgress => new TranslatableString(getKey(@"server_outage_in_progress"), @"Server outage in progress");

        /// <summary>
        /// "Online functionality is not available due to an outage. Sorry for the inconvenience."
        /// </summary>
        public static LocalisableString ServerOutageExplanation => new TranslatableString(getKey(@"server_outage_explanation"), @"Online functionality is not available due to an outage. Sorry for the inconvenience.");

        /// <summary>
        /// "{0} challenged you to a duel ({1}). Click to accept."
        /// </summary>
        public static LocalisableString UserChallengedYouToDuel(string username, string poolName) => new TranslatableString(getKey(@"user_challenged_you_to_duel"), @"{0} challenged you to a duel ({1}). Click to accept.", username, poolName);

        /// <summary>
        /// "Task has completed!"
        /// </summary>
        public static LocalisableString TaskCompleted => new TranslatableString(getKey(@"task_completed"), @"Task has completed!");

        /// <summary>
        /// "Preparing to delete all {0}s..."
        /// </summary>
        public static LocalisableString PreparingToDeleteAll(LocalisableString modelName) => new TranslatableString(getKey(@"preparing_to_delete_all"), @"Preparing to delete all {0}s...", modelName);

        /// <summary>
        /// "Deleting {0}s ({1} of {2})"
        /// </summary>
        public static LocalisableString DeletingModels(LocalisableString modelName, int index, int totalCount) => new TranslatableString(getKey(@"deleting_models"), @"Deleting {0}s ({1} of {2})", modelName, index, totalCount);

        /// <summary>
        /// "Deleted all {0}s!"
        /// </summary>
        public static LocalisableString DeletedAll(LocalisableString modelName) => new TranslatableString(getKey(@"deleted_all"), @"Deleted all {0}s!", modelName);

        /// <summary>
        /// "No {0}s found to delete!"
        /// </summary>
        public static LocalisableString NoModelsFoundToDelete(LocalisableString modelName) => new TranslatableString(getKey(@"no_models_found_to_delete"), @"No {0}s found to delete!", modelName);

        /// <summary>
        /// "No {0}s found to restore!"
        /// </summary>
        public static LocalisableString NoModelsFoundToRestore(LocalisableString modelName) => new TranslatableString(getKey(@"no_models_found_to_restore"), @"No {0}s found to restore!", modelName);

        /// <summary>
        /// "Restoring ({0} of {1})"
        /// </summary>
        public static LocalisableString RestoringModels(int index, int totalCount) => new TranslatableString(getKey(@"restoring_models"), @"Restoring ({0} of {1})", index, totalCount);

        /// <summary>
        /// "Restored all deleted items!"
        /// </summary>
        public static LocalisableString RestoredAllDeletedItems => new TranslatableString(getKey(@"restored_all_deleted_items"), @"Restored all deleted items!");

        /// <summary>
        /// "Preparing to delete all {0} videos..."
        /// </summary>
        public static LocalisableString PreparingToDeleteAllVideos(LocalisableString modelName) => new TranslatableString(getKey(@"preparing_to_delete_all_videos"), @"Preparing to delete all {0} videos...", modelName);

        /// <summary>
        /// "Deleted {0} {1} video(s)!"
        /// </summary>
        public static LocalisableString DeletedVideos(int deletedCount, LocalisableString modelName) => new TranslatableString(getKey(@"deleted_videos"), @"Deleted {0} {1} video(s)!", deletedCount, modelName);

        /// <summary>
        /// "Deleting videos from {0}s ({1} deleted)"
        /// </summary>
        public static LocalisableString DeletingVideosFrom(LocalisableString modelName, int deletedCount) => new TranslatableString(getKey(@"deleting_videos_from"), @"Deleting videos from {0}s ({1} deleted)", modelName, deletedCount);

        /// <summary>
        /// "beatmap"
        /// </summary>
        public static LocalisableString ModelBeatmap => new TranslatableString(getKey(@"model_beatmap"), @"beatmap");

        /// <summary>
        /// "score"
        /// </summary>
        public static LocalisableString ModelScore => new TranslatableString(getKey(@"model_score"), @"score");

        /// <summary>
        /// "skin"
        /// </summary>
        public static LocalisableString ModelSkin => new TranslatableString(getKey(@"model_skin"), @"skin");

        /// <summary>
        /// Returns a localisable name for the given humanised model name (see <c>IModelImporter.HumanisedModelName</c>).
        /// Falls back to the raw name when no translation is available.
        /// </summary>
        public static LocalisableString ModelName(string humanisedName) => humanisedName switch
        {
            "beatmap" => ModelBeatmap,
            "score" => ModelScore,
            "skin" => ModelSkin,
            _ => humanisedName,
        };

        /// <summary>
        /// "No {0}s were found to import!"
        /// </summary>
        public static LocalisableString NoModelsFoundToImport(LocalisableString modelName) => new TranslatableString(getKey(@"no_models_found_to_import"), @"No {0}s were found to import!", modelName);

        /// <summary>
        /// "{0} import is initialising..."
        /// </summary>
        public static LocalisableString ImportInitialising(LocalisableString modelName) => new TranslatableString(getKey(@"import_initialising"), @"{0} import is initialising...", modelName);

        /// <summary>
        /// "Imported {0} of {1} {2}s"
        /// </summary>
        public static LocalisableString ImportedXOfY(int importedCount, int totalCount, LocalisableString modelName) => new TranslatableString(getKey(@"imported_x_of_y"), @"Imported {0} of {1} {2}s", importedCount, totalCount, modelName);

        /// <summary>
        /// "{0} import failed! Check logs for more information."
        /// </summary>
        public static LocalisableString ImportFailed(LocalisableString modelName) => new TranslatableString(getKey(@"import_failed"), @"{0} import failed! Check logs for more information.", modelName);

        /// <summary>
        /// "Imported {0} of {1} {2}s."
        /// </summary>
        public static LocalisableString ImportedXOfYCompleted(int importedCount, int totalCount, LocalisableString modelName) => new TranslatableString(getKey(@"imported_x_of_y_completed"), @"Imported {0} of {1} {2}s.", importedCount, totalCount, modelName);

        /// <summary>
        /// "Imported {0} {1}s!"
        /// </summary>
        public static LocalisableString ImportedCount(int importedCount, LocalisableString modelName) => new TranslatableString(getKey(@"imported_count"), @"Imported {0} {1}s!", importedCount, modelName);

        /// <summary>
        /// "Imported {0}!"
        /// </summary>
        public static LocalisableString ImportedNamed(string displayName) => new TranslatableString(getKey(@"imported_named"), @"Imported {0}!", displayName);

        /// <summary>
        /// "{0} import is paused due to gameplay..."
        /// </summary>
        public static LocalisableString ImportPaused(LocalisableString modelName) => new TranslatableString(getKey(@"import_paused"), @"{0} import is paused due to gameplay...", modelName);

        /// <summary>
        /// "{0} import is resuming..."
        /// </summary>
        public static LocalisableString ImportResuming(LocalisableString modelName) => new TranslatableString(getKey(@"import_resuming"), @"{0} import is resuming...", modelName);

        /// <summary>
        /// "{0} Click to view."
        /// </summary>
        public static LocalisableString WithClickToView(LocalisableString completionText) => new TranslatableString(getKey(@"with_click_to_view"), @"{0} Click to view.", completionText);

        /// <summary>
        /// "Failed to join room."
        /// </summary>
        public static LocalisableString FailedToJoinRoom => new TranslatableString(getKey(@"failed_to_join_room"), @"Failed to join room.");

        /// <summary>
        /// "Reprocessing star rating for beatmaps"
        /// </summary>
        public static LocalisableString ReprocessingStarRatings => new TranslatableString(getKey(@"reprocessing_star_ratings"), @"Reprocessing star rating for beatmaps");

        /// <summary>
        /// "beatmaps' star ratings have been updated"
        /// </summary>
        public static LocalisableString StarRatingsUpdated => new TranslatableString(getKey(@"star_ratings_updated"), @"beatmaps' star ratings have been updated");

        /// <summary>
        /// "Updating online data for beatmaps"
        /// </summary>
        public static LocalisableString UpdatingOnlineData => new TranslatableString(getKey(@"updating_online_data"), @"Updating online data for beatmaps");

        /// <summary>
        /// "beatmaps' online data have been updated"
        /// </summary>
        public static LocalisableString OnlineDataUpdated => new TranslatableString(getKey(@"online_data_updated"), @"beatmaps' online data have been updated");

        /// <summary>
        /// "Populating missing statistics for beatmaps"
        /// </summary>
        public static LocalisableString PopulatingBeatmapStatistics => new TranslatableString(getKey(@"populating_beatmap_statistics"), @"Populating missing statistics for beatmaps");

        /// <summary>
        /// "beatmaps have been populated with missing statistics"
        /// </summary>
        public static LocalisableString BeatmapStatisticsPopulated => new TranslatableString(getKey(@"beatmap_statistics_populated"), @"beatmaps have been populated with missing statistics");

        /// <summary>
        /// "Populating missing statistics for scores"
        /// </summary>
        public static LocalisableString PopulatingScoreStatistics => new TranslatableString(getKey(@"populating_score_statistics"), @"Populating missing statistics for scores");

        /// <summary>
        /// "scores have been populated with missing statistics"
        /// </summary>
        public static LocalisableString ScoreStatisticsPopulated => new TranslatableString(getKey(@"score_statistics_populated"), @"scores have been populated with missing statistics");

        /// <summary>
        /// "Upgrading scores to new mod multipliers"
        /// </summary>
        public static LocalisableString UpgradingModMultipliers => new TranslatableString(getKey(@"upgrading_mod_multipliers"), @"Upgrading scores to new mod multipliers");

        /// <summary>
        /// "scores have been upgraded to the new mod multipliers"
        /// </summary>
        public static LocalisableString ModMultipliersUpgraded => new TranslatableString(getKey(@"mod_multipliers_upgraded"), @"scores have been upgraded to the new mod multipliers");

        /// <summary>
        /// "Upgrading scores to new scoring algorithm"
        /// </summary>
        public static LocalisableString UpgradingScoringAlgorithm => new TranslatableString(getKey(@"upgrading_scoring_algorithm"), @"Upgrading scores to new scoring algorithm");

        /// <summary>
        /// "scores have been upgraded to the new scoring algorithm"
        /// </summary>
        public static LocalisableString ScoringAlgorithmUpgraded => new TranslatableString(getKey(@"scoring_algorithm_upgraded"), @"scores have been upgraded to the new scoring algorithm");

        /// <summary>
        /// "Adjusting ranks of scores"
        /// </summary>
        public static LocalisableString AdjustingScoreRanks => new TranslatableString(getKey(@"adjusting_score_ranks"), @"Adjusting ranks of scores");

        /// <summary>
        /// "scores now have more correct ranks."
        /// </summary>
        public static LocalisableString ScoreRanksAdjusted => new TranslatableString(getKey(@"score_ranks_adjusted"), @"scores now have more correct ranks.");

        /// <summary>
        /// "Populating missing submission and rank dates"
        /// </summary>
        public static LocalisableString PopulatingSubmissionDates => new TranslatableString(getKey(@"populating_submission_dates"), @"Populating missing submission and rank dates");

        /// <summary>
        /// "beatmap sets now have correct submission and rank dates."
        /// </summary>
        public static LocalisableString SubmissionDatesPopulated => new TranslatableString(getKey(@"submission_dates_populated"), @"beatmap sets now have correct submission and rank dates.");

        /// <summary>
        /// "Updating user tags"
        /// </summary>
        public static LocalisableString UpdatingUserTags => new TranslatableString(getKey(@"updating_user_tags"), @"Updating user tags");

        /// <summary>
        /// "beatmaps have had their tags updated. This runs once a month to allow searching user tags."
        /// </summary>
        public static LocalisableString UserTagsUpdated => new TranslatableString(getKey(@"user_tags_updated"), @"beatmaps have had their tags updated. This runs once a month to allow searching user tags.");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
