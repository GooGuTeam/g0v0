// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.DebugSettings
{
    public partial class GeneralSettings : SettingsSubsection
    {
        protected override LocalisableString Header => @"General";

        [BackgroundDependencyLoader]
        private void load(FrameworkDebugConfigManager config, FrameworkConfigManager frameworkConfig, OsuConfigManager osuConfig)
        {
            Add(new SettingsItemV2(new FormCheckBox
            {
                Caption = @"Show log overlay",
                Current = frameworkConfig.GetBindable<bool>(FrameworkSetting.ShowLogOverlay)
            }));

            Add(new SettingsItemV2(new FormCheckBox
            {
                Caption = @"Bypass front-to-back render pass",
                Current = config.GetBindable<bool>(DebugSetting.BypassFrontToBackPass)
            }));

            // temporary migration aid; remove once the SQLite backend is confirmed working.
            Add(new SettingsItemV2(new FormEnumDropdown<DataStoreBackend>
            {
                Caption = DebugSettingsStrings.DataStoreBackend,
                Current = osuConfig.GetBindable<DataStoreBackend>(OsuSetting.DataStoreBackend)
            })
            {
                Note = { Value = new SettingsNote.Data(DebugSettingsStrings.DataStoreBackendRestartRequired, SettingsNote.Type.Informational) }
            });
        }
    }
}
