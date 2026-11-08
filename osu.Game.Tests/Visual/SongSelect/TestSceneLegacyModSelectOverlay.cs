// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
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

        [BackgroundDependencyLoader]
        private void load(RulesetStore rulesets)
        {
            var osuRuleset = rulesets.GetRuleset(0)!.CreateInstance();

            Child = overlay = new LegacyModSelectOverlay
            {
                RelativeSizeAxes = Axes.Both,
            };

            overlay.Ruleset.Value = osuRuleset.RulesetInfo;
            overlay.Beatmap.Value = new DummyWorkingBeatmap(Audio, null);
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("reset state", () =>
            {
                overlay.Hide();
                overlay.ResetAllMods();
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
