// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Scoring;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Overlays.Settings.Sections.Maintenance
{
    public partial class ScoreSettings : SettingsSubsection
    {
        protected override LocalisableString Header => CommonStrings.Scores;

        private ButtonEntry deleteScoresButton = null!;

        [BackgroundDependencyLoader]
        private void load(ScoreManager scores, IDialogOverlay? dialogOverlay)
        {
            Add(CreateItem(deleteScoresButton = new ButtonEntry
            {
                Caption = MaintenanceSettingsStrings.DeleteAllScores,
                Action = () =>
                {
                    dialogOverlay?.Push(new MassDeleteConfirmationDialog(() =>
                    {
                        deleteScoresButton.Enabled.Value = false;
                        Task.Run(() => scores.Delete()).ContinueWith(_ => Schedule(() => deleteScoresButton.Enabled.Value = true));
                    }, DeleteConfirmationContentStrings.Scores));
                },
                Dangerous = true,
            }));
        }
    }
}
