// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Overlays.Settings.Sections.DebugSettings
{
    public partial class GeneralSettings : SettingsSubsection
    {
        protected override LocalisableString Header => @"General";

        [BackgroundDependencyLoader]
        private void load(FrameworkDebugConfigManager config, FrameworkConfigManager frameworkConfig)
        {
            Add(CreateItem(new CheckboxEntry
            {
                Caption = @"Show log overlay",
                Current = frameworkConfig.GetBindable<bool>(FrameworkSetting.ShowLogOverlay)
            }));

            Add(CreateItem(new CheckboxEntry
            {
                Caption = @"Bypass front-to-back render pass",
                Current = config.GetBindable<bool>(DebugSetting.BypassFrontToBackPass)
            }));
        }
    }
}
