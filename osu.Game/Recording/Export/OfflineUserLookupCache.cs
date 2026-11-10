// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using System.Threading.Tasks;
using osu.Game.Database;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Recording.Export
{
    /// <summary>
    /// Stands in for the game's <see cref="UserLookupCache"/> in the export scene: every lookup finds nothing, without network access,
    /// so identity displays keep what the replay's score records. It is never loaded into the scene.
    /// </summary>
    public partial class OfflineUserLookupCache : UserLookupCache
    {
        protected override Task<APIUser?> ComputeValueAsync(int lookup, CancellationToken token = default) => Task.FromResult<APIUser?>(null);
    }
}
