// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Screens.Menu;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Menus
{
    [TestFixture]
    public partial class TestSceneLegacyMainMenu : OsuGameTestScene
    {
        [Test]
        public void TestLegacyMainMenuToggleAndInteraction()
        {
            AddStep("enable legacy main menu", () => Game.LocalConfig.SetValue(OsuSetting.ToriiLegacyMainMenu, true));

            AddUntilStep("wait for legacy buttons visible", () =>
            {
                var legacyButtons = Game.ChildrenOfType<LegacyButtonSystem>().FirstOrDefault();
                return legacyButtons != null && legacyButtons.IsPresent;
            });

            AddUntilStep("modern button system hidden", () =>
            {
                var modernButtons = Game.ChildrenOfType<ButtonSystem>().FirstOrDefault();
                return modernButtons == null || !modernButtons.IsPresent;
            });

            AddAssert("legacy music control present", () => Game.ChildrenOfType<LegacyMusicControl>().Any(m => m.IsPresent));
            AddAssert("legacy side flashes present", () => Game.ChildrenOfType<LegacyMenuSideFlashes>().Any(f => f.IsPresent));

            AddStep("open buttons with P", () => InputManager.Key(Key.P));
            AddUntilStep("legacy buttons open", () =>
            {
                var legacyButtons = Game.ChildrenOfType<LegacyButtonSystem>().FirstOrDefault();
                return legacyButtons != null && !legacyButtons.IsIdle;
            });

            AddWaitStep("settle main menu animation", 3);
            AddStep("move cursor over menu", () => InputManager.MoveMouseTo(Game.ChildrenOfType<LegacyMenuButton>().First(b => b.Kind == "options")));

            AddStep("switch to play tier with P", () => InputManager.Key(Key.P));
            AddUntilStep("on play tier", () =>
            {
                var legacyButtons = Game.ChildrenOfType<LegacyButtonSystem>().FirstOrDefault();
                return legacyButtons != null && legacyButtons.CurrentTier == LegacyButtonSystem.MenuTier.Play;
            });

            AddWaitStep("settle play menu animation", 3);

            AddStep("press escape to return to main tier", () => InputManager.Key(Key.Escape));
            AddUntilStep("on main tier", () =>
            {
                var legacyButtons = Game.ChildrenOfType<LegacyButtonSystem>().FirstOrDefault();
                return legacyButtons != null && legacyButtons.CurrentTier == LegacyButtonSystem.MenuTier.Main;
            });

            AddStep("press escape to return to idle", () => InputManager.Key(Key.Escape));
            AddUntilStep("buttons idle", () =>
            {
                var legacyButtons = Game.ChildrenOfType<LegacyButtonSystem>().FirstOrDefault();
                return legacyButtons != null && legacyButtons.IsIdle;
            });

            AddStep("disable legacy main menu", () => Game.LocalConfig.SetValue(OsuSetting.ToriiLegacyMainMenu, false));
            AddUntilStep("modern buttons restored", () =>
            {
                var modernButtons = Game.ChildrenOfType<ButtonSystem>().FirstOrDefault();
                return modernButtons != null && modernButtons.IsPresent;
            });
        }

    }
}
