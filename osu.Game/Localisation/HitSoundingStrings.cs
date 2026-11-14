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

        /// <summary>
        /// "Default Normal"
        /// </summary>
        public static LocalisableString DefaultNormal => new TranslatableString(getKey(@"default_normal"), @"Default Normal");

        /// <summary>
        /// "Do not hide"
        /// </summary>
        public static LocalisableString ShowAllNormal => new TranslatableString(getKey(@"show_all_normal"), @"Do not hide");

        /// <summary>
        /// "Hide Normal0"
        /// </summary>
        public static LocalisableString HideNormal0 => new TranslatableString(getKey(@"hide_normal_0"), @"Hide Normal0");

        /// <summary>
        /// "Hide Normal1"
        /// </summary>
        public static LocalisableString HideNormal1 => new TranslatableString(getKey(@"hide_normal_1"), @"Hide Normal1");

        /// <summary>
        /// "Sample Gallery"
        /// </summary>
        public static LocalisableString SampleGallery => new TranslatableString(getKey(@"sample_gallery"), @"Sample Gallery");

        /// <summary>
        /// "Quick edit the sampleset!"
        /// </summary>
        public static LocalisableString TooltipSampleGallery => new TranslatableString(getKey(@"tooltip_sample_gallery"), @"Quick edit the sampleset!");

        /// <summary>
        /// "Sample sets"
        /// </summary>
        public static LocalisableString SampleSetsHeader => new TranslatableString(getKey(@"sample_sets_header"), @"Sample sets");

        /// <summary>
        /// "+  Add set"
        /// </summary>
        public static LocalisableString AddSetButton => new TranslatableString(getKey(@"add_set_button"), @"+  Add set");

        /// <summary>
        /// "Add a sample set"
        /// </summary>
        public static LocalisableString TooltipAddSet => new TranslatableString(getKey(@"tooltip_add_set"), @"Add a sample set");

        /// <summary>
        /// "Drop a file here to replace the assigned sample"
        /// </summary>
        public static LocalisableString TooltipAssignedSlotRow => new TranslatableString(getKey(@"tooltip_assigned_slot_row"), @"Drop a file here to replace the assigned sample");

        /// <summary>
        /// "Click to play the sample assigned to this slot"
        /// </summary>
        public static LocalisableString TooltipAssignedSlotPlay => new TranslatableString(getKey(@"tooltip_assigned_slot_play"), @"Click to play the sample assigned to this slot");

        /// <summary>
        /// "Click to choose a sample file"
        /// </summary>
        public static LocalisableString TooltipEmptySlotClick => new TranslatableString(getKey(@"tooltip_empty_slot_click"), @"Click to choose a sample file");

        /// <summary>
        /// "Drag and drop a file here to assign it"
        /// </summary>
        public static LocalisableString TooltipEmptySlotDrop => new TranslatableString(getKey(@"tooltip_empty_slot_drop"), @"Drag and drop a file here to assign it");

        /// <summary>
        /// "Release to assign {0} to {1}"
        /// </summary>
        public static LocalisableString TooltipDropTarget(LocalisableString source, LocalisableString slot) => new TranslatableString(getKey(@"tooltip_drop_target"), @"Release to assign {0} to {1}", source, slot);

        /// <summary>
        /// "Sample files"
        /// </summary>
        public static LocalisableString SampleFilesHeader => new TranslatableString(getKey(@"sample_files_header"), @"Sample files");

        /// <summary>
        /// "+  Add files"
        /// </summary>
        public static LocalisableString AddFilesButton => new TranslatableString(getKey(@"add_files_button"), @"+  Add files");

        /// <summary>
        /// "Add sample files to this beatmap"
        /// </summary>
        public static LocalisableString TooltipAddFiles => new TranslatableString(getKey(@"tooltip_add_files"), @"Add sample files to this beatmap");

        /// <summary>
        /// "Search aliases or filenames…"
        /// </summary>
        public static LocalisableString SearchFilesPlaceholder => new TranslatableString(getKey(@"search_files_placeholder"), @"Search aliases or filenames…");

        /// <summary>
        /// "Search aliases and original filenames"
        /// </summary>
        public static LocalisableString TooltipSearchFiles => new TranslatableString(getKey(@"tooltip_search_files"), @"Search aliases and original filenames");

        /// <summary>
        /// "{0} files"
        /// </summary>
        public static LocalisableString FilesCount(int count) => new TranslatableString(getKey(@"files_count"), @"{0} files", count);

        /// <summary>
        /// "{0} file"
        /// </summary>
        public static LocalisableString FileCountSingular(int count) => new TranslatableString(getKey(@"file_count_singular"), @"{0} file", count);

        /// <summary>
        /// "Drag this file onto a sample slot"
        /// </summary>
        public static LocalisableString TooltipFileTileDrag => new TranslatableString(getKey(@"tooltip_file_tile_drag"), @"Drag this file onto a sample slot");

        /// <summary>
        /// "Double Click to edit alias without renaming the original file"
        /// </summary>
        public static LocalisableString TooltipFileTileAlias => new TranslatableString(getKey(@"tooltip_file_tile_alias"), @"Double Click to edit alias without renaming the original file");

        /// <summary>
        /// "ENTER"
        /// </summary>
        public static LocalisableString AliasConfirmHotkey => new TranslatableString(getKey(@"alias_confirm_hotkey"), @"ENTER");

        /// <summary>
        /// "Confirm alias"
        /// </summary>
        public static LocalisableString AliasConfirm => new TranslatableString(getKey(@"alias_confirm"), @"Confirm alias");

        /// <summary>
        /// "Drag a tile onto a tree item to assign its sample"
        /// </summary>
        public static LocalisableString FilesHint => new TranslatableString(getKey(@"files_hint"), @"Drag a tile onto a tree item to assign its sample");

        /// <summary>
        /// "No sample files yet — use Add files to import some"
        /// </summary>
        public static LocalisableString NoSampleFiles => new TranslatableString(getKey(@"no_sample_files"), @"No sample files yet — use Add files to import some");

        /// <summary>
        /// "No files match your search"
        /// </summary>
        public static LocalisableString NoMatchingFiles => new TranslatableString(getKey(@"no_matching_files"), @"No files match your search");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
