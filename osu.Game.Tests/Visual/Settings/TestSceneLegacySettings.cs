// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;

namespace osu.Game.Tests.Visual.Settings
{
    [TestFixture]
    public partial class TestSceneLegacySettings : OsuManualInputManagerTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private SettingsOverlay settings = null!;

        [Test]
        public void TestLegacySettingsToggle()
        {
            AddStep("enable legacy options", () => config.SetValue(OsuSetting.ToriiLegacyOptions, true));

            AddStep("create settings", () =>
            {
                settings?.Expire();
                Add(settings = new SettingsOverlay
                {
                    State = { Value = Visibility.Visible }
                });
            });

            AddUntilStep("wait for items to load", () => settings.SectionsContainer.ChildrenOfType<IFilterable>().Any());

            AddAssert("sidebar buttons exist", () => settings.ChildrenOfType<ISettingsSidebarButton>().Any());

            AddStep("disable legacy options", () => config.SetValue(OsuSetting.ToriiLegacyOptions, false));
        }
    }
}
