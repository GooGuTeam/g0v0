// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.Recording
{
    public enum RecordingResolution
    {
        [Description(@"1280 × 720")]
        Hd720 = 720,
        [Description(@"1920 × 1080")]
        FullHd1080 = 1080,
        [Description(@"2560 × 1440")]
        Qhd1440 = 1440,
        [Description(@"3840 × 2160")]
        Uhd2160 = 2160,
    }
}
