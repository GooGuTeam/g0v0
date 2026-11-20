// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Select;
using osu.Game.Skinning;
using osuTK.Input;

namespace osu.Game.Tests.Visual.SongSelect
{
    [TestFixture]
    public partial class TestSceneLegacyModSelectOverlay : OsuManualInputManagerTestScene
    {
        private LegacyModSelectOverlay overlay = null!;
        private RealmRulesetStore rulesets = null!;
        private OsuConfigManager localConfig = null!;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.Cache(rulesets = new RealmRulesetStore(Realm));
            dependencies.Cache(Realm);
            dependencies.Cache(localConfig = new OsuConfigManager(LocalStorage));
            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = overlay = new LegacyModSelectOverlay
            {
                RelativeSizeAxes = Axes.Both,
            };

            overlay.Ruleset.Value = rulesets.GetRuleset(0)!;
            overlay.Beatmap.Value = new DummyWorkingBeatmap(Audio, null);
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("reset state", () =>
            {
                localConfig.SetValue(OsuSetting.UseDefaultModPresets, false);
                localConfig.SetValue(OsuSetting.DefaultModPresets, string.Empty);
                Realm.Write(r => r.RemoveAll<ModPreset>());

                overlay.RequestMoreMods = null;
                overlay.Ruleset.Value = rulesets.GetRuleset(0)!;
                overlay.Hide();
                overlay.SelectedMods.Value = Array.Empty<Mod>();
            });
        }

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        /// <summary>
        /// Switches to the legacy skin folder named by <c>G0V0_PREVIEW_SKIN</c> while the overlay is open; its mod icons should follow.
        /// </summary>
        [Test]
        public void TestSkinChangeUpdatesIcons()
        {
            string? directory = Environment.GetEnvironmentVariable(@"G0V0_PREVIEW_SKIN");

            if (string.IsNullOrEmpty(directory))
                Assert.Ignore(@"Set G0V0_PREVIEW_SKIN to a legacy skin folder to run this test.");

            AddStep("show overlay", () => overlay.Show());
            AddStep("switch skin", () => skins.CurrentSkinInfo.Value = skins.Import(new ImportTask(directory)).GetResultSafely()!);
            AddWaitStep("let icons rebuild", 5);
            AddStep("press Q (Easy)", () => InputManager.Key(Key.Q));
            AddWaitStep("let the icon settle", 5);
        }

