// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Input.Bindings;
using osu.Game.Models;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Skinning;
using Realms;

namespace osu.Game.Database
{
    [GenerateRealmDetach(typeof(RealmKeyBinding))]
    [GenerateRealmDetach(typeof(BeatmapCollection))]
    [GenerateRealmDetach(typeof(BeatmapMetadata))]
    [GenerateRealmDetach(typeof(BeatmapUserSettings))]
    [GenerateRealmDetach(typeof(BeatmapDifficulty))]
    [GenerateRealmDetach(typeof(RulesetInfo))]
    [GenerateRealmDetach(typeof(ScoreInfo))]
    [GenerateRealmDetach(typeof(RealmUser))]
    [GenerateRealmDetach(typeof(RealmFile))]
    [GenerateRealmDetach(typeof(RealmNamedFileUsage))]
    [GenerateRealmDetach(typeof(RealmOnlineAsset))]
    [GenerateRealmDetach(typeof(SkinInfo))]
    [GenerateRealmDetach(typeof(BeatmapSetInfo), OptionalMembers = new[] { nameof(BeatmapSetInfo.Files) })]
    [GenerateRealmDetach(typeof(BeatmapInfo), OptionalMembers = new[] { nameof(BeatmapInfo.BeatmapSet) })]
    [GenerateRealmCopy(typeof(BeatmapMetadata), IgnoredMembers = new[] { nameof(BeatmapMetadata.Author) })]
    [GenerateRealmCopy(typeof(BeatmapDifficulty))]
    [GenerateRealmCopy(typeof(RealmUser))]
    [GenerateRealmCopy(typeof(RealmFile))]
    [GenerateRealmCopy(typeof(RealmNamedFileUsage))]
    [GenerateRealmCopy(typeof(BeatmapInfo), IgnoredMembers = new[]
    {
        nameof(BeatmapInfo.Ruleset),
        nameof(BeatmapInfo.Metadata),
        nameof(BeatmapInfo.UserSettings),
        nameof(BeatmapInfo.Difficulty),
        nameof(BeatmapInfo.BeatmapSet)
    })]
    [GenerateRealmCopy(typeof(BeatmapSetInfo), IgnoredMembers = new[] { nameof(BeatmapSetInfo.Beatmaps) })]
    public static partial class RealmObjectExtensions
    {
        static partial void afterDetach_BeatmapSetInfo(BeatmapSetInfo source, BeatmapSetInfo destination)
        {
            foreach (var beatmap in destination.Beatmaps)
                beatmap.BeatmapSet = destination;
        }

        static partial void afterDetach_BeatmapInfo(BeatmapInfo source, BeatmapInfo destination)
        {
            for (int i = 0; i < destination.BeatmapSet?.Beatmaps.Count; i++)
            {
                if (destination.BeatmapSet.Beatmaps[i].Equals(destination))
                {
                    destination.BeatmapSet.Beatmaps[i] = destination;
                    break;
                }
            }
        }

        static partial void afterCopyToRealm_BeatmapMetadata(BeatmapMetadata source, BeatmapMetadata destination)
        {
            copyToRealm_RealmUser(source.Author, destination.Author);
        }

        static partial void afterCopyToRealm_BeatmapInfo(BeatmapInfo source, BeatmapInfo destination)
        {
            destination.Ruleset = destination.Realm!.Find<RulesetInfo>(source.Ruleset.ShortName)!;
            copyToRealm_BeatmapDifficulty(source.Difficulty, destination.Difficulty);
            copyToRealm_BeatmapMetadata(source.Metadata, destination.Metadata);
        }

        static partial void afterCopyToRealm_BeatmapSetInfo(BeatmapSetInfo source, BeatmapSetInfo destination)
        {
            foreach (var beatmap in source.Beatmaps)
            {
                // Importantly, search all of realm for the beatmap (not just the set's beatmaps).
                // It may have gotten detached, and if that's the case let's use this opportunity to fix
                // things up.
                var existingBeatmap = destination.Realm!.Find<BeatmapInfo>(beatmap.ID);

                if (existingBeatmap != null)
                {
                    // As above, reattach if it happens to not be in the set's beatmaps.
                    if (!destination.Beatmaps.Contains(existingBeatmap))
                    {
                        Debug.Fail("Beatmaps should never become detached under normal circumstances. If this ever triggers, it should be investigated further.");
                        Logger.Log("WARNING: One of the difficulties in a beatmap was detached from its set. Please save a copy of logs and report this to devs.", LoggingTarget.Database,
                            LogLevel.Important);
                        destination.Beatmaps.Add(existingBeatmap);
                    }

                    copyToRealm_BeatmapInfo(beatmap, existingBeatmap);
                }
                else
                {
                    var newBeatmap = new BeatmapInfo
                    {
                        ID = beatmap.ID,
                        BeatmapSet = destination,
                        Ruleset = destination.Realm.Find<RulesetInfo>(beatmap.Ruleset.ShortName)!
                    };

                    destination.Beatmaps.Add(newBeatmap);
                    copyToRealm_BeatmapInfo(beatmap, newBeatmap);
                }
            }
        }

