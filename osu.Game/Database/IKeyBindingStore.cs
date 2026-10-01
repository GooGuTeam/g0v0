// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Input.Bindings;

namespace osu.Game.Database
{
    /// <summary>
    /// Storage for key bindings (global and per-ruleset).
    /// </summary>
    /// <remarks>
    /// All read methods return detached copies which are safe to use on any thread.
    /// Key bindings are hard-deleted; they do not support soft deletion.
    /// </remarks>
    public interface IKeyBindingStore
    {
        /// <summary>
        /// Retrieve all key bindings.
        /// </summary>
        List<RealmKeyBinding> GetAllDetached();

        /// <summary>
        /// Add a new key binding.
        /// The passed instance is consumed by the store and must not be used afterwards.
        /// </summary>
        void Add(RealmKeyBinding item);

        /// <summary>
        /// Apply a mutation to the stored binding with the given primary key.
        /// </summary>
        void Update(Guid id, Action<RealmKeyBinding> update);

        /// <summary>
        /// Permanently delete a key binding.
        /// </summary>
        /// <returns>false if the binding does not exist.</returns>
        bool Delete(Guid id);

        /// <summary>
        /// Subscribe to any change in this store. The callback is an invalidation hint only;
        /// refetch on invocation. Also invoked once for the initial snapshot and after any
        /// backend reset. The invoking thread is backend-defined.
        /// </summary>
        /// <returns>A handle which terminates the subscription when disposed.</returns>
        IDisposable Subscribe(Action onChanged);
    }
}
