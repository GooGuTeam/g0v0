// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Realms;

namespace osu.Game.Database
{
    /// <summary>
    /// Base class for Realm-backed stores of soft-deletable models (<see cref="ISoftDelete"/>),
    /// replacing hard deletion with the delete-pending flag and filtering usable subsets.
    /// </summary>
    /// <typeparam name="T">The realm model type managed by this store.</typeparam>
    public abstract class RealmSoftDeletableStore<T> : RealmStore<T>
        where T : RealmObject, IHasGuidPrimaryKey, ISoftDelete
    {
        protected RealmSoftDeletableStore(RealmAccess realm)
            : base(realm)
        {
        }

        /// <summary>
        /// Returns detached copies of all models not pending deletion.
        /// </summary>
        public virtual List<T> GetAllUsableDetached() => Realm.Run(r =>
            r.All<T>()
             .Where(item => !item.DeletePending)
             .AsEnumerable()
             .Detach());

        /// <summary>
        /// Marks the model with the given primary key as pending deletion.
        /// </summary>
        /// <returns>Whether a model was found and marked.</returns>
        public override bool Delete(Guid id) => Realm.Write(r =>
        {
            var item = r.Find<T>(id);

            if (item == null || item.DeletePending)
                return false;

            item.DeletePending = true;
            return true;
        });

        /// <summary>
        /// Clears the delete-pending flag on the model with the given primary key.
        /// </summary>
        /// <returns>Whether a model was found and restored.</returns>
        public bool Undelete(Guid id) => Realm.Write(r =>
        {
            var item = r.Find<T>(id);

            if (item == null || !item.DeletePending)
                return false;

            item.DeletePending = false;
            return true;
        });
    }
}
