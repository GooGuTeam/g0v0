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
        /// "Additions"
        /// </summary>
        public static LocalisableString Additions => new TranslatableString(getKey(@"additions"), @"Additions");

        /// <summary>
        /// "Whistle"
        /// </summary>
        public static LocalisableString Whistle => new TranslatableString(getKey(@"whistle"), @"Whistle");

        /// <summary>
        /// "Finish"
        /// </summary>
        public static LocalisableString Finish => new TranslatableString(getKey(@"finish"), @"Finish");

        /// <summary>
        /// "Clap"
        /// </summary>
        public static LocalisableString Clap => new TranslatableString(getKey(@"clap"), @"Clap");

        /// <summary>
        /// "Auto"
        /// </summary>
        public static LocalisableString Auto => new TranslatableString(getKey(@"auto"), @"Auto");

        /// <summary>
        /// "Click to edit bank and set · Ctrl-click to select · Right-click to reset"
        /// </summary>
        public static LocalisableString BankHint => new TranslatableString(getKey(@"bank_hint"), @"Click to edit bank and set · Ctrl-click to select · Right-click to reset");

        /// <summary>
        /// "Click to toggle this addition · Ctrl-click to select"
        /// </summary>
        public static LocalisableString AdditionHint => new TranslatableString(getKey(@"addition_hint"), @"Click to toggle this addition · Ctrl-click to select");

        /// <summary>
        /// "Enable an addition first to configure its bank"
        /// </summary>
        public static LocalisableString ConfigureAdditionHint => new TranslatableString(getKey(@"configure_addition_hint"), @"Enable an addition first to configure its bank");

        /// <summary>
        /// "Object sample"
        /// </summary>
        public static LocalisableString BodySample => new TranslatableString(getKey(@"body_sample"), @"Object sample");

        /// <summary>
        /// "Restore defaults"
        /// </summary>
        public static LocalisableString RestoreDefaults => new TranslatableString(getKey(@"restore_defaults"), @"Restore defaults");

        /// <summary>
        /// "Drag to change volume · Double-click for precise editing · Ctrl-click to edit from a sample point"
        /// </summary>
        public static LocalisableString VolumeHint => new TranslatableString(getKey(@"volume_hint"), @"Drag to change volume · Double-click for precise editing · Ctrl-click to edit from a sample point");

        /// <summary>
        /// "Ctrl-click to edit the volume from this sample point onwards"
        /// </summary>
        public static LocalisableString CtrlSplitHint => new TranslatableString(getKey(@"ctrl_split_hint"), @"Ctrl-click to edit the volume from this sample point onwards");

        /// <summary>
        /// "Click to edit bank and set"
        /// </summary>
        public static LocalisableString TooltipClickEditBank => new TranslatableString(getKey(@"tooltip_click_edit_bank"), @"Click to edit bank and set");

        /// <summary>
        /// "Select sample point"
        /// </summary>
        public static LocalisableString TooltipSelectSamplePoint => new TranslatableString(getKey(@"tooltip_select_sample_point"), @"Select sample point");

        /// <summary>
        /// "Right click to restore defaults"
        /// </summary>
        public static LocalisableString TooltipRestoreDefaults => new TranslatableString(getKey(@"tooltip_restore_defaults"), @"Right click to restore defaults");

        /// <summary>
        /// "Cycle sample sets on hover; no selection needed"
        /// </summary>
        public static LocalisableString TooltipCycleSampleSets => new TranslatableString(getKey(@"tooltip_cycle_sample_sets"), @"Cycle sample sets on hover; no selection needed");

        /// <summary>
        /// "Click to toggle this addition"
        /// </summary>
        public static LocalisableString TooltipToggleAddition => new TranslatableString(getKey(@"tooltip_toggle_addition"), @"Click to toggle this addition");

        /// <summary>
        /// "Drag to change volume"
        /// </summary>
        public static LocalisableString TooltipDragVolume => new TranslatableString(getKey(@"tooltip_drag_volume"), @"Drag to change volume");

        /// <summary>
        /// "Double click for precise volume editing"
        /// </summary>
        public static LocalisableString TooltipDoubleClickVolume => new TranslatableString(getKey(@"tooltip_double_click_volume"), @"Double click for precise volume editing");

        /// <summary>
        /// "Edit volume from a sample point"
        /// </summary>
        public static LocalisableString TooltipEditVolumeFromPoint => new TranslatableString(getKey(@"tooltip_edit_volume_from_point"), @"Edit volume from a sample point");

        /// <summary>
        /// "Edit volume from this sample point onwards"
        /// </summary>
        public static LocalisableString TooltipEditVolumeFromPointOnwards => new TranslatableString(getKey(@"tooltip_edit_volume_from_point_onwards"), @"Edit volume from this sample point onwards");

        /// <summary>
        /// "{0} - {1}"
        /// </summary>
        public static LocalisableString TimeRange(string start, string end) => new TranslatableString(getKey(@"time_range"), @"{0} - {1}", start, end);

        /// <summary>
        /// "Time"
        /// </summary>
        public static LocalisableString Time => new TranslatableString(getKey(@"time"), @"Time");

        /// <summary>
        /// "Set"
        /// </summary>
        public static LocalisableString SetCaption => new TranslatableString(getKey(@"set_caption"), @"Set");

        /// <summary>
        /// "Preview"
        /// </summary>
        public static LocalisableString Preview => new TranslatableString(getKey(@"preview"), @"Preview");

        /// <summary>
        /// "Set ID"
        /// </summary>
        public static LocalisableString SetId => new TranslatableString(getKey(@"set_id"), @"Set ID");

        /// <summary>
        /// "Search sets…"
        /// </summary>
        public static LocalisableString SearchSets => new TranslatableString(getKey(@"search_sets"), @"Search sets…");

        /// <summary>
        /// "Preview length"
        /// </summary>
        public static LocalisableString PreviewLength => new TranslatableString(getKey(@"preview_length"), @"Preview length");

        /// <summary>
        /// "Open preview highlights"
        /// </summary>
        public static LocalisableString PreviewHighlights => new TranslatableString(getKey(@"preview_highlights"), @"Open preview highlights");

        /// <summary>
        /// "Selected"
        /// </summary>
        public static LocalisableString Selected => new TranslatableString(getKey(@"selected"), @"Selected");

        /// <summary>
        /// "▶  Play preview"
        /// </summary>
        public static LocalisableString PlayPreview => new TranslatableString(getKey(@"play_preview"), @"▶  Play preview");

        /// <summary>
        /// "■  Stop preview"
        /// </summary>
        public static LocalisableString StopPreview => new TranslatableString(getKey(@"stop_preview"), @"■  Stop preview");

        /// <summary>
        /// "Time of the selected sample point"
        /// </summary>
        public static LocalisableString TooltipTime => new TranslatableString(getKey(@"tooltip_time"), @"Time of the selected sample point");

        /// <summary>
        /// "Every sample of this object shares one set index"
        /// </summary>
        public static LocalisableString TooltipSelectedSet => new TranslatableString(getKey(@"tooltip_selected_set"), @"Every sample of this object shares one set index");

        /// <summary>
        /// "Click to choose the normal bank"
        /// </summary>
        public static LocalisableString TooltipSelectedBank => new TranslatableString(getKey(@"tooltip_selected_bank"), @"Click to choose the normal bank");

        /// <summary>
        /// "Choose the addition bank"
        /// </summary>
        public static LocalisableString TooltipSelectedAddition => new TranslatableString(getKey(@"tooltip_selected_addition"), @"Choose the addition bank");

        /// <summary>
        /// "Click to toggle Whistle"
        /// </summary>
        public static LocalisableString TooltipWhistle => new TranslatableString(getKey(@"tooltip_whistle"), @"Click to toggle Whistle");

        /// <summary>
        /// "Click to toggle Finish"
        /// </summary>
        public static LocalisableString TooltipFinish => new TranslatableString(getKey(@"tooltip_finish"), @"Click to toggle Finish");

        /// <summary>
        /// "Click to toggle Clap"
        /// </summary>
        public static LocalisableString TooltipClap => new TranslatableString(getKey(@"tooltip_clap"), @"Click to toggle Clap");

        /// <summary>
        /// "Toggle Whistle for selected points"
        /// </summary>
        public static LocalisableString HotkeyToggleWhistle => new TranslatableString(getKey(@"hotkey_toggle_whistle"), @"Toggle Whistle for selected points");

        /// <summary>
        /// "Toggle Finish for selected points"
        /// </summary>
        public static LocalisableString HotkeyToggleFinish => new TranslatableString(getKey(@"hotkey_toggle_finish"), @"Toggle Finish for selected points");

        /// <summary>
        /// "Toggle Clap for selected points"
        /// </summary>
        public static LocalisableString HotkeyToggleClap => new TranslatableString(getKey(@"hotkey_toggle_clap"), @"Toggle Clap for selected points");

        /// <summary>
        /// "Preview the hitsound under different samplesets!"
        /// </summary>
        public static LocalisableString TooltipPreview => new TranslatableString(getKey(@"tooltip_preview"), @"Preview the hitsound under different samplesets!");

        /// <summary>
        /// "The set number to filter"
        /// </summary>
        public static LocalisableString TooltipSearchSets => new TranslatableString(getKey(@"tooltip_search_sets"), @"The set number to filter");

        /// <summary>
        /// "The preview length around the selected sound points"
        /// </summary>
        public static LocalisableString TooltipPreviewLength => new TranslatableString(getKey(@"tooltip_preview_length"), @"The preview length around the selected sound points");

        /// <summary>
        /// "Turn on to show the preview range highlight"
        /// </summary>
        public static LocalisableString TooltipPreviewHighlights => new TranslatableString(getKey(@"tooltip_preview_highlights"), @"Turn on to show the preview range highlight");

        /// <summary>
        /// "Use samples from the current user skin"
        /// </summary>
        public static LocalisableString TooltipUserSkin => new TranslatableString(getKey(@"tooltip_user_skin"), @"Use samples from the current user skin");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
