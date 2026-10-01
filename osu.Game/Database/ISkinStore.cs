// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Skinning;

namespace osu.Game.Database
{
    /// <summary>
    /// Storage for locally stored skins.
    /// </summary>
    /// <remarks>
    /// All read methods return detached copies which are safe to use on any thread.
    /// </remarks>
    public interface ISkinStore
    {
        /// <summary>
        /// Retrieve a skin by primary key.
        /// </summary>
        /// <returns>The detached skin, or null if not found.</returns>
        SkinInfo? GetDetached(Guid id);

        /// <summary>
        /// Retrieve all usable skins (not deleted, not protected).
        /// </summary>
        List<SkinInfo> GetAllUsableDetached();

        /// <summary>
        /// Soft-delete a skin.
        /// </summary>
        /// <returns>false if the skin does not exist or is already deleted.</returns>
        bool Delete(Guid id);

        /// <summary>
        /// Restore a soft-deleted skin.
        /// </summary>
        /// <returns>false if the skin does not exist or is not deleted.</returns>
        bool Undelete(Guid id);

        /// <summary>
        /// Subscribe to any change in this store. The callback is an invalidation hint only;
        /// refetch on invocation. Also invoked once for the initial snapshot and after any
        /// backend reset. The invoking thread is backend-defined.
        /// </summary>
        /// <returns>A handle which terminates the subscription when disposed.</returns>
        IDisposable Subscribe(Action onChanged);
    }
}
