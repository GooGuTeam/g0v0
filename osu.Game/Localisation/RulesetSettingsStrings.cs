using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class RulesetSettingsStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.RulesetSettings";

        /// <summary>
        /// "Rulesets"
        /// </summary>
        public static LocalisableString Rulesets => new TranslatableString(getKey(@"rulesets"), @"Rulesets");

        /// <summary>
        /// "Snaking in sliders"
        /// </summary>
        public static LocalisableString SnakingInSliders => new TranslatableString(getKey(@"snaking_in_sliders"), @"Snaking in sliders");

        /// <summary>
        /// "Snaking out sliders"
        /// </summary>
        public static LocalisableString SnakingOutSliders => new TranslatableString(getKey(@"snaking_out_sliders"), @"Snaking out sliders");

        /// <summary>
        /// "Cursor trail"
        /// </summary>
        public static LocalisableString CursorTrail => new TranslatableString(getKey(@"cursor_trail"), @"Cursor trail");

        /// <summary>
        /// "Cursor ripples"
        /// </summary>
        public static LocalisableString CursorRipples => new TranslatableString(getKey(@"cursor_ripples"), @"Cursor ripples");

        /// <summary>
        /// "Playfield border style"
        /// </summary>
        public static LocalisableString PlayfieldBorderStyle => new TranslatableString(getKey(@"playfield_border_style"), @"Playfield border style");

        /// <summary>
        /// "None"
        /// </summary>
        public static LocalisableString BorderNone => new TranslatableString(getKey(@"no_borders"), @"None");

        /// <summary>
        /// "Corners"
        /// </summary>
        public static LocalisableString BorderCorners => new TranslatableString(getKey(@"corner_borders"), @"Corners");

        /// <summary>
        /// "Full"
        /// </summary>
        public static LocalisableString BorderFull => new TranslatableString(getKey(@"full_borders"), @"Full");

        /// <summary>
        /// "Scrolling direction"
        /// </summary>
        public static LocalisableString ScrollingDirection => new TranslatableString(getKey(@"scrolling_direction"), @"Scrolling direction");

        /// <summary>
        /// "Up"
        /// </summary>
        public static LocalisableString ScrollingDirectionUp => new TranslatableString(getKey(@"scrolling_up"), @"Up");

        /// <summary>
        /// "Down"
        /// </summary>
        public static LocalisableString ScrollingDirectionDown => new TranslatableString(getKey(@"scrolling_down"), @"Down");

        /// <summary>
        /// "Scroll speed"
        /// </summary>
        public static LocalisableString ScrollSpeed => new TranslatableString(getKey(@"scroll_speed"), @"Scroll speed");

        /// <summary>
        /// "Timing-based note colouring"
        /// </summary>
        public static LocalisableString TimingBasedColouring => new TranslatableString(getKey(@"Timing_based_colouring"), @"Timing-based note colouring");

        /// <summary>
        /// "Rate-adjusted hit animations"
        /// </summary>
        public static LocalisableString RateAdjustedHitAnimation => new TranslatableString(getKey(@"rate_adjusted_hit_animation"), @"Rate-adjusted hit animations");

        /// <summary>
        /// "Hits will fly faster or slower when beatmap rate is adjusted via mods."
        /// </summary>
        public static LocalisableString RateAdjustedHitAnimationTooltip => new TranslatableString(getKey(@"rate_adjusted_hit_animation_tooltip"), @"Hits will fly faster or slower when beatmap rate is adjusted via mods.");

        /// <summary>
        /// "Hit animations"
        /// </summary>
        public static LocalisableString HitAnimations => new TranslatableString(getKey(@"hit_animations"), @"Hit animations");

        /// <summary>
        /// "When enabled, hits will fly off the screen. When disabled, hits will disappear immediately."
        /// </summary>
        public static LocalisableString HitAnimationsTaikoTooltip => new TranslatableString(getKey(@"hit_animations_taiko_tooltip"), @"When enabled, hits will fly off the screen. When disabled, hits will disappear immediately.");

        /// <summary>
        /// "When enabled, hit circles will play an animation when hit. When disabled, they will disappear almost immediately."
        /// </summary>
        public static LocalisableString HitAnimationsOsuTooltip => new TranslatableString(getKey(@"hit_animations_osu_tooltip"), @"When enabled, hit circles will play an animation when hit. When disabled, they will disappear almost immediately.");

        /// <summary>
        /// "{0}ms (speed {1:N1})"
        /// </summary>
        public static LocalisableString ScrollSpeedTooltip(int scrollTime, double scrollSpeed) => new TranslatableString(getKey(@"ruleset"), @"{0}ms (speed {1:N1})", scrollTime, scrollSpeed);

        /// <summary>
        /// "Touch control scheme"
        /// </summary>
        public static LocalisableString TouchControlScheme => new TranslatableString(getKey(@"touch_control_scheme"), @"Touch control scheme");

        /// <summary>
        /// "Mobile layout"
        /// </summary>
        public static LocalisableString MobileLayout => new TranslatableString(getKey(@"mobile_layout"), @"Mobile layout");

        /// <summary>
        /// "Portrait"
        /// </summary>
        public static LocalisableString Portrait => new TranslatableString(getKey(@"portrait"), @"Portrait");

        /// <summary>
        /// "Landscape"
        /// </summary>
        public static LocalisableString Landscape => new TranslatableString(getKey(@"landscape"), @"Landscape");

        /// <summary>
        /// "Landscape (expanded columns)"
        /// </summary>
        public static LocalisableString LandscapeExpandedColumns => new TranslatableString(getKey(@"landscape_expanded_columns"), @"Landscape (expanded columns)");

        /// <summary>
        /// "Touch overlay"
        /// </summary>
        public static LocalisableString TouchOverlay => new TranslatableString(getKey(@"touch_overlay"), @"Touch overlay");

        /// <summary>
        /// "Ruleset management"
        /// </summary>
        public static LocalisableString RulesetManagement => new TranslatableString(getKey(@"ruleset_management"), @"Ruleset management");

        /// <summary>
        /// "Manage supported game modes!"
        /// </summary>
        public static LocalisableString RulesetManagementSubheading => new TranslatableString(getKey(@"ruleset_management_subheading"), @"Manage supported game modes!");

        /// <summary>
        /// "Local rulesets"
        /// </summary>
        public static LocalisableString LocalRulesets => new TranslatableString(getKey(@"local_rulesets"), @"Local rulesets");

        /// <summary>
        /// "&lt;unknown version&gt;"
        /// </summary>
        public static LocalisableString UnknownVersionPlaceholder => new TranslatableString(getKey(@"unknown_version_placeholder"), @"<unknown version>");

        /// <summary>
        /// "(Builtin)"
        /// </summary>
        public static LocalisableString BuiltinPrefix => new TranslatableString(getKey(@"builtin_prefix"), @"(Builtin)");

        /// <summary>
        /// "Show source assembly in file manager"
        /// </summary>
        public static LocalisableString ShowAssemblyInFileManager => new TranslatableString(getKey(@"show_assembly_in_file_manager"), @"Show source assembly in file manager");

        /// <summary>
        /// "Trusted mode"
        /// </summary>
        public static LocalisableString TrustedMode => new TranslatableString(getKey(@"trusted_mode"), @"Trusted mode");

        /// <summary>
        /// "Newly added rulesets won't be enabled by default."
        /// </summary>
        public static LocalisableString TrustedModeTooltip
            => new TranslatableString(getKey(@"trusted_mode_tooltip"), @"Newly added rulesets won't be enabled by default.");

        /// <summary>
        /// "Changes to local rulesets would take effect after a restart of the game client."
        /// </summary>
        public static LocalisableString LocalRulesetRestartNote
            => new TranslatableString(getKey(@"local_ruleset_restart_note"), @"Changes to local rulesets would take effect after a restart of the game client.");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
