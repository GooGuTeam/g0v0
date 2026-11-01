// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    public enum ReplayVideoBackend
    {
        [LocalisableDescription(typeof(VideoRecordingStrings), nameof(VideoRecordingStrings.EncoderAutomatic))]
        Automatic,
        [LocalisableDescription(typeof(VideoRecordingStrings), nameof(VideoRecordingStrings.NativeBackend))]
        Native,
        [LocalisableDescription(typeof(VideoRecordingStrings), nameof(VideoRecordingStrings.FfmpegBackend))]
        Ffmpeg,
    }
}
