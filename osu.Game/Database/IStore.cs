// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Linq.Expressions;

namespace osu.Game.Database
{
    /// <summary>
    /// Backend-neutral storage for a single model type.
    /// </summary>
    /// <typeparam name="TModel">The model type. Must have a GUID primary key.</typeparam>
    /// <remarks>
    /// All read methods return <b>detached</b> copies which are safe to use on any thread
    /// and never mutate the stored data.
    /// </remarks>
    public interface IStore<TModel>
        where TModel : class, IHasGuidPrimaryKey
    {
        /// <summary>
        /// Retrieve a single model by primary key, detached from the backing store.
        /// </summary>
        /// <returns>The detached model, or null if not found.</returns>
        TModel? GetDetached(Guid id);

        /// <summary>
        /// Run a read query against this store and return its (detached) result.
        /// The provided <see cref="IQueryable{T}"/> is only valid for the duration of the callback.
        /// </summary>
        /// <remarks>
        /// Queryable-based (rather than exposing a live <see cref="IQueryable{T}"/>) so both Realm
        /// and LINQ-to-SQL backends can translate the expression without leaking live objects.
        /// Only use expression-tree-compatible constructs; no captured mutable state.
        /// </remarks>
        TResult Query<TResult>(Func<IQueryable<TModel>, TResult> query);

        /// <summary>
        /// Insert a new detached model. The primary key is taken from <paramref name="item"/>.
        /// </summary>
        /// <returns>A detached copy of the stored model.</returns>
        TModel Add(TModel item);

        /// <summary>
        /// Apply a mutation to the stored model with the given primary key, inside a write transaction.
        /// The instance passed to <paramref name="update"/> is the managed object; do not capture it.
        /// </summary>
        void Update(Guid id, Action<TModel> update);

        /// <summary>
        /// Delete the model with the given primary key.
        /// For soft-deletable stores this marks the model as deleted; see <see cref="ISoftDeletableStore{TModel}"/>.
        /// </summary>
        /// <returns>false if no model with the given id exists.</returns>
        bool Delete(Guid id);

        /// <summary>
        /// Subscribe to changes in this store.
        /// Replaces Realm-style live query subscriptions (<c>SubscribeForNotifications</c>).
        /// </summary>
        /// <param name="callback">
        /// Invoked after a committed transaction changed matching models.
        /// Thread on which this is invoked is backend-defined; schedule to the update thread if needed.
        /// </param>
        /// <param name="filter">Optional predicate restricting which changes raise the callback.</param>
        /// <returns>A handle which terminates the subscription when disposed.</returns>
        IStoreSubscription Subscribe(Action<StoreChangedEventArgs<TModel>> callback, Expression<Func<TModel, bool>>? filter = null);
    }
}
