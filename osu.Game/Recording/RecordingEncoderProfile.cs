// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Recording
{
    public enum RecordingEncoderProfile
    {
        [LocalisableDescription(typeof(VideoRecordingStrings), nameof(VideoRecordingStrings.EncoderAutomatic))]
        Automatic = 0,
        [Description(@"H.264 Baseline")]
        Baseline = 66,
        [Description(@"H.264 Main")]
        Main = 77,
        [Description(@"H.264 High")]
        High = 100,
    }
}
