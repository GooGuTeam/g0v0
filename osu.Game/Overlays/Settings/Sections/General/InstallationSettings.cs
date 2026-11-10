// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Sections.Maintenance;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Overlays.Settings.Sections.General
{
    public partial class InstallationSettings : SettingsSubsection
    {
        protected override LocalisableString Header => GeneralSettingsStrings.InstallationHeader;

        [Resolved]
        private OsuGame? game { get; set; }

        [BackgroundDependencyLoader]
        private void load(Storage storage)
        {
            Add(CreateItem(new ButtonEntry
            {
                Caption = GeneralSettingsStrings.OpenOsuFolder,
                Keywords = new[] { @"logs", @"files", @"access", "directory" },
                Action = () => storage.PresentExternally(),
            }));

            Add(CreateItem(new ButtonEntry
            {
                Caption = GeneralSettingsStrings.ChangeFolderLocation,
                Action = () => game?.PerformFromScreen(menu => menu.Push(new MigrationSelectScreen())),
                Dangerous = true,
            }));

            // This is a temporary stand-in until we have a setup for `SettingsItemV2` which can handle buttons.
            Add(CreateItem(new NoteEntry
            {
                AttachedToPrevious = true,
                Note =
                {
                    Value = new SettingsNote.Data(GeneralSettingsStrings.ChangeFolderLocationTooltip, SettingsNote.Type.Informational)
                }
            }));
        }
    }
}
