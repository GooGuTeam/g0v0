// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Database
{
    /// <summary>
    /// A store for models supporting soft deletion (see <see cref="ISoftDelete"/>).
    /// </summary>
    public interface ISoftDeletableStore<TModel> : IStore<TModel>
        where TModel : class, IHasGuidPrimaryKey, ISoftDelete
    {
        /// <summary>
        /// Restore a previously soft-deleted model.
        /// </summary>
        /// <returns>false if the model was not in a deleted state or is protected.</returns>
        bool Undelete(Guid id);

        /// <summary>
        /// Permanently purge all soft-deleted models from the backing storage.
        /// </summary>
        void PurgeDeleted();
    }
}
