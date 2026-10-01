// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Database
{
    /// <summary>
    /// Selects the local database implementation used by domain stores.
    /// </summary>
    public enum DataStoreBackend
    {
        Realm,
        SQLite
    }
}
