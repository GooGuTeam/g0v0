// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Tests
{
    public partial class TestSceneOsuPlayfieldAlignment : OsuTestScene
    {
        private Container playfield = null!;
        private ScalingContainer gameplayArea = null!;

        [Test]
        public void TestDefaultGameplayMatchesStable()
        {
            AddStep("create production gameplay layout", () =>
            {
                // Use the same scaling and playfield-adjustment hierarchy as Player and
                // DrawableOsuRuleset, not a fixture-supplied pixel position or ratio.
                Child = gameplayArea = new ScalingContainer(ScalingMode.Gameplay)
                {
                    Child = new OsuPlayfieldAdjustmentContainer
                    {
                        AlignWithStoryboard = true,
                        Child = playfield = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Color4.Gray,
                                },
                                new Box { Size = new Vector2(8), Colour = Color4.Yellow },
                                new Box { Position = new Vector2(512, 384), Origin = Anchor.BottomRight, Size = new Vector2(8), Colour = Color4.Yellow },
                            },
                        },
                    },
                };
            });
            AddWaitStep("settle actual layout", 5);
            AddAssert("playfield matches stable default gameplay rectangle", () =>
            {
                // Compare against the available gameplay region (the visual test browser
                // may reserve space for its step list), not the outer desktop window.
                Vector2 areaOrigin = gameplayArea.ToScreenSpace(Vector2.Zero);
                Vector2 screen = gameplayArea.ToScreenSpace(gameplayArea.DrawSize) - areaOrigin;
                float ratio = screen.Y / 480;
                Vector2 expectedSize = new Vector2(512, 384) * ratio;
                Vector2 expectedOrigin = areaOrigin + new Vector2((screen.X - expectedSize.X) / 2, (screen.Y - expectedSize.Y) * 0.75f - 16 * ratio);
                Vector2 origin = playfield.ToScreenSpace(Vector2.Zero);
                Vector2 size = playfield.ToScreenSpace(new Vector2(512, 384)) - origin;
                return (origin - expectedOrigin).Length < 0.01f && (size - expectedSize).Length < 0.01f;
            });
        }
    }
}
