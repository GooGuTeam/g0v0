// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.Configuration
{
    public enum ReleaseStream
    {
        [Description("g0v0")]
        G0V0,

        [Description("robin (Unstable)")]
        Robin
    }
}
