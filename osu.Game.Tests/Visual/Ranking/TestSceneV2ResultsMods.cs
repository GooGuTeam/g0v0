// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Ranking;
using osu.Game.Tests.Resources;
using osuTK;

namespace osu.Game.Tests.Visual.Ranking
{
    public partial class TestSceneV2ResultsMods : OsuManualInputManagerTestScene
    {
        [TestCase(3)]
        [TestCase(30)]
        public void TestCompactHoverAndClipping(int count)
        {
            V2ResultsPanel panel = null!;
            ModDisplay mods = null!;
            Container viewport = null!;
            float compactWidth = 0;
            AddStep("load many mods", () =>
            {
                var score = TestResources.CreateTestScoreInfo();
                score.Mods = Enumerable.Range(0, count).Select(_ => (Mod)new OsuModHardRock()).ToArray();
                Child = panel = new V2ResultsPanel(score);
                InputManager.MoveMouseTo(Vector2.Zero);
            });
            AddUntilStep("loaded", () => panel.IsLoaded);
            AddStep("record compact width", () =>
            {
                mods = panel.ChildrenOfType<ModDisplay>().Single();
                viewport = panel.ChildrenOfType<Container>().Single(c => c.Name == "Mods area");
                compactWidth = mods.DrawWidth;
            });
            AddAssert("hover expansion enabled", () => mods.ExpansionMode, () => Is.EqualTo(ExpansionMode.ExpandOnHover));
            AddAssert("overflow is clipped not scaled", () => viewport.Masking && mods.Parent!.Scale == Vector2.One);
            AddStep("hover mods", () => InputManager.MoveMouseTo(viewport.ScreenSpaceDrawQuad.TopLeft + new Vector2(10, 10)));
            AddUntilStep("mods expand", () => mods.DrawWidth > compactWidth + 5);
            if (count > 3)
                AddAssert("long expanded list exceeds clipped viewport", () => mods.DrawWidth * mods.Scale.X > viewport.DrawWidth);
            AddStep("leave mods", () => InputManager.MoveMouseTo(Vector2.Zero));
            AddUntilStep("mods contract again", () => mods.DrawWidth <= compactWidth + 1);
        }
    }
}
