// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Realms;

namespace osu.Game.Database
{
    /// <summary>
    /// Base class for Realm-backed domain stores, providing the shared CRUD and
    /// change-subscription plumbing over a single <see cref="RealmObject"/> type.
    /// </summary>
    /// <typeparam name="T">The realm model type managed by this store.</typeparam>
    public abstract class RealmStore<T>
        where T : RealmObject, IHasGuidPrimaryKey
    {
        protected readonly RealmAccess Realm;

        protected RealmStore(RealmAccess realm)
        {
            Realm = realm;
        }

        /// <summary>
        /// Retrieves a model by its primary key and returns a detached copy.
        /// </summary>
        public T? GetDetached(Guid id) => Realm.Run(r => r.Find<T>(id)?.Detach());

        /// <summary>
        /// Returns detached copies of all models of this type.
        /// </summary>
        public List<T> GetAllDetached() => Realm.Run(r =>
            r.All<T>()
             .AsEnumerable()
             .Detach());

        public virtual void Add(T item) => Realm.Write(r => r.Add(item));

        /// <summary>
        /// Applies an in-place mutation to the managed model with the given primary key,
        /// if it exists.
        /// </summary>
        public void Update(Guid id, Action<T> update) => Realm.Write(r =>
        {
            var item = r.Find<T>(id);
            if (item != null)
                update(item);
        });

        /// <summary>
        /// Hard-deletes the model with the given primary key.
        /// Soft-deletable models should use <see cref="RealmSoftDeletableStore{T}"/> instead.
        /// </summary>
        /// <returns>Whether a model was found and deleted.</returns>
        public virtual bool Delete(Guid id) => Realm.Write(r =>
        {
            var item = r.Find<T>(id);

            if (item == null)
                return false;

            r.Remove(item);
            return true;
        });

        /// <summary>
        /// Subscribes to changes of this model type. Dispose the returned handle to unsubscribe.
        /// </summary>
        public IDisposable Subscribe(Action onChanged) => Realm.RegisterForNotifications(
            r => r.All<T>(),
            (_, _) => onChanged());
    }
}
