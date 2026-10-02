using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterfaceV2;

namespace osu.Game.Overlays.Settings.Sections.DebugSettings
{
    public partial class GraphicsSettings : SettingsSubsection
    {
        protected override LocalisableString Header => @"Graphics";

        [BackgroundDependencyLoader]
        private void load(FrameworkConfigManager frameworkConfig)
        {
            Add(new SettingsItemV2(new FormCheckBox
            {
                Caption = @"Use load error placeholders (will apply after restart)",
                Current = frameworkConfig.GetBindable<bool>(FrameworkSetting.LoadErrorPlaceholders)
            }));
        }
    }
}
