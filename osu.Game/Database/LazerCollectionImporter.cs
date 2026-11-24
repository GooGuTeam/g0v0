// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Game.Collections;
using osu.Game.IO;
using osu.Game.Localisation;
using osu.Game.Overlays.Notifications;
using Realms;

namespace osu.Game.Database
{
    /// <summary>
    /// Imports collections from another g0v0!/lazer installation.
    /// </summary>
    public class LazerCollectionImporter
    {
        private readonly RealmAccess realm;

        public Action<Notification>? PostNotification { get; set; }

        public LazerCollectionImporter(RealmAccess realm)
        {
            this.realm = realm;
        }

        public Task<int> GetAvailableCount(LazerStorage storage) => Task.Run(() =>
        {
            using Realm sourceRealm = storage.OpenRealm();
            return sourceRealm.All<BeatmapCollection>().Count();
        });

        public Task<int> ImportFromLazerAsync(LazerStorage storage) => Task.Run(() =>
        {
            var notification = new ProgressNotification
            {
                State = ProgressNotificationState.Active,
                Text = NotificationsStrings.CollectionsImportInitialising,
            };

            PostNotification?.Invoke(notification);

            // Collections are cheap to copy, and the source realm context is thread-confined.
            List<(string Name, List<string> BeatmapHashes)> collections;

            using (Realm sourceRealm = storage.OpenRealm())
            {
                collections = sourceRealm.All<BeatmapCollection>()
                                         .AsEnumerable()
                                         .Select(c => (c.Name, c.BeatmapMD5Hashes.ToList()))
                                         .ToList();
            }

            realm.Write(r =>
            {
                foreach ((string name, var beatmapHashes) in collections)
                {
                    var existing = r.All<BeatmapCollection>().FirstOrDefault(c => c.Name == name);

                    if (existing == null)
                    {
                        r.Add(new BeatmapCollection(name, beatmapHashes));
                        continue;
                    }

                    foreach (string hash in beatmapHashes.Where(hash => !existing.BeatmapMD5Hashes.Contains(hash)))
                    {
                        existing.BeatmapMD5Hashes.Add(hash);
                    }

                    existing.LastModified = DateTimeOffset.UtcNow;
                }
            });

            notification.CompletionText = NotificationsStrings.CollectionsImportProgress(collections.Count);
            notification.State = ProgressNotificationState.Completed;

            return collections.Count;
        });
    }
}
