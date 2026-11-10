// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osuTK.Graphics;

namespace osu.Game.Tests.Visual.UserInterface
{
    public partial class TestSceneLegacyFpsDisplay : OsuTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Test]
        public void TestDisplay()
        {
            AddStep("enable", () =>
            {
                config.SetValue(OsuSetting.ShowFpsDisplay, true);
                config.SetValue(OsuSetting.ToriiLegacyFpsCounter, true);
            });
            LegacyFpsDisplay display = null!;
            AddStep("create display", () => Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(40, 60, 80, 255),
                },
                display = new LegacyFpsDisplay(),
            });
            // its first measurement, well within the four seconds after which the frame rate box fades at its limit.
            AddUntilStep("frame rate measured", () => display.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString().StartsWith('/')));
        }
    }
}
