// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Entries;
using osu.Game.Overlays.Settings.Rendering.Stable;
using osu.Game.Recording;
using osu.Game.Recording.Live;

namespace osu.Game.Overlays.Settings.Sections.Graphics
{
    public partial class RecordingSettings : SettingsSubsection
    {
        protected override LocalisableString Header => VideoRecordingStrings.CaptureSettings;

        [Resolved(canBeNull: true)]
        private LiveCaptureManager? recording { get; set; }

        private ButtonEntry recordButton = null!;
        private ButtonEntry streamButton = null!;
        private Drawable recordButtonDrawable = null!;
        private Drawable streamButtonDrawable = null!;
        private bool? lastIsRecording;
        private bool? lastIsStreaming;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                CreateItem(new SliderEntry<int>
                {
                    Caption = VideoRecordingStrings.VideoBitrate,
                    HintText = VideoRecordingStrings.RecordingSettingsHelp,
                    Current = config.GetBindable<int>(OsuSetting.RecordingBitrate),
                    LabelFormat = value => VideoRecordingStrings.BitrateValue(value),
                }),
                CreateItem(new EnumDropdownEntry<RecordingFrameRate>
                {
                    Caption = VideoRecordingStrings.VideoFrameRate,
                    Current = config.GetBindable<RecordingFrameRate>(OsuSetting.RecordingFrameRate),
                }),
                CreateItem(new EnumDropdownEntry<RecordingEncoderProfile>
                {
                    Caption = VideoRecordingStrings.EncoderProfile,
                    HintText = VideoRecordingStrings.EncoderHelp,
                    Current = config.GetBindable<RecordingEncoderProfile>(OsuSetting.RecordingEncoderProfile),
                }),
                CreateItem(new EnumDropdownEntry<RecordingResolution>
                {
                    Caption = VideoRecordingStrings.VideoResolution,
                    HintText = VideoRecordingStrings.ResolutionHelp,
                    Current = config.GetBindable<RecordingResolution>(OsuSetting.RecordingResolution),
                }),
                CreateItem(new EnumDropdownEntry<RecordingBackend>
                {
                    Caption = VideoRecordingStrings.RecordingBackend,
                    HintText = VideoRecordingStrings.RecordingBackendHelp,
                    Current = config.GetBindable<RecordingBackend>(OsuSetting.RecordingBackend),
                }),
                CreateItem(new TextBoxEntry
                {
                    Caption = VideoRecordingStrings.FfmpegPath,
                    Current = config.GetBindable<string>(OsuSetting.RecordingFfmpegPath),
                    PlaceholderText = @"ffmpeg",
                }),
                recordButtonDrawable = CreateItem(recordButton = new ButtonEntry
                {
                    Caption = VideoRecordingStrings.RecordToggle,
                    Action = () => { if (recording != null) _ = recording.ToggleAsync(); },
                }),
                streamButtonDrawable = CreateItem(streamButton = new ButtonEntry
                {
                    Caption = VideoRecordingStrings.StreamStart,
                    Action = () => recording?.ToggleStreaming(),
                }),
            };
        }

        protected override void Update()
        {
            base.Update();

            bool isRecording = recording?.IsRecording == true;
            bool isStreaming = recording?.IsStreaming == true;

            recordButton.Enabled.Value = recording != null && !isStreaming;
            streamButton.Enabled.Value = recording != null && !isRecording;

            if (lastIsRecording != isRecording && setButtonText(recordButtonDrawable, isRecording ? VideoRecordingStrings.RecordStop : VideoRecordingStrings.RecordToggle))
                lastIsRecording = isRecording;

            if (lastIsStreaming != isStreaming && setButtonText(streamButtonDrawable, isStreaming ? VideoRecordingStrings.StreamStop : VideoRecordingStrings.StreamStart))
                lastIsStreaming = isStreaming;
        }

        private static bool setButtonText(Drawable drawable, LocalisableString text)
        {
            if (drawable is SettingsButtonV2 lazerButton)
            {
                lazerButton.Text = text;
                return true;
            }

            if (drawable.ChildrenOfType<StableButton.ButtonBody>().FirstOrDefault() is StableButton.ButtonBody stableButton)
            {
                stableButton.Text = text;
                return true;
            }

            return false;
        }
    }
}
