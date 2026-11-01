// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.Configuration
{
    public enum RecordingFrameRate
    {
        [Description(@"30 FPS")]
        Fps30 = 30,
        [Description(@"60 FPS")]
        Fps60 = 60,
        [Description(@"120 FPS")]
        Fps120 = 120,
    }
}
