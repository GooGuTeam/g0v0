// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class UserInterfaceStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.UserInterface";

        /// <summary>
        /// "Strictly vertical UI (no slant)"
        /// </summary>
        public static LocalisableString UnslantedSongSelectUI => new TranslatableString(getKey(@"unslanted_song_select_ui"), @"Strictly vertical UI (no slant)");

        /// <summary>
        /// "Re-render the slanted Song Select panels (wedges, leaderboard, dropdowns) as straight rectangles. Takes effect on next entry to song select."
        /// </summary>
        public static LocalisableString UnslantedSongSelectUIDescription => new TranslatableString(getKey(@"unslanted_song_select_ui_description"), @"Re-render the slanted Song Select panels (wedges, leaderboard, dropdowns) as straight rectangles. Takes effect on next entry to song select.");

        /// <summary>
        /// "User Interface"
        /// </summary>
        public static LocalisableString UserInterfaceSectionHeader => new TranslatableString(getKey(@"user_interface_section_header"), @"User Interface");

        /// <summary>
        /// "Rotate cursor when dragging"
        /// </summary>
        public static LocalisableString CursorRotation => new TranslatableString(getKey(@"cursor_rotation"), @"Rotate cursor when dragging");

        /// <summary>
        /// "Menu cursor size"
        /// </summary>
        public static LocalisableString MenuCursorSize => new TranslatableString(getKey(@"menu_cursor_size"), @"Menu cursor size");

        /// <summary>
        /// "Menu tips"
        /// </summary>
        public static LocalisableString ShowMenuTips => new TranslatableString(getKey(@"show_menu_tips"), @"Menu tips");

        /// <summary>
        /// "Parallax"
        /// </summary>
        public static LocalisableString Parallax => new TranslatableString(getKey(@"parallax"), @"Parallax");

        /// <summary>
        /// "Hold-to-confirm activation time"
        /// </summary>
        public static LocalisableString HoldToConfirmActivationTime => new TranslatableString(getKey(@"hold_to_confirm_activation_time"), @"Hold-to-confirm activation time");

        /// <summary>
        /// "Main Menu"
        /// </summary>
        public static LocalisableString MainMenuHeader => new TranslatableString(getKey(@"main_menu_header"), @"Main Menu");

        /// <summary>
        /// "Interface voices"
        /// </summary>
        public static LocalisableString InterfaceVoices => new TranslatableString(getKey(@"interface_voices"), @"Interface voices");

        /// <summary>
        /// "g0v0! music theme"
        /// </summary>
        public static LocalisableString OsuMusicTheme => new TranslatableString(getKey(@"osu_music_theme"), @"g0v0! music theme");

        /// <summary>
        /// "Intro sequence"
        /// </summary>
        public static LocalisableString IntroSequence => new TranslatableString(getKey(@"intro_sequence"), @"Intro sequence");

        /// <summary>
        /// "Random"
        /// </summary>
        public static LocalisableString IntroRandom => new TranslatableString(getKey(@"intro_random"), @"Random");

        /// <summary>
        /// "Background source"
        /// </summary>
        public static LocalisableString BackgroundSource => new TranslatableString(getKey(@"background_source"), @"Background source");

        /// <summary>
        /// "Seasonal backgrounds"
        /// </summary>
        public static LocalisableString SeasonalBackgrounds => new TranslatableString(getKey(@"seasonal_backgrounds"), @"Seasonal backgrounds");

        /// <summary>
        /// "Changes to this setting will only apply with an active osu!supporter tag."
        /// </summary>
        public static LocalisableString NotSupporterNote => new TranslatableString(getKey(@"not_supporter_note"), @"Changes to this setting will only apply with an active osu!supporter tag.");

        /// <summary>
        /// "Song Select"
        /// </summary>
        public static LocalisableString SongSelectHeader => new TranslatableString(getKey(@"song_select_header"), @"Song Select");

        /// <summary>
        /// "Right mouse drag to absolute scroll"
        /// </summary>
        public static LocalisableString RightMouseScroll => new TranslatableString(getKey(@"right_mouse_scroll"), @"Right mouse drag to absolute scroll");

        /// <summary>
        /// "Show converts"
        /// </summary>
        public static LocalisableString ShowConverts => new TranslatableString(getKey(@"show_converts"), @"Show converts");

        /// <summary>
        /// "Show converted beatmaps"
        /// </summary>
        public static LocalisableString ShowConvertedBeatmaps => new TranslatableString(getKey(@"show_converted_beatmaps"), @"Show converted beatmaps");

        /// <summary>
        /// "Display beatmaps from"
        /// </summary>
        public static LocalisableString StarsMinimum => new TranslatableString(getKey(@"stars_minimum"), @"Display beatmaps from");

        /// <summary>
        /// "up to"
        /// </summary>
        public static LocalisableString StarsMaximum => new TranslatableString(getKey(@"stars_maximum"), @"up to");

        /// <summary>
        /// "Random selection algorithm"
        /// </summary>
        public static LocalisableString RandomSelectionAlgorithm => new TranslatableString(getKey(@"random_selection_algorithm"), @"Random selection algorithm");

        /// <summary>
        /// "Mod select hotkey style"
        /// </summary>
        public static LocalisableString ModSelectHotkeyStyle => new TranslatableString(getKey(@"mod_select_hotkey_style"), @"Mod select hotkey style");

        /// <summary>
        /// "Automatically focus search text box in mod select"
        /// </summary>
        public static LocalisableString ModSelectTextSearchStartsActive => new TranslatableString(getKey(@"mod_select_text_search_starts_active"), @"Automatically focus search text box in mod select");

        /// <summary>
        /// "no limit"
        /// </summary>
        public static LocalisableString NoLimit => new TranslatableString(getKey(@"no_limit"), @"no limit");

        /// <summary>
        /// "Beatmap (with storyboard / video)"
        /// </summary>
        public static LocalisableString BeatmapWithStoryboard => new TranslatableString(getKey(@"beatmap_with_storyboard"), @"Beatmap (with storyboard / video)");

        /// <summary>
        /// "Always"
        /// </summary>
        public static LocalisableString AlwaysSeasonalBackground => new TranslatableString(getKey(@"always_seasonal_backgrounds"), @"Always");

        /// <summary>
        /// "Never"
        /// </summary>
        public static LocalisableString NeverSeasonalBackground => new TranslatableString(getKey(@"never_seasonal_backgrounds"), @"Never");

        /// <summary>
        /// "Sometimes"
        /// </summary>
        public static LocalisableString SometimesSeasonalBackground => new TranslatableString(getKey(@"sometimes_seasonal_backgrounds"), @"Sometimes");

        /// <summary>
        /// "Sequential"
        /// </summary>
        public static LocalisableString SequentialHotkeyStyle => new TranslatableString(getKey(@"mods_sequential_hotkeys"), @"Sequential");

        /// <summary>
        /// "Classic"
        /// </summary>
        public static LocalisableString ClassicHotkeyStyle => new TranslatableString(getKey(@"mods_classic_hotkeys"), @"Classic");

        /// <summary>
        /// "Never repeat"
        /// </summary>
        public static LocalisableString NeverRepeat => new TranslatableString(getKey(@"never_repeat_random"), @"Never repeat");

        /// <summary>
        /// "True random"
        /// </summary>
        public static LocalisableString TrueRandom => new TranslatableString(getKey(@"true_random"), @"True random");

        /// <summary>
        /// "Selected Mods"
        /// </summary>
        public static LocalisableString SelectedMods => new TranslatableString(getKey(@"selected_mods"), @"Selected Mods");

        /// <summary>
        /// "hold for menu"
        /// </summary>
        public static LocalisableString HoldForMenu => new TranslatableString(getKey(@"hold_for_menu"), @"hold for menu");

        /// <summary>
        /// "press for menu"
        /// </summary>
        public static LocalisableString PressForMenu => new TranslatableString(getKey(@"press_for_menu"), @"press for menu");

        /// <summary>
        /// "Device"
        /// </summary>
        public static LocalisableString Device => new TranslatableString(getKey(@"device"), @"Device");

        /// <summary>
        /// "Show hidden"
        /// </summary>
        public static LocalisableString ShowHidden => new TranslatableString(getKey(@"show_hidden"), @"Show hidden");

        /// <summary>
        /// "Currently online"
        /// </summary>
        public static LocalisableString CurrentlyOnline => new TranslatableString(getKey(@"currently_online"), @"Currently online");

        /// <summary>
        /// "User search"
        /// </summary>
        public static LocalisableString UserSearch => new TranslatableString(getKey(@"user_search"), @"User search");

        /// <summary>
        /// "Use V2 results screen"
        /// </summary>
        public static LocalisableString UseV2ResultsScreen => new TranslatableString(getKey(@"use_v2_results_screen"), @"Use V2 results screen");

        /// <summary>
        /// "Show solo results with a large score and rank ring. Disable to use the original layout."
        /// </summary>
        public static LocalisableString UseV2ResultsScreenDescription => new TranslatableString(getKey(@"use_v2_results_screen_description"), @"Show solo results with a large score and rank ring. Disable to use the original layout.");

        /// <summary>
        /// "Song Select Appearance"
        /// </summary>
        public static LocalisableString SongSelectAppearanceHeader => new TranslatableString(getKey(@"song_select_appearance_header"), @"Song Select Appearance");

        /// <summary>
        /// "Legacy song select"
        /// </summary>
        public static LocalisableString LegacySongSelect => new TranslatableString(getKey(@"legacy_song_select"), @"Legacy song select");

        /// <summary>
        /// "Use a stable-style song select with a skinnable footer and rank panel, without the modern filter bar or information wedges. Includes the legacy footer. Reopen song select to apply."
        /// </summary>
        public static LocalisableString LegacySongSelectDescription => new TranslatableString(getKey(@"legacy_song_select_description"), @"Use a stable-style song select with a skinnable footer and rank panel, without the modern filter bar or information wedges. Includes the legacy footer. Reopen song select to apply.");

        /// <summary>
        /// "Skin aspect ratio"
        /// </summary>
        public static LocalisableString LegacySongSelectAspectRatio => new TranslatableString(getKey(@"legacy_song_select_aspect_ratio"), @"Skin aspect ratio");

        /// <summary>
        /// "Use the same aspect ratio for legacy song select, loading, gameplay and legacy results to match your skin."
        /// </summary>
        public static LocalisableString LegacySongSelectAspectRatioDescription => new TranslatableString(getKey(@"legacy_song_select_aspect_ratio_description"), @"Use the same aspect ratio for legacy song select, loading, gameplay and legacy results to match your skin.");

        /// <summary>
        /// "Follow window (stable)"
        /// </summary>
        public static LocalisableString LegacySongSelectFollowWindow => new TranslatableString(getKey(@"legacy_song_select_follow_window"), @"Follow window (stable)");

        /// <summary>
        /// "Use Inter font in legacy song select"
        /// </summary>
        public static LocalisableString UseInterFont => new TranslatableString(getKey(@"use_inter_font"), @"Use Inter font in legacy song select");

        /// <summary>
        /// "Use Inter instead of the default UI font in legacy song select components. Reopen song select to apply."
        /// </summary>
        public static LocalisableString UseInterFontDescription => new TranslatableString(getKey(@"use_inter_font_description"), @"Use Inter instead of the default UI font in legacy song select components. Reopen song select to apply.");

        /// <summary>
        /// "Legacy song select footer"
        /// </summary>
        public static LocalisableString LegacySongSelectFooter => new TranslatableString(getKey(@"legacy_song_select_footer"), @"Legacy song select footer");

        /// <summary>
        /// "Show the stable-style footer and rank panel over the standard song select interface. Always enabled when legacy song select is on; your separate footer preference is preserved. Reopen song select to apply."
        /// </summary>
        public static LocalisableString LegacySongSelectFooterDescription => new TranslatableString(getKey(@"legacy_song_select_footer_description"), @"Show the stable-style footer and rank panel over the standard song select interface. Always enabled when legacy song select is on; your separate footer preference is preserved. Reopen song select to apply.");

        /// <summary>
        /// "Results"
        /// </summary>
        public static LocalisableString ResultsHeader => new TranslatableString(getKey(@"results_header"), @"Results");

        /// <summary>
        /// "Results screen style"
        /// </summary>
        public static LocalisableString ResultsScreenStyle => new TranslatableString(getKey(@"results_screen_style"), @"Results screen style");

        /// <summary>
        /// "Choose the default layout, the modern score and rank ring, or the skinnable stable-style ranking panel. Applies to newly opened results screens."
        /// </summary>
        public static LocalisableString ResultsScreenStyleDescription => new TranslatableString(getKey(@"results_screen_style_description"), @"Choose the default layout, the modern score and rank ring, or the skinnable stable-style ranking panel. Applies to newly opened results screens.");

        /// <summary>
        /// "Default"
        /// </summary>
        public static LocalisableString DefaultResultsStyle => new TranslatableString(getKey(@"default_results_style"), @"Default");

        /// <summary>
        /// "g0v0 V2"
        /// </summary>
        public static LocalisableString V2ResultsStyle => new TranslatableString(getKey(@"v2_results_style"), @"g0v0 V2");

        /// <summary>
        /// "Torii (Legacy)"
        /// </summary>
        public static LocalisableString LegacyResultsStyle => new TranslatableString(getKey(@"legacy_results_style"), @"Torii (Legacy)");

        /// <summary>
        /// "What do you want to do with this beatmap?"
        /// </summary>
        public static LocalisableString LegacyBeatmapOptionsPrompt => new TranslatableString(getKey(@"legacy_beatmap_options_prompt"), @"What do you want to do with this beatmap?");

        /// <summary>
        /// "Remove from Unplayed"
        /// </summary>
        public static LocalisableString LegacyBeatmapOptionsRemoveFromUnplayed => new TranslatableString(getKey(@"legacy_beatmap_options_remove_from_unplayed"), @"Remove from Unplayed");

        /// <summary>
        /// "Clear local scores"
        /// </summary>
        public static LocalisableString LegacyBeatmapOptionsClearLocalScores => new TranslatableString(getKey(@"legacy_beatmap_options_clear_local_scores"), @"Clear local scores");

        /// <summary>
        /// "Legacy main menu"
        /// </summary>
        public static LocalisableString LegacyMainMenu => new TranslatableString(getKey(@"legacy_main_menu"), @"Legacy main menu");

        /// <summary>
        /// "Use the osu!stable classic main menu with circular logo button, two-tier submenu, and top-right music controls."
        /// </summary>
        public static LocalisableString LegacyMainMenuDescription => new TranslatableString(getKey(@"legacy_main_menu_description"), @"Use the osu!stable classic main menu with circular logo button, two-tier submenu, and top-right music controls.");

        /// <summary>
        /// "Legacy mod select"
        /// </summary>
        public static LocalisableString LegacyModSelect => new TranslatableString(getKey(@"legacy_mod_select"), @"Legacy mod select");

        /// <summary>
        /// "Use the osu!stable classic 3-category mod selection dialog with score multiplier display and hotkeys."
        /// </summary>
        public static LocalisableString LegacyModSelectDescription => new TranslatableString(getKey(@"legacy_mod_select_description"), @"Use the osu!stable classic 3-category mod selection dialog with score multiplier display and hotkeys.");

        /// <summary>
        /// "Mod Customization"
        /// </summary>
        public static LocalisableString ModCustomizationTitle => new TranslatableString(getKey(@"mod_customization_title"), @"Mod Customization");

        /// <summary>
        /// "Difficulty Reduction"
        /// </summary>
        public static LocalisableString DifficultyReduction => new TranslatableString(getKey(@"difficulty_reduction"), @"Difficulty Reduction");

        /// <summary>
        /// "Difficulty Increase"
        /// </summary>
        public static LocalisableString DifficultyIncrease => new TranslatableString(getKey(@"difficulty_increase"), @"Difficulty Increase");

        /// <summary>
        /// "Special"
        /// </summary>
        public static LocalisableString SpecialMods => new TranslatableString(getKey(@"special_mods"), @"Special");

        /// <summary>
        /// "1. Reset all mods"
        /// </summary>
        public static LocalisableString ResetAllMods => new TranslatableString(getKey(@"reset_all_mods"), @"1. Reset all mods");

        /// <summary>
        /// "2. Close"
        /// </summary>
        public static LocalisableString CloseDialog => new TranslatableString(getKey(@"close_dialog"), @"2. Close");

        /// <summary>
        /// "Legacy options menu"
        /// </summary>
        public static LocalisableString LegacyOptions => new TranslatableString(getKey(@"legacy_options"), @"Legacy options menu");

        /// <summary>
        /// "Restore osu!stable's classic sliding options sidebar appearance and styling."
        /// </summary>
        public static LocalisableString LegacyOptionsDescription => new TranslatableString(getKey(@"legacy_options_description"), @"Restore osu!stable's classic sliding options sidebar appearance and styling.");

        /// <summary>
        /// "Restart the game to apply this change."
        /// </summary>
        public static LocalisableString LegacyOptionsRestartRequired => new TranslatableString(getKey(@"legacy_options_restart_required"), @"Restart the game to apply this change.");

        /// <summary>
        /// "Legacy FPS counter"
        /// </summary>
        public static LocalisableString LegacyFpsCounter => new TranslatableString(getKey(@"legacy_fps_counter"), @"Legacy FPS counter");

        /// <summary>
        /// "Show the frame rate counter as osu!stable draws it."
        /// </summary>
        public static LocalisableString LegacyFpsCounterDescription => new TranslatableString(getKey(@"legacy_fps_counter_description"), @"Show the frame rate counter as osu!stable draws it.");

        /// <summary>
        /// "Legacy gameplay screens"
        /// </summary>
        public static LocalisableString LegacyGameplayScreens => new TranslatableString(getKey(@"legacy_gameplay_screens"), @"Legacy gameplay screens");

        /// <summary>
        /// "Show the loading, pause and fail screens as osu!stable draws them, using the skin's pause and fail textures. Applies from the next play."
        /// </summary>
        public static LocalisableString LegacyGameplayScreensDescription => new TranslatableString(getKey(@"legacy_gameplay_screens_description"),
            @"Show the loading, pause and fail screens as osu!stable draws them, using the skin's pause and fail textures. Applies from the next play.");

        /// <summary>
        /// "Background dim:"
        /// </summary>
        public static LocalisableString LegacyVisualSettingsBackgroundDim => new TranslatableString(getKey(@"legacy_visual_settings_background_dim"), @"Background dim:");

        /// <summary>
        /// "{0}% dimmed"
        /// </summary>
        public static LocalisableString LegacyVisualSettingsDimTooltip(int percent) => new TranslatableString(getKey(@"legacy_visual_settings_dim_tooltip"), @"{0}% dimmed", percent);

        /// <summary>
        /// "Toggles"
        /// </summary>
        public static LocalisableString LegacyVisualSettingsToggles => new TranslatableString(getKey(@"legacy_visual_settings_toggles"), @"Toggles");

        /// <summary>
        /// "Disable storyboard"
        /// </summary>
        public static LocalisableString LegacyVisualSettingsDisableStoryboard => new TranslatableString(getKey(@"legacy_visual_settings_disable_storyboard"), @"Disable storyboard");

        /// <summary>
        /// "Ignore beatmap skin"
        /// </summary>
        public static LocalisableString LegacyVisualSettingsIgnoreBeatmapSkin => new TranslatableString(getKey(@"legacy_visual_settings_ignore_beatmap_skin"), @"Ignore beatmap skin");

        /// <summary>
        /// "Ignore beatmap hitsounds"
        /// </summary>
        public static LocalisableString LegacyVisualSettingsIgnoreBeatmapHitsounds => new TranslatableString(getKey(@"legacy_visual_settings_ignore_beatmap_hitsounds"), @"Ignore beatmap hitsounds");

        /// <summary>
        /// "Legacy gameplay leaderboard"
        /// </summary>
        public static LocalisableString LegacyGameplayLeaderboard => new TranslatableString(getKey(@"legacy_gameplay_leaderboard"), @"Legacy gameplay leaderboard");

        /// <summary>
        /// "Use osu!stable's scoreboard in legacy skins' default HUD. It shows during breaks, or throughout while the leaderboard is toggled on. Applies from the next play."
        /// </summary>
        public static LocalisableString LegacyGameplayLeaderboardDescription => new TranslatableString(getKey(@"legacy_gameplay_leaderboard_description"),
            @"Use osu!stable's scoreboard in legacy skins' default HUD. It shows during breaks, or throughout while the leaderboard is toggled on. Applies from the next play.");

        /// <summary>
        /// "Press F1 to view replay"
        /// </summary>
        public static LocalisableString LegacyFailViewReplay => new TranslatableString(getKey(@"legacy_fail_view_replay"), @"Press F1 to view replay");

        /// <summary>
        /// "Gameplay aspect ratio"
        /// </summary>
        public static LocalisableString GameplayAspectRatio => new TranslatableString(getKey(@"gameplay_aspect_ratio"), @"Gameplay aspect ratio");

        /// <summary>
        /// "Letterbox the loading screen and gameplay to a fixed aspect ratio, for skins designed for that size."
        /// </summary>
        public static LocalisableString GameplayAspectRatioDescription => new TranslatableString(getKey(@"gameplay_aspect_ratio_description"), @"Letterbox the loading screen and gameplay to a fixed aspect ratio, for skins designed for that size.");

        /// <summary>
        /// "Skip"
        /// </summary>
        public static LocalisableString LegacySkip => new TranslatableString(getKey(@"legacy_skip"), @"Skip");

        /// <summary>
        /// "Mods provide different ways to enjoy gameplay. Some have an effect on the score you can achieve during ranked play. Others are just for fun."
        /// </summary>
        public static LocalisableString LegacyModsDescription => new TranslatableString(getKey(@"legacy_mods_description"), @"Mods provide different ways to enjoy gameplay. Some have an effect on the score you can achieve during ranked play. Others are just for fun.");

        /// <summary>
        /// "Guest"
        /// </summary>
        public static LocalisableString LegacyGuest => new TranslatableString(getKey(@"legacy_guest"), @"Guest");

        /// <summary>
        /// "Click to sign in!"
        /// </summary>
        public static LocalisableString LegacySignIn => new TranslatableString(getKey(@"legacy_sign_in"), @"Click to sign in!");

        /// <summary>
        /// "Please sign in to enable online facilities."
        /// </summary>
        public static LocalisableString LegacyOnlinePrompt => new TranslatableString(getKey(@"legacy_online_prompt"), @"Please sign in to enable online facilities.");

        /// <summary>
        /// "You have {0} beatmaps available!"
        /// </summary>
        public static LocalisableString LegacyBeatmapCount(int count) => new TranslatableString(getKey(@"legacy_beatmap_count"), @"You have {0} beatmaps available!", count);

        /// <summary>
        /// "Score Multiplier: {0}"
        /// </summary>
        public static LocalisableString LegacyScoreMultiplier(string multiplier) => new TranslatableString(getKey(@"legacy_score_multiplier"), @"Score Multiplier: {0}", multiplier);

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}

