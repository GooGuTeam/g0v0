// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Video;

namespace osu.Game.Configuration
{
    public static class RecordingConfiguration
    {
        /// <summary>
        /// Capture on the update thread before starting asynchronous recording work.
        /// </summary>
        public static VideoRecordingOptions Snapshot(OsuConfigManager config) => new VideoRecordingOptions(
            (int)config.Get<RecordingFrameRate>(OsuSetting.RecordingFrameRate),
            checked(config.Get<int>(OsuSetting.RecordingBitrate) * 1000000),
            (int)config.Get<RecordingEncoderProfile>(OsuSetting.RecordingEncoderProfile),
            (int)config.Get<RecordingResolution>(OsuSetting.RecordingResolution) * 16 / 9,
            (int)config.Get<RecordingResolution>(OsuSetting.RecordingResolution));
    }
}
