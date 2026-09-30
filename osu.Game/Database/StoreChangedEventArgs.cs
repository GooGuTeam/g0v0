// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Database
{
    /// <summary>
    /// The kind of a change observed in a store.
    /// </summary>
    public enum StoreChangeType
    {
        Added,
        Modified,
        Removed
    }

    /// <summary>
    /// Describes a batch of committed changes in a store.
    /// </summary>
    /// <remarks>
    /// A soft delete is reported as <see cref="StoreChangeType.Modified"/>, matching Realm semantics
    /// where <c>DeletePending</c> is a regular property.
    /// </remarks>
    public class StoreChangedEventArgs<TModel> : EventArgs
        where TModel : class, IHasGuidPrimaryKey
    {
        /// <summary>
        /// Primary keys of the changed models.
        /// </summary>
        public IReadOnlyList<Guid> Ids { get; }

        /// <summary>
        /// The kind of change.
        /// </summary>
        public StoreChangeType ChangeType { get; }

        public StoreChangedEventArgs(StoreChangeType changeType, IReadOnlyList<Guid> ids)
        {
            ChangeType = changeType;
            Ids = ids;
        }
    }
}
