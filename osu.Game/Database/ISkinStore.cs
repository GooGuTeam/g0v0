// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Skinning;

namespace osu.Game.Database
{
    /// <summary>
    /// Storage for locally stored skins.
    /// </summary>
    public interface ISkinStore : ISoftDeletableStore<SkinInfo>
    {
    }
}
