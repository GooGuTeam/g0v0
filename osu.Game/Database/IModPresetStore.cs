// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Database
{
    /// <summary>
    /// Storage for user mod presets.
    /// </summary>
    /// <remarks>
    /// All read methods return detached copies which are safe to use on any thread.
    /// </remarks>
    public interface IModPresetStore
    {
        /// <summary>
        /// Retrieve a mod preset by primary key.
        /// </summary>
        /// <returns>The detached preset, or null if not found.</returns>
        ModPreset? GetDetached(Guid id);

        /// <summary>
        /// Retrieve all non-deleted mod presets.
        /// </summary>
        List<ModPreset> GetAllUsableDetached();

        /// <summary>
        /// Soft-delete a mod preset.
        /// </summary>
        /// <returns>false if the preset does not exist or is already deleted.</returns>
        bool Delete(Guid id);

        /// <summary>
        /// Restore a soft-deleted mod preset.
        /// </summary>
        /// <returns>false if the preset does not exist or is not deleted.</returns>
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