        /// <summary>
        /// Create a detached copy of the each item in the collection.
        /// </summary>
        /// <remarks>
        /// Items which are already detached (ie. not managed by realm) will not be modified.
        /// </remarks>
        /// <param name="items">A list of managed <see cref="RealmObject"/>s to detach.</param>
        /// <typeparam name="T">The type of object.</typeparam>
        /// <returns>A list containing non-managed copies of provided items.</returns>
        public static List<T> Detach<T>(this IEnumerable<T> items) where T : RealmObjectBase
        {
            var list = new List<T>();

            foreach (var obj in items)
                list.Add(obj.Detach());

            return list;
        }

        /// <summary>
        /// Create a detached copy of the item.
        /// </summary>
        /// <remarks>
        /// If the item if already detached (ie. not managed by realm) it will not be detached again and the original instance will be returned. This allows this method to be potentially called at multiple levels while only incurring the clone overhead once.
        /// </remarks>
        /// <param name="item">The managed <see cref="RealmObject"/> to detach.</param>
        /// <typeparam name="T">The type of object.</typeparam>
        /// <returns>A non-managed copy of provided item. Will return the provided item if already detached.</returns>
        public static T Detach<T>(this T item) where T : RealmObjectBase
        {
            if (!item.IsManaged)
                return item;

            return detachGenerated(item);
        }

        /// <summary>
        /// Copy changes in a detached beatmap back to realm.
        /// This is a temporary method to handle existing flows only. It should not be used going forward if we can avoid it.
        /// </summary>
        /// <param name="source">The detached beatmap to copy from.</param>
        /// <param name="destination">The live beatmap to copy to.</param>
        public static void CopyChangesToRealm(this BeatmapSetInfo source, BeatmapSetInfo destination)
            => copyToRealm_BeatmapSetInfo(source, destination);

        public static List<Live<T>> ToLiveUnmanaged<T>(this IEnumerable<T> realmList)
            where T : RealmObject, IHasGuidPrimaryKey
        {
            return realmList.Select(l => new RealmLiveUnmanaged<T>(l)).Cast<Live<T>>().ToList();
        }

        public static Live<T> ToLiveUnmanaged<T>(this T realmObject)
            where T : RealmObject, IHasGuidPrimaryKey
        {
            return new RealmLiveUnmanaged<T>(realmObject);
        }

        public static Live<T> ToLive<T>(this T realmObject, RealmAccess realm)
            where T : RealmObject, IHasGuidPrimaryKey
        {
            return new RealmLive<T>(realmObject, realm);
        }

#pragma warning disable RS0030 // mentioning banned symbols in documentation
        /// <summary>
        /// Register a callback to be invoked each time this <see cref="IRealmCollection{T}" /> changes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This adds g0v0! specific thread and managed state safety checks on top of <see cref="IRealmCollection{T}.SubscribeForNotifications"/>.
        /// </para>
        /// <para>
        /// The first callback will be invoked with the initial <see cref="IRealmCollection{T}" /> after the asynchronous query completes,
        /// and then called again after each write transaction which changes either any of the objects in the collection, or
        /// which objects are in the collection. The <c>changes</c> parameter will
        /// be <c>null</c> the first time the callback is invoked with the initial results. For each call after that,
        /// it will contain information about which rows in the results were added, removed or modified.
        /// </para>
        /// <para>
        /// If a write transaction did not modify any objects in this <see cref="IRealmCollection{T}" />, the callback is not invoked at all.
        /// If an error occurs the callback will be invoked with <c>null</c> for the <c>sender</c> parameter and a non-<c>null</c> <c>error</c>.
        /// Currently, the only errors that can occur are when opening the <see cref="Realm" /> on the background worker thread.
        /// </para>
        /// <para>
        /// At the time when the block is called, the <see cref="IRealmCollection{T}" /> object will be fully evaluated
        /// and up-to-date, and as long as you do not perform a write transaction on the same thread
        /// or explicitly call <see cref="Realm.Refresh" />, accessing it will never perform blocking work.
        /// </para>
        /// <para>
        /// Notifications are delivered via the standard event loop, and so can't be delivered while the event loop is blocked by other activity.
        /// When notifications can't be delivered instantly, multiple notifications may be coalesced into a single notification.
        /// This can include the notification with the initial collection.
        /// </para>
        /// </remarks>
        /// <param name="collection">The <see cref="IRealmCollection{T}"/> to observe for changes.</param>
        /// <param name="callback">The callback to be invoked with the updated <see cref="IRealmCollection{T}" />.</param>
        /// <returns>
        /// A subscription token. It must be kept alive for as long as you want to receive change notifications.
        /// To stop receiving notifications, call <see cref="IDisposable.Dispose" />.
        /// </returns>
        /// <seealso cref="Realms.CollectionExtensions.SubscribeForNotifications{T}(IList{T}, NotificationCallbackDelegate{T},KeyPathsCollection?)" />
        /// <seealso cref="Realms.CollectionExtensions.SubscribeForNotifications{T}(IQueryable{T}, NotificationCallbackDelegate{T},KeyPathsCollection?)" />
#pragma warning restore RS0030
        public static IDisposable QueryAsyncWithNotifications<T>(this IRealmCollection<T> collection, NotificationCallbackDelegate<T> callback)
            where T : RealmObjectBase
        {
            if (!RealmAccess.CurrentThreadSubscriptionsAllowed)
                throw new InvalidOperationException($"Make sure to call {nameof(RealmAccess)}.{nameof(RealmAccess.RegisterForNotifications)}");

            bool initial = true;
            return collection.SubscribeForNotifications((sender, changes) =>
            {
                if (initial)
                {
                    initial = false;

                    // Realm might coalesce the initial callback, meaning we never receive a `ChangeSet` of `null` marking the first callback.
                    // Let's decouple it for simplicity in handling.
                    if (changes != null)
                    {
                        callback(sender, null);
                        return;
                    }
                }

                callback(sender, changes);
            });
        }

