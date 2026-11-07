// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Cursor;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Ranking;
using osu.Game.Tests.Resources;
using osu.Framework.Extensions;
using osu.Framework.Screens;
using osu.Game.Configuration;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;
using osu.Game.Tests.Beatmaps.IO;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Navigation
{
    [Description("the loading screen and gameplay held to a fixed aspect ratio")]
    public partial class TestSceneGameplayAspectRatio : OsuGameTestScene
    {
        [Test]
        public void TestReturnToLegacyMenuKeepsToolbarHidden()
        {
            MainMenu? menu = null;
            AddUntilStep("main menu ready", () => (menu = Game.ScreenStack.CurrentScreen as MainMenu) != null);
            AddStep("enable legacy screens", () =>
            {
                Game.LocalConfig.SetValue(OsuSetting.ToriiLegacyMainMenu, true);
                Game.LocalConfig.SetValue(OsuSetting.ToriiLegacyFooterUseSkin, true);
            });
            PushAndConfirm(() => new SoloSongSelect());
            AddStep("return to main menu", () => menu!.MakeCurrent());
            AddUntilStep("main menu current", () => menu!.IsCurrentScreen());
            AddAssert("toolbar not shown on return", () => Game.Toolbar.State.Value == Visibility.Hidden && Game.Toolbar.Alpha == 0);
        }

        [TestCase(LegacySongSelectAspectRatio.Widescreen, 16f / 9)]
        [TestCase(LegacySongSelectAspectRatio.WidescreenTall, 16f / 10)]
        public void TestLegacyResultsUseSkinRatioAndCursor(LegacySongSelectAspectRatio ratio, float expected)
        {
            SoloResultsScreen results = null!;
            AddStep("configure legacy results", () =>
            {
                Game.LocalConfig.SetValue(OsuSetting.ToriiStableResults, true);
                Game.LocalConfig.SetValue(OsuSetting.ToriiLegacySongSelectAspectRatio, ratio);
            });
            PushAndConfirm(() => results = new SoloResultsScreen(TestResources.CreateTestScoreInfo()));
            AddUntilStep("results aspect ratio applied", () => System.Math.Abs(results.DrawWidth / results.DrawHeight - expected) < 0.01f);
            AddAssert("gameplay cursor selected", () => Game.ChildrenOfType<GlobalCursorDisplay>().Single().UseGameplayCursor);
            AddAssert("toolbar remains hidden", () => Game.Toolbar.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestResultsPresentationChangesUpdateGlobalChrome()
        {
            SoloResultsScreen results = null!;
            float windowRatio = 0;
            AddStep("configure modern results", () =>
            {
                Game.LocalConfig.SetValue(OsuSetting.ToriiStableResults, false);
                Game.LocalConfig.SetValue(OsuSetting.ToriiLegacySongSelectAspectRatio, LegacySongSelectAspectRatio.WidescreenTall);
            });
            PushAndConfirm(() => results = new SoloResultsScreen(TestResources.CreateTestScoreInfo()));
            AddStep("remember window ratio", () => windowRatio = results.DrawWidth / results.DrawHeight);
            AddStep("enable legacy while results are open", () => Game.LocalConfig.SetValue(OsuSetting.ToriiStableResults, true));
            AddUntilStep("fixed ratio applied live", () => System.Math.Abs(results.DrawWidth / results.DrawHeight - 1.6f) < 0.01f);
            AddUntilStep("toolbar hidden live", () => Game.Toolbar.State.Value == Visibility.Hidden && Game.Toolbar.Alpha == 0);
            AddUntilStep("legacy cursor applied live", () => Game.ChildrenOfType<GlobalCursorDisplay>().Single().UseGameplayCursor);
            AddStep("disable legacy while results are open", () => Game.LocalConfig.SetValue(OsuSetting.ToriiStableResults, false));
            AddUntilStep("window ratio restored", () => System.Math.Abs(results.DrawWidth / results.DrawHeight - windowRatio) < 0.01f);
            AddUntilStep("modern cursor restored", () => !Game.ChildrenOfType<GlobalCursorDisplay>().Single().UseGameplayCursor);
            AddAssert("same screen retained", () => ReferenceEquals(Game.ScreenStack.CurrentScreen, results));
        }

        [TestCase(LegacySongSelectAspectRatio.Widescreen)]
        [TestCase(LegacySongSelectAspectRatio.WidescreenTall)]
        public void TestGameplay(LegacySongSelectAspectRatio ratio)
        {
            SoloSongSelect songSelect = null!;

            AddStep($"set gameplay aspect ratio to {ratio}", () => Game.LocalConfig.SetValue(OsuSetting.ToriiLegacySongSelectAspectRatio, ratio));

            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddUntilStep("wait for song select", () => songSelect.CarouselItemsPresented);
            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());
            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            AddStep("start gameplay", () => InputManager.Key(Key.Enter));
            AddUntilStep("wait for loader", () => Game.ScreenStack.CurrentScreen is PlayerLoader);
            AddUntilStep("wait for player", () => Game.ScreenStack.CurrentScreen is Player);
            AddWaitStep("let gameplay settle", 10);

            AddStep("return to song select", () => songSelect.MakeCurrent());
            AddUntilStep("back at song select", () => songSelect.IsCurrentScreen());
        }
    }
}
