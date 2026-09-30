// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Database
{
    /// <summary>
    /// Handle for an active store change subscription. Dispose to unsubscribe.
    /// </summary>
    public interface IStoreSubscription : IDisposable
    {
    }
}
