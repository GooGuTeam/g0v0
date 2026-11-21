// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Platform;

namespace osu.Game.Database
{
    /// <summary>Compatibility entry point for score exports through registered replay codecs.</summary>
    public class LegacyScoreExporter : ScoreExporter
    {
        public LegacyScoreExporter(Storage storage)
            : base(storage)
        {
        }
    }
}
