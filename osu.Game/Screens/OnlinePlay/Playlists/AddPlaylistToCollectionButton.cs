// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using Realms;

namespace osu.Game.Screens.OnlinePlay.Playlists
{
    public partial class AddPlaylistToCollectionButton : RoundedButton
    {
        private readonly Room room;

        private IDisposable? beatmapSubscription;
        private IDisposable? collectionSubscription;

        private BeatmapCollection? collection;
        private HashSet<string> localBeatmapHashes = new HashSet<string>();

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private IBeatmapCollectionStore collectionStore { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private INotificationOverlay? notifications { get; set; }

        public AddPlaylistToCollectionButton(Room room)
        {
            this.room = room;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Action = () =>
            {
                if (room.Playlist.Count == 0)
                    return;

                Text = "Updating collection...";
                Enabled.Value = false;

                Task.Run(() =>
                {
                    string[] hashes = realm.Run(r => getBeatmapsForPlaylist(r).Select(b => b.MD5Hash).ToArray());

                    int countBefore;
                    int countAfter;

                    BeatmapCollection? existing = collectionStore.FindByName(room.Name);

                    if (existing == null)
                    {
                        var created = new BeatmapCollection(room.Name, hashes.Distinct().ToList());
                        countBefore = 0;
                        countAfter = created.BeatmapMD5Hashes.Count;
                        collectionStore.Add(created);
                    }
                    else
                    {
                        countBefore = existing.BeatmapMD5Hashes.Count;

                        int after = 0;

                        collectionStore.Update(existing.ID, c =>
                        {
                            foreach (string hash in hashes)
                            {
                                if (!c.BeatmapMD5Hashes.Contains(hash))
                                    c.BeatmapMD5Hashes.Add(hash);
                            }

                            after = c.BeatmapMD5Hashes.Count;
                        });

                        countAfter = after;
                    }

                    Schedule(() =>
                    {
                        LocalisableString message;

                        if (countBefore == 0)
                            message = NotificationsStrings.CollectionCreated(room.Name, countAfter);
                        else
                            message = NotificationsStrings.CollectionBeatmapsAdded(room.Name, countAfter - countBefore);

                        notifications?.Post(new SimpleNotification { Text = message });
                    });
                });
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // will be updated via updateButtonState() when ready.
            Enabled.Value = false;

            if (room.Playlist.Count == 0)
                return;

            beatmapSubscription = realm.RegisterForNotifications(getBeatmapsForPlaylist, (sender, _) =>
            {
                localBeatmapHashes = sender.Select(b => b.MD5Hash).ToHashSet();
                Schedule(updateButtonState);
            });

            collectionSubscription = collectionStore.Subscribe(() =>
            {
                collection = collectionStore.FindByName(room.Name);
                Schedule(updateButtonState);
            });
        }

        private void updateButtonState()
        {
            int countToAdd = getCountToBeAdded();

            if (collection == null)
                Text = $"Create new collection with {countToAdd} beatmaps";
            else if (hasAllItemsInCollection)
                Text = "Collection complete!";
            else
                Text = $"Add {countToAdd} beatmaps to collection";

            Enabled.Value = countToAdd > 0;
        }

        private int getCountToBeAdded()
        {
            if (collection == null)
                return localBeatmapHashes.Count;

            int count = localBeatmapHashes.Count;

            foreach (string hash in localBeatmapHashes)
            {
                if (collection.BeatmapMD5Hashes.Contains(hash))
                    count--;
            }

            return count;
        }

        private IQueryable<BeatmapInfo> getBeatmapsForPlaylist(Realm r)
        {
            return r.All<BeatmapInfo>().Filter(string.Join(" OR ", room.Playlist.Select(item => $"(OnlineID == {item.Beatmap.OnlineID})").Distinct()));
        }

        private bool hasAllItemsInCollection
        {
            get
            {
                if (collection == null)
                    return false;

                return room.Playlist.DistinctBy(i => i.Beatmap.OnlineID).Count() ==
                       collection.BeatmapMD5Hashes.Count;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            beatmapSubscription?.Dispose();
            collectionSubscription?.Dispose();
        }

        public override LocalisableString TooltipText
        {
            get
            {
                if (Enabled.Value)
                    return string.Empty;

                if (hasAllItemsInCollection)
                    return "All beatmaps have been added!";

                return "Download some beatmaps first.";
            }
        }
    }
}
