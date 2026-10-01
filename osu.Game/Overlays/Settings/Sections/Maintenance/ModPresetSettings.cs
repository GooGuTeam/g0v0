// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Mods;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Maintenance
{
    public partial class ModPresetSettings : SettingsSubsection
    {
        protected override LocalisableString Header => CommonStrings.ModPresets;

        [Resolved]
        private IModPresetStore modPresetStore { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notificationOverlay { get; set; }

        private SettingsButtonV2 undeleteButton = null!;
        private SettingsButtonV2 deleteAllButton = null!;

        [BackgroundDependencyLoader]
        private void load(IDialogOverlay? dialogOverlay)
        {
            AddRange(new Drawable[]
            {
                deleteAllButton = new DangerousSettingsButtonV2
                {
                    Text = MaintenanceSettingsStrings.DeleteAllModPresets,
                    Action = () =>
                    {
                        dialogOverlay?.Push(new MassDeleteConfirmationDialog(() =>
                        {
                            deleteAllButton.Enabled.Value = false;
                            Task.Run(deleteAllModPresets).ContinueWith(t => Schedule(onAllModPresetsDeleted, t));
                        }, DeleteConfirmationContentStrings.ModPresets));
                    }
                },
                undeleteButton = new SettingsButtonV2
                {
                    Text = MaintenanceSettingsStrings.RestoreAllRecentlyDeletedModPresets,
                    Action = () => Task.Run(undeleteModPresets).ContinueWith(t => Schedule(onModPresetsUndeleted, t))
                }
            });
        }

        private bool deleteAllModPresets()
        {
            var presets = modPresetStore.GetAllUsableDetached();

            foreach (var preset in presets)
                modPresetStore.Delete(preset.ID);

            return presets.Count > 0;
        }

        private void onAllModPresetsDeleted(Task<bool> deletionTask)
        {
            deleteAllButton.Enabled.Value = true;

            if (deletionTask.IsCompletedSuccessfully)
                notificationOverlay?.Post(new ProgressCompletionNotification { Text = deletionTask.GetResultSafely() ? MaintenanceSettingsStrings.DeletedAllModPresets : MaintenanceSettingsStrings.NoModPresetsFoundToDelete });
            else if (deletionTask.IsFaulted)
                Logger.Error(deletionTask.Exception, "Failed to delete all mod presets");
        }

        private bool undeleteModPresets()
        {
            var presets = modPresetStore.GetAllDetached().Where(preset => preset.DeletePending).ToList();

            foreach (var preset in presets)
                modPresetStore.Undelete(preset.ID);

            return presets.Count > 0;
        }

        private void onModPresetsUndeleted(Task<bool> undeletionTask)
        {
            undeleteButton.Enabled.Value = true;

            if (undeletionTask.IsCompletedSuccessfully)
                notificationOverlay?.Post(new ProgressCompletionNotification { Text = undeletionTask.GetResultSafely() ? MaintenanceSettingsStrings.RestoredAllDeletedModPresets : MaintenanceSettingsStrings.NoModPresetsFoundToRestore });
            else if (undeletionTask.IsFaulted)
                Logger.Error(undeletionTask.Exception, "Failed to restore mod presets");
        }
    }
}
