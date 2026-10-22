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
    /// <para>
    /// All read methods return detached copies which are safe to use on any thread.
    /// </para>
    /// <para>
    /// This store owns metadata and file references only. Writing and deleting skin file
    /// contents is the caller's responsibility.
    /// </para>
    /// </remarks>
    public interface ISkinStore
    {
        /// <summary>
        /// The directory, relative to the configured storage root, which holds this backend's
        /// skin file contents.
        /// </summary>
        /// <remarks>
        /// The Realm backend shares the general <c>files</c> directory. The SQLite backend uses a
        /// dedicated <c>skin-files</c> directory so that Realm's file cleanup does not remove
        /// content still referenced by this store.
        /// </remarks>
        string FileStorageDirectory { get; }

        /// <summary>
        /// Retrieve a skin by primary key.
        /// </summary>
        /// <returns>The detached skin, or null if not found.</returns>
        SkinInfo? GetDetached(Guid id);

        /// <summary>
        /// Retrieve all skins, including deleted and protected entries.
        /// </summary>
        /// <returns>Detached copies of every stored skin.</returns>
        List<SkinInfo> GetAllDetached();

        /// <summary>
        /// Retrieve all usable skins (not deleted, not protected).
        /// </summary>
        List<SkinInfo> GetAllUsableDetached();

        /// <summary>
        /// Retrieve a skin by its content hash.
        /// </summary>
        /// <remarks>
        /// Deleted skins are included so importers can reuse and restore an existing entry;
        /// non-deleted entries are preferred.
        /// </remarks>
        /// <returns>The detached skin, or null if none matches.</returns>
        SkinInfo? FindByHash(string hash);

        /// <summary>
        /// Store a skin together with its ordered file references.
        /// </summary>
        void Add(SkinInfo item);

        /// <summary>
        /// Apply an in-place mutation to the skin with the given primary key, if it exists.
        /// </summary>
        /// <remarks>
        /// The primary key is preserved: a mutation attempting to change it does not re-key the
        /// stored entry. A callback throwing aborts the update without modifying storage.
        /// </remarks>
        void Update(Guid id, Action<SkinInfo> update);

        /// <summary>
        /// Soft-delete a skin.
        /// </summary>
        /// <returns>false if the skin does not exist, is protected or is already deleted.</returns>
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
