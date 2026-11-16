// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

#nullable disable

using osu.Framework.IO.Stores;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;

namespace osu.Game.Scoring
{
    /// <summary>Compatibility entry point; stored replays are loaded by the shared codec registry.</summary>
    public class LegacyDatabasedScore : DatabasedScore
    {
        public LegacyDatabasedScore(ScoreInfo score, RulesetStore rulesets, BeatmapManager beatmaps, IResourceStore<byte[]> store)
            : base(score, rulesets, beatmaps, store)
        {
        }
    }
}
