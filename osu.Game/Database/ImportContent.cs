// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Database
{
    /// <summary>
    /// A kind of user content which can be migrated from another installation.
    /// </summary>
    [Flags]
    public enum ImportContent
    {
        // TODO: Include settings?
        Beatmaps = 1 << 0,
        Scores = 1 << 1,
        Skins = 1 << 2,
        Collections = 1 << 3,
        All = Beatmaps | Scores | Skins | Collections
    }
}
