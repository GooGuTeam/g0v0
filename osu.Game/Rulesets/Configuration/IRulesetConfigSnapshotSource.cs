// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.Rulesets.Configuration
{
    /// <summary>
    /// Transfers setting values without sharing bindables or database-backed configuration instances.
    /// </summary>
    public interface IRulesetConfigSnapshotSource
    {
        IReadOnlyDictionary<string, string> CreateSnapshot();

        void ApplySnapshot(IReadOnlyDictionary<string, string> snapshot);
    }
}