        [Test]
        public void TestOverlayVisibility()
        {
            AddStep("show overlay", () => overlay.Show());
            AddUntilStep("overlay is visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("hide overlay", () => overlay.Hide());
            AddUntilStep("overlay is hidden", () => overlay.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestModSelectionAndIncompatibility()
        {
            AddStep("show overlay", () => overlay.Show());
            AddUntilStep("overlay is visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("press Q (Easy)", () => InputManager.Key(Key.Q));
            AddUntilStep("Easy is selected", () => overlay.SelectedMods.Value.Any(m => m is OsuModEasy));

            AddStep("press W (NoFail)", () => InputManager.Key(Key.W));
            AddUntilStep("Easy and NoFail selected", () =>
                overlay.SelectedMods.Value.Any(m => m is OsuModEasy) &&
                overlay.SelectedMods.Value.Any(m => m is OsuModNoFail));

            AddStep("press A (HardRock)", () => InputManager.Key(Key.A));
            AddUntilStep("HardRock replaced Easy", () =>
                overlay.SelectedMods.Value.Any(m => m is OsuModHardRock) &&
                overlay.SelectedMods.Value.Any(m => m is OsuModNoFail) &&
                !overlay.SelectedMods.Value.Any(m => m is OsuModEasy));

            AddStep("press BackSpace (Reset)", () => InputManager.Key(Key.BackSpace));
            AddUntilStep("all mods reset", () => overlay.SelectedMods.Value.Count == 0);
        }

        [Test]
        public void TestModCycling()
        {
            AddStep("show overlay", () => overlay.Show());
            AddUntilStep("overlay is visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("press S (SuddenDeath)", () => InputManager.Key(Key.S));
            AddUntilStep("SuddenDeath selected", () => overlay.SelectedMods.Value.Any(m => m is OsuModSuddenDeath));

            AddStep("press S again (Perfect)", () => InputManager.Key(Key.S));
            AddUntilStep("Perfect selected", () => overlay.SelectedMods.Value.Any(m => m is OsuModPerfect) && !overlay.SelectedMods.Value.Any(m => m is OsuModSuddenDeath));

            AddStep("press S again (None)", () => InputManager.Key(Key.S));
            AddUntilStep("no death mod selected", () => !overlay.SelectedMods.Value.Any(m => m is ModSuddenDeath));
        }

        [Test]
        public void TestResetButtonAndClose()
        {
            AddStep("show overlay", () => overlay.Show());
            AddUntilStep("overlay is visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("select HardRock", () => InputManager.Key(Key.A));
            AddUntilStep("HardRock selected", () => overlay.SelectedMods.Value.Count > 0);

            AddStep("press 1 (Reset option)", () => InputManager.Key(Key.Number1));
            AddUntilStep("mods cleared", () => overlay.SelectedMods.Value.Count == 0);

            AddStep("press 2 (Close option)", () => InputManager.Key(Key.Number2));
            AddUntilStep("overlay hidden", () => overlay.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestMoreModsButtonAndHotkeys()
        {
            int moreModsCount = 0;

            AddStep("wire RequestMoreMods", () =>
            {
                moreModsCount = 0;
                overlay.RequestMoreMods = () => moreModsCount++;
            });

            AddStep("show overlay", () => overlay.Show());
            AddUntilStep("overlay is visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("click More Mods button", () =>
            {
                InputManager.MoveMouseTo(overlay.MoreModsButton);
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("callback invoked once", () => moreModsCount == 1);

            AddStep("press M", () => InputManager.Key(Key.M));
            AddAssert("callback invoked twice", () => moreModsCount == 2);

            AddStep("press 3 in osu! ruleset", () => InputManager.Key(Key.Number3));
            AddAssert("callback invoked three times", () => moreModsCount == 3);
        }

        [Test]
        public void TestTwoStageDefaultModPresetReset()
        {
            AddStep("configure SV1 default preset", () =>
            {
                Guid presetId = Guid.Empty;

                Realm.Write(r =>
                {
                    var preset = new ModPreset
                    {
                        Name = "SV1 Default",
                        Ruleset = r.Find<RulesetInfo>("osu")!,
                        Mods = new Mod[] { new OsuModScoreV1() },
                    };
                    r.Add(preset);
                    presetId = preset.ID;
                });

                localConfig.SetValue(OsuSetting.UseDefaultModPresets, true);
                DefaultModPresetHelper.SetPresetId(localConfig, overlay.Ruleset.Value, presetId);
            });

            AddStep("select HR + SV1", () => overlay.SelectedMods.Value = new Mod[] { new OsuModScoreV1(), new OsuModHardRock() });
            AddStep("show overlay", () => overlay.Show());
            AddUntilStep("overlay is visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("press 1 (first reset -> default preset)", () => InputManager.Key(Key.Number1));
            AddAssert("reset to SV1 preset", () => overlay.SelectedMods.Value.Count == 1 && overlay.SelectedMods.Value.Single() is OsuModScoreV1);

            AddStep("press 1 again (second reset -> empty)", () => InputManager.Key(Key.Number1));
            AddAssert("all mods cleared", () => overlay.SelectedMods.Value.Count == 0);
        }

        [Test]
        public void TestManiaMirrorAndHotkeys()
        {
            int moreModsCount = 0;

            AddStep("switch to mania ruleset", () =>
            {
                moreModsCount = 0;
                overlay.RequestMoreMods = () => moreModsCount++;
                overlay.Ruleset.Value = rulesets.GetRuleset(3)!;
            });

            AddStep("show overlay", () => overlay.Show());
            AddUntilStep("overlay is visible", () => overlay.State.Value == Visibility.Visible);

            AddAssert("Mirror button is present in row 2", () =>
                overlay.ChildrenOfType<LegacyModButton>().Any(b => b.AvailableMods.Any(m => m is ManiaModMirror)));

            AddStep("click Mirror button", () =>
            {
                var mirrorButton = overlay.ChildrenOfType<LegacyModButton>().Single(b => b.AvailableMods.Any(m => m is ManiaModMirror));
                InputManager.MoveMouseTo(mirrorButton);
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("Mirror mod selected", () => overlay.SelectedMods.Value.Any(m => m is ManiaModMirror));

            AddStep("press 7 (7K)", () => InputManager.Key(Key.Number7));
            AddAssert("7K selected", () => overlay.SelectedMods.Value.Any(m => m is ManiaModKey7));

            AddStep("press 0 (clear key-count mod)", () => InputManager.Key(Key.Number0));
            AddAssert("key-count mod cleared while Mirror remains", () =>
                !overlay.SelectedMods.Value.Any(m => m is ManiaKeyMod) &&
                overlay.SelectedMods.Value.Any(m => m is ManiaModMirror));

            AddStep("press 3 in mania (3K, not More Mods)", () => InputManager.Key(Key.Number3));
            AddAssert("3K selected and More Mods not triggered", () =>
                overlay.SelectedMods.Value.Any(m => m is ManiaModKey3) && moreModsCount == 0);

            AddStep("press Keypad0 (clear key-count mod)", () => InputManager.Key(Key.Keypad0));
            AddAssert("3K cleared", () => !overlay.SelectedMods.Value.Any(m => m is ManiaKeyMod));

            AddStep("press X (Random in mania)", () => InputManager.Key(Key.X));
            AddAssert("Random selected", () => overlay.SelectedMods.Value.Any(m => m is ManiaModRandom));

            AddStep("press X again (clear Random)", () => InputManager.Key(Key.X));
            AddAssert("Random cleared", () => !overlay.SelectedMods.Value.Any(m => m is ManiaModRandom));

            AddStep("press C (no-op in mania)", () => InputManager.Key(Key.C));
            AddAssert("Random and DualStages not selected by C", () =>
                !overlay.SelectedMods.Value.Any(m => m is ManiaModRandom) &&
                !overlay.SelectedMods.Value.Any(m => m is ManiaModDualStages));
        }

        [Test]
        public void TestModTooltipIsLocalisable()
        {
            LegacyModButton hidden = null!;

            AddStep("show overlay", () => overlay.Show());
            AddUntilStep("overlay is visible", () => overlay.State.Value == Visibility.Visible);
            AddStep("find Hidden", () => hidden = overlay.ChildrenOfType<LegacyModButton>().First(b => b.AvailableMods.Any(m => m is OsuModHidden)));
            AddStep("hover Hidden", () => InputManager.MoveMouseTo(hidden));
            AddWaitStep("wait for tooltip", 5);
        }

        [Test]
        public void TestDialogButtonHover()
        {
            AddStep("show overlay", () => overlay.Show());
            AddUntilStep("overlay is visible", () => overlay.State.Value == Visibility.Visible);
            AddStep("hover reset button", () => InputManager.MoveMouseTo(overlay.ChildrenOfType<LegacyDialogButton>().First()));
            AddWaitStep("wait for hover", 3);
        }

        [Test]
        public void TestAutoplayAndCinemaAvailable()
        {
            AddStep("show overlay", () => overlay.Show());
            AddUntilStep("overlay is visible", () => overlay.State.Value == Visibility.Visible);

            AddAssert("Autoplay/Cinema button exists", () =>
                overlay.ChildrenOfType<LegacyModButton>().Any(b =>
                    b.AvailableMods.Any(m => m is ModAutoplay && m is not ModCinema) &&
                    b.AvailableMods.Any(m => m is ModCinema)));

            AddStep("press V to select Autoplay", () => InputManager.Key(Key.V));
            AddAssert("Autoplay selected", () => overlay.SelectedMods.Value.Any(m => m is ModAutoplay && m is not ModCinema));

            AddStep("press V again to cycle to Cinema", () => InputManager.Key(Key.V));
            AddAssert("Cinema selected", () => overlay.SelectedMods.Value.Any(m => m is ModCinema));

            AddStep("press V again to clear Autoplay/Cinema", () => InputManager.Key(Key.V));
            AddAssert("Autoplay/Cinema cleared", () => !overlay.SelectedMods.Value.Any(m => m is ModAutoplay));
        }

        [Test]
        public void TestModifiedF1NotConsumed()
        {
            AddStep("show overlay", () => overlay.Show());
            AddUntilStep("overlay is visible", () => overlay.State.Value == Visibility.Visible);
            AddStep("press Ctrl+F1", () => InputManager.PressKey(Key.ControlLeft));
            AddStep("press F1", () => InputManager.Key(Key.F1));
            AddStep("release Ctrl", () => InputManager.ReleaseKey(Key.ControlLeft));
            AddAssert("overlay still visible", () => overlay.State.Value == Visibility.Visible);
        }
    }
}
