// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Graphics
{
    public partial class RecordingSettings : SettingsSubsection
    {
        protected override LocalisableString Header => VideoRecordingStrings.CaptureSettings;

        [Resolved(canBeNull: true)]
        private VideoRecordingManager? recording { get; set; }

        private SettingsButtonV2 recordButton = null!;
        private SettingsButtonV2 streamButton = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = VideoRecordingStrings.VideoBitrate,
                    HintText = VideoRecordingStrings.RecordingSettingsHelp,
                    Current = config.GetBindable<int>(OsuSetting.RecordingBitrate),
                    LabelFormat = value => VideoRecordingStrings.BitrateValue(value),
                }),
                new SettingsItemV2(new FormEnumDropdown<RecordingFrameRate>
                {
                    Caption = VideoRecordingStrings.VideoFrameRate,
                    Current = config.GetBindable<RecordingFrameRate>(OsuSetting.RecordingFrameRate),
                }),
                new SettingsItemV2(new FormEnumDropdown<RecordingEncoderProfile>
                {
                    Caption = VideoRecordingStrings.EncoderProfile,
                    HintText = VideoRecordingStrings.EncoderHelp,
                    Current = config.GetBindable<RecordingEncoderProfile>(OsuSetting.RecordingEncoderProfile),
                }),
                new SettingsItemV2(new FormEnumDropdown<RecordingResolution>
                {
                    Caption = VideoRecordingStrings.VideoResolution,
                    HintText = VideoRecordingStrings.ResolutionHelp,
                    Current = config.GetBindable<RecordingResolution>(OsuSetting.RecordingResolution),
                }),
                new SettingsItemV2(new FormEnumDropdown<ReplayVideoBackend>
                {
                    Caption = VideoRecordingStrings.ExportBackend,
                    HintText = VideoRecordingStrings.ExportBackendHelp,
                    Current = config.GetBindable<ReplayVideoBackend>(OsuSetting.ReplayVideoBackend),
                }),
                new SettingsItemV2(new FormTextBox
                {
                    Caption = VideoRecordingStrings.FfmpegPath,
                    Current = config.GetBindable<string>(OsuSetting.RecordingFfmpegPath),
                    PlaceholderText = @"ffmpeg",
                }),
                recordButton = new SettingsButtonV2
                {
                    Text = VideoRecordingStrings.RecordToggle,
                    Action = () => { if (recording != null) _ = recording.ToggleAsync(); },
                },
                streamButton = new SettingsButtonV2
                {
                    Text = VideoRecordingStrings.StreamStart,
                    Action = () => recording?.ToggleStreaming(),
                },
            };
        }

        protected override void Update()
        {
            base.Update();
            recordButton.Enabled.Value = recording != null && !recording.IsStreaming;
            streamButton.Enabled.Value = recording != null;
            streamButton.Text = recording?.IsStreaming == true ? VideoRecordingStrings.StreamStop : VideoRecordingStrings.StreamStart;
            recordButton.Text = recording?.IsRecording == true ? VideoRecordingStrings.RecordStop : VideoRecordingStrings.RecordToggle;
        }
    }
}
