// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Localisation;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Overlays.Settings.Sections.Maintenance
{
    public partial class CollectionsSettings : SettingsSubsection
    {
        protected override LocalisableString Header => CommonStrings.Collections;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notificationOverlay { get; set; }

        [BackgroundDependencyLoader]
        private void load(IDialogOverlay? dialogOverlay)
        {
            Add(CreateItem(new ButtonEntry
            {
                Caption = MaintenanceSettingsStrings.DeleteAllCollections,
                Action = () =>
                {
                    dialogOverlay?.Push(new MassDeleteConfirmationDialog(deleteAllCollections, DeleteConfirmationContentStrings.Collections));
                },
                Dangerous = true,
            }));
        }

        private void deleteAllCollections()
        {
            bool anyDeleted = realm.Write(r =>
            {
                if (r.All<BeatmapCollection>().Any())
                {
                    r.RemoveAll<BeatmapCollection>();
                    return true;
                }
                else
                {
                    return false;
                }
            });

            notificationOverlay?.Post(new ProgressCompletionNotification { Text = anyDeleted ? MaintenanceSettingsStrings.DeletedAllCollections : MaintenanceSettingsStrings.NoCollectionsFoundToDelete });
        }
    }
}
