// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Screens.Menu;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Menus
{
    /// <summary>
    /// The legacy main menu reached straight from the intro (the option already enabled at startup), as players normally see it.
    /// </summary>
    [TestFixture]
    public partial class TestSceneLegacyMainMenuFirstEntry : OsuGameTestScene
    {
        protected override TestOsuGame CreateTestGame() => new LegacyMenuTestGame(this);

        [Test]
        public void TestSwitchToModernRestoresLogo()
        {
            AddUntilStep("legacy menu visible", () => Game.ChildrenOfType<LegacyButtonSystem>().Any(b => b.IsPresent));
            AddWaitStep("finish arrival", 10);
            AddStep("switch to modern", () => Game.LocalConfig.SetValue(OsuSetting.ToriiLegacyMainMenu, false));
            AddUntilStep("modern logo restored", () => Game.ChildrenOfType<OsuLogo>().Any(l => l.Alpha > 0));
            AddAssert("modern buttons active", () => Game.ChildrenOfType<ButtonSystem>().Any(b => b.IsPresent));
            AddStep("switch back", () => Game.LocalConfig.SetValue(OsuSetting.ToriiLegacyMainMenu, true));
            AddUntilStep("modern logo hidden", () => Game.ChildrenOfType<OsuLogo>().All(l => l.Alpha == 0));
        }

        [Test]
        public void TestExitButtonReceivesInput()
        {
            AddUntilStep("wait for legacy buttons visible", () => Game.ChildrenOfType<LegacyButtonSystem>().FirstOrDefault()?.IsPresent == true);
            AddStep("open buttons with P", () => InputManager.Key(Key.P));
            AddWaitStep("settle", 10);
            AddStep("open settings with O", () => InputManager.Key(Key.O));
            AddUntilStep("settings open", () => Game.Settings.State.Value == osu.Framework.Graphics.Containers.Visibility.Visible);
            AddWaitStep("settle settings", 10);
            AddStep("close settings", () => Game.Settings.Hide());
            AddWaitStep("settle close", 10);
            AddStep("hover exit where the player pointed", () =>
            {
                // the player's cursor in a 1936x1020 window was at (1180, 735): (99.8, 106) stable units from the screen centre.
                var system = Game.ChildrenOfType<LegacyButtonSystem>().First();
                var content = system.ChildrenOfType<osu.Framework.Graphics.Containers.DrawSizePreservingFillContainer>().First().Child;
                InputManager.MoveMouseTo(content.ToScreenSpace(content.DrawSize / 2 + new Vector2(99.8f, 106f)));
            });
            AddWaitStep("settle hover", 3);
            AddAssert("exit button hovered", () => InputManager.HoveredDrawables.OfType<LegacyMenuButton>().Any(b => b.Kind == "exit"));
            AddStep("click exit", () => InputManager.Click(MouseButton.Left));
            AddUntilStep("menu exiting", () => Game.ScreenStack.CurrentScreen is not MainMenu);
            AddAssert("lazer logo stays hidden", () => !Game.ChildrenOfType<OsuLogo>().Any(l => l.Alpha > 0));
        }

        private partial class LegacyMenuTestGame : TestOsuGame
        {
            public LegacyMenuTestGame(TestSceneLegacyMainMenuFirstEntry scene)
                : base(scene.LocalStorage, scene.API)
            {
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                LocalConfig.SetValue(OsuSetting.ToriiLegacyMainMenu, true);
            }
        }
    }
}
