// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Rendering;
using osu.Game.Overlays.Settings.Rendering.Stable;
using osu.Game.Overlays.Settings.Sections.Gameplay;
using osu.Game.Overlays.Settings.Sections.UserInterface;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Taiko.Mods;
using osu.Game.Skinning;
using osu.Game.Skinning.Select;

namespace osu.Game.Tests.Visual.Settings
{
    [TestFixture]
    public partial class TestSceneDefaultModPresets : OsuGameTestScene
    {
        [Test]
        public void TestAutoApplyOnStartupAndRulesetSwitch()
        {
            Guid osuPresetId = Guid.Empty;
            RulesetInfo osuRuleset = null!;
            RulesetInfo taikoRuleset = null!;

            AddStep("create saved preset for osu!", () =>
            {
                var rulesetStore = Game.Dependencies.Get<RulesetStore>();
                osuRuleset = rulesetStore.GetRuleset(0)!;
                taikoRuleset = rulesetStore.GetRuleset(1)!;

                osuPresetId = Guid.NewGuid();

                Game.Realm.Write(r =>
                {
                    r.RemoveAll<ModPreset>();
                    r.Add(new ModPreset
                    {
                        ID = osuPresetId,
                        Name = @"Classic SV1",
                        Ruleset = r.Find<RulesetInfo>(osuRuleset.ShortName)!,
                        Mods = new Mod[] { new OsuModScoreV1(), new OsuModClassic() },
                    });
                });

                Game.Ruleset.Value = osuRuleset;
                Game.SelectedMods.Value = Array.Empty<Mod>();
                DefaultModPresetHelper.SetPresetId(Game.LocalConfig, osuRuleset, osuPresetId);
            });

            AddAssert("mods remain empty while feature disabled", () => Game.SelectedMods.Value.Count == 0);

            AddStep("enable default mod presets", () =>
            {
                Game.LocalConfig.SetValue(OsuSetting.UseDefaultModPresets, true);
                Game.LocalConfig.Save();
            });

            AddAssert("preset immediately applied", () =>
                Game.SelectedMods.Value.Count == 2
                && Game.SelectedMods.Value.OfType<OsuModScoreV1>().Any()
                && Game.SelectedMods.Value.OfType<OsuModClassic>().Any());

            AddStep("switch to taiko with hidden mod", () =>
            {
                Game.Ruleset.Value = taikoRuleset;
                Game.SelectedMods.Value = new Mod[] { new TaikoModHidden() };
            });

            AddStep("switch back to osu!", () => Game.Ruleset.Value = osuRuleset);

            AddAssert("osu! preset overrides converted mods", () =>
                Game.SelectedMods.Value.Count == 2
                && Game.SelectedMods.Value.OfType<OsuModScoreV1>().Any()
                && Game.SelectedMods.Value.OfType<OsuModClassic>().Any());

            AddStep("restart game on same storage", () =>
            {
                Remove(Game, true);
                CreateGame();
            });

            AddUntilStep("wait for restarted game load", () => Game.IsLoaded);
            ConfirmAtMainMenu();

            AddAssert("startup auto-applied default preset", () =>
                Game.SelectedMods.Value.Count == 2
                && Game.SelectedMods.Value.OfType<OsuModScoreV1>().Any()
                && Game.SelectedMods.Value.OfType<OsuModClassic>().Any());
        }

        [Test]
        public void TestStableBackButtonAndThemeMusicSettings()
        {
            Container testRoot = null!;
            StableBackButton stableBackButton = null!;
            LegacyBackButton skinnableBackButton = null!;
            ThemeMusicSettings themeMusicSettings = null!;
            ModsSettings modsSettings = null!;

            AddStep("create controls under stable renderer and menu-back skin", () =>
            {
                Game.Realm.Write(r => r.RemoveAll<ModPreset>());

                var colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

                Game.Add(testRoot = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[]
                    {
                        (typeof(OverlayColourProvider), colourProvider),
                        (typeof(ISettingsRenderer), StableSettingsRenderer.DEFAULT),
                    },
                    Children = new Drawable[]
                    {
                        new SkinProvidingContainer(new MenuBackTestSkin(Game.Dependencies.Get<IRenderer>().WhitePixel))
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                stableBackButton = new StableBackButton(),
                                skinnableBackButton = new LegacyBackButton(),
                            },
                        },
                        new FillFlowContainer
                        {
                            Width = 400,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Children = new Drawable[]
                            {
                                themeMusicSettings = new ThemeMusicSettings(),
                                modsSettings = new ModsSettings(),
                            },
                        },
                    },
                });
            });

            AddUntilStep("wait for controls to load", () =>
                stableBackButton.IsLoaded
                && skinnableBackButton.IsLoaded
                && themeMusicSettings.IsLoaded
                && modsSettings.IsLoaded);

            AddAssert("skinnable LegacyBackButton uses menu-back piece", () =>
                skinnableBackButton.AllowSkinning
                && skinnableBackButton.ChildrenOfType<LegacyOldBackButtonPiece>().Any());

            AddAssert("StableBackButton disables skinning and uses layered piece", () =>
                !stableBackButton.ChildrenOfType<LegacyBackButton>().Single().AllowSkinning
                && stableBackButton.ChildrenOfType<LegacyNewBackButtonPiece>().Any()
                && !stableBackButton.ChildrenOfType<LegacyOldBackButtonPiece>().Any());

            AddAssert("ThemeMusicSettings uses StableCheckbox and StableButton", () =>
                themeMusicSettings.ChildrenOfType<StableCheckbox>().Count() == 2
                && themeMusicSettings.ChildrenOfType<StableButton>().Count() == 1
                && !themeMusicSettings.ChildrenOfType<SettingsItemV2>().Any()
                && !themeMusicSettings.ChildrenOfType<SettingsButtonV2>().Any());

            AddAssert("ModsSettings renders per-ruleset StableDropdowns", () =>
                modsSettings.ChildrenOfType<StableCheckbox>().Count() == 2
                && modsSettings.ChildrenOfType<StableDropdown<ModsSettings.ModPresetChoice>>().Any());

            AddStep("add preset while ModsSettings is open", () =>
            {
                var osuRuleset = Game.Dependencies.Get<RulesetStore>().GetRuleset(0)!;

                Game.Realm.Write(r =>
                {
                    r.Add(new ModPreset
                    {
                        ID = Guid.NewGuid(),
                        Name = @"Live Preset",
                        Ruleset = r.Find<RulesetInfo>(osuRuleset.ShortName)!,
                        Mods = new Mod[] { new OsuModScoreV1() },
                    });
                });
            });

            AddUntilStep("ModsSettings dropdown live-updated with new preset", () =>
                modsSettings.ChildrenOfType<StableDropdown<ModsSettings.ModPresetChoice>>()
                            .Any(d => d.FilterTerms.Any(t => t.ToString().Contains(@"Live Preset", StringComparison.Ordinal))));

            AddStep("clean up test root", () => testRoot.Expire());
        }

        private class MenuBackTestSkin : ISkin
        {
            private readonly Texture menuBackTexture;

            public MenuBackTestSkin(Texture menuBackTexture)
            {
                this.menuBackTexture = menuBackTexture;
            }

            public Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => null;

            public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT)
                => componentName == @"menu-back" ? menuBackTexture : null;

            public ISample? GetSample(ISampleInfo sampleInfo) => null;

            public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
                where TLookup : notnull
                where TValue : notnull
                => null;
        }
    }
}
