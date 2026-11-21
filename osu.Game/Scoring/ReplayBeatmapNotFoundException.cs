// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Scoring
{
    public class ReplayBeatmapNotFoundException : Exception
    {
        public string Hash { get; }

        public ReplayBeatmapNotFoundException(string hash)
            : base($"The replay's beatmap is not installed: {hash}")
        {
            Hash = hash;
        }
    }
}
