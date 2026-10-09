// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.UserInterface
{
    public partial class ResultsSettings : SettingsSubsection
    {
        protected override LocalisableString Header => UserInterfaceStrings.ResultsHeader;

        private Bindable<bool> legacyResults = null!;
        private Bindable<bool> v2Results = null!;
        private readonly Bindable<ResultsScreenStyle> style = new Bindable<ResultsScreenStyle>();
        private bool syncingStyle;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            // Retain the existing configuration keys so older preferences require no migration.
            legacyResults = config.GetBindable<bool>(OsuSetting.ToriiStableResults);
            v2Results = config.GetBindable<bool>(OsuSetting.UseV2ResultsScreen);

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormEnumDropdown<ResultsScreenStyle>
                {
                    Caption = UserInterfaceStrings.ResultsScreenStyle,
                    HintText = UserInterfaceStrings.ResultsScreenStyleDescription,
                    Current = style,
                    NewFeatureId = NewFeatureRegistry.StableResults,
                })
                {
                    Keywords = new[] { @"results", @"ranking", @"score", @"modern", @"legacy", @"stable", @"layout", @"skin" },
                },
            };

            legacyResults.BindValueChanged(_ => syncStyle(), true);
            v2Results.BindValueChanged(_ => syncStyle());
            style.BindValueChanged(e =>
            {
                if (syncingStyle)
                    return;

                syncingStyle = true;
                legacyResults.Value = e.NewValue == ResultsScreenStyle.Legacy;
                v2Results.Value = e.NewValue == ResultsScreenStyle.V2;
                syncingStyle = false;
            });
        }

        private void syncStyle()
        {
            if (syncingStyle)
                return;

            syncingStyle = true;
            // Match the results screen's existing precedence when both old flags are enabled.
            style.Value = legacyResults.Value ? ResultsScreenStyle.Legacy : v2Results.Value ? ResultsScreenStyle.V2 : ResultsScreenStyle.Default;
            syncingStyle = false;
        }
    }
}
