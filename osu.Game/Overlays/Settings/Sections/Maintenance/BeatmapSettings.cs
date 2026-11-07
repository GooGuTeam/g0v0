// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Overlays.Settings.Sections.Maintenance
{
    public partial class BeatmapSettings : SettingsSubsection
    {
        protected override LocalisableString Header => CommonStrings.Beatmaps;

        private ButtonEntry deleteBeatmapsButton = null!;
        private ButtonEntry deleteBeatmapVideosButton = null!;
        private ButtonEntry resetOffsetsButton = null!;
        private ButtonEntry restoreButton = null!;
        private ButtonEntry undeleteButton = null!;

        [BackgroundDependencyLoader]
        private void load(BeatmapManager beatmaps, IDialogOverlay? dialogOverlay)
        {
            Add(CreateItem(deleteBeatmapsButton = new ButtonEntry
            {
                Caption = MaintenanceSettingsStrings.DeleteAllBeatmaps,
                Action = () =>
                {
                    dialogOverlay?.Push(new MassDeleteConfirmationDialog(() =>
                    {
                        deleteBeatmapsButton.Enabled.Value = false;
                        Task.Run(() => beatmaps.Delete()).ContinueWith(_ => Schedule(() => deleteBeatmapsButton.Enabled.Value = true));
                    }, DeleteConfirmationContentStrings.Beatmaps));
                },
                Dangerous = true,
            }));

            Add(CreateItem(deleteBeatmapVideosButton = new ButtonEntry
            {
                Caption = MaintenanceSettingsStrings.DeleteAllBeatmapVideos,
                Action = () =>
                {
                    dialogOverlay?.Push(new MassDeleteConfirmationDialog(() =>
                    {
                        deleteBeatmapVideosButton.Enabled.Value = false;
                        Task.Run(beatmaps.DeleteAllVideos).ContinueWith(_ => Schedule(() => deleteBeatmapVideosButton.Enabled.Value = true));
                    }, DeleteConfirmationContentStrings.BeatmapVideos));
                },
                Dangerous = true,
            }));

            Add(CreateItem(resetOffsetsButton = new ButtonEntry
            {
                Caption = MaintenanceSettingsStrings.ResetAllOffsets,
                Action = () =>
                {
                    dialogOverlay?.Push(new MassDeleteConfirmationDialog(() =>
                    {
                        resetOffsetsButton.Enabled.Value = false;
                        Task.Run(beatmaps.ResetAllOffsets).ContinueWith(_ => Schedule(() => resetOffsetsButton.Enabled.Value = true));
                    }, DeleteConfirmationContentStrings.Offsets));
                },
                Dangerous = true,
            }));

            AddRange(new Drawable[]
            {
                CreateItem(restoreButton = new ButtonEntry
                {
                    Caption = MaintenanceSettingsStrings.RestoreAllHiddenDifficulties,
                    Action = () =>
                    {
                        restoreButton.Enabled.Value = false;
                        Task.Run(beatmaps.RestoreAll).ContinueWith(_ => Schedule(() => restoreButton.Enabled.Value = true));
                    }
                }),
                CreateItem(undeleteButton = new ButtonEntry
                {
                    Caption = MaintenanceSettingsStrings.RestoreAllRecentlyDeletedBeatmaps,
                    Action = () =>
                    {
                        undeleteButton.Enabled.Value = false;
                        Task.Run(beatmaps.UndeleteAll).ContinueWith(_ => Schedule(() => undeleteButton.Enabled.Value = true));
                    }
                })
            });
        }
    }
}