        /// <summary>
        /// A convenience method that casts <see cref="IQueryable{T}"/> to <see cref="IRealmCollection{T}"/> and subscribes for change notifications.
        /// </summary>
        /// <remarks>
        /// This adds g0v0! specific thread and managed state safety checks on top of <see cref="IRealmCollection{T}.SubscribeForNotifications"/>.
        /// </remarks>
        /// <param name="list">The <see cref="IQueryable{T}"/> to observe for changes.</param>
        /// <typeparam name="T">Type of the elements in the list.</typeparam>
        /// <seealso cref="IRealmCollection{T}.SubscribeForNotifications"/>
        /// <param name="callback">The callback to be invoked with the updated <see cref="IRealmCollection{T}"/>.</param>
        /// <returns>
        /// A subscription token. It must be kept alive for as long as you want to receive change notifications.
        /// To stop receiving notifications, call <see cref="IDisposable.Dispose"/>.
        ///
        /// May be null in the case the provided collection is not managed.
        /// </returns>
        public static IDisposable? QueryAsyncWithNotifications<T>(this IQueryable<T> list, NotificationCallbackDelegate<T> callback)
            where T : RealmObjectBase
        {
            // Subscribing to non-managed instances doesn't work.
            // In this usage, the instance may be non-managed in tests.
            if (!(list is IRealmCollection<T> realmCollection))
                return null;

            return QueryAsyncWithNotifications(realmCollection, callback);
        }

        /// <summary>
        /// A convenience method that casts <see cref="IList{T}"/> to <see cref="IRealmCollection{T}"/> and subscribes for change notifications.
        /// </summary>
        /// <remarks>
        /// This adds g0v0! specific thread and managed state safety checks on top of <see cref="IRealmCollection{T}.SubscribeForNotifications"/>.
        /// </remarks>
        /// <param name="list">The <see cref="IList{T}"/> to observe for changes.</param>
        /// <typeparam name="T">Type of the elements in the list.</typeparam>
        /// <seealso cref="IRealmCollection{T}.SubscribeForNotifications"/>
        /// <param name="callback">The callback to be invoked with the updated <see cref="IRealmCollection{T}"/>.</param>
        /// <returns>
        /// A subscription token. It must be kept alive for as long as you want to receive change notifications.
        /// To stop receiving notifications, call <see cref="IDisposable.Dispose"/>.
        ///
        /// May be null in the case the provided collection is not managed.
        /// </returns>
        public static IDisposable? QueryAsyncWithNotifications<T>(this IList<T> list, NotificationCallbackDelegate<T> callback)
            where T : RealmObjectBase
        {
            // Subscribing to non-managed instances doesn't work.
            // In this usage, the instance may be non-managed in tests.
            if (!(list is IRealmCollection<T> realmCollection))
                return null;

            return QueryAsyncWithNotifications(realmCollection, callback);
        }
    }
}
