// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.Tests.Visual.Navigation
{
    public partial class TestSceneLegacyFpsCounterNavigation : OsuGameTestScene
    {
        private FPSCounter modernCounter => Game.ChildrenOfType<FPSCounter>().Single();

        private LegacyFpsDisplay legacyCounter => Game.ChildrenOfType<LegacyFpsDisplay>().Single();

        [Test]
        public void TestLegacyCounterReplacesModern()
        {
            AddStep("show fps", () => Game.LocalConfig.SetValue(OsuSetting.ShowFpsDisplay, true));
            AddStep("modern counter", () => Game.LocalConfig.SetValue(OsuSetting.ToriiLegacyFpsCounter, false));
            AddUntilStep("modern shown", () => modernCounter.Parent!.Alpha == 1 && legacyCounter.Alpha == 0);

            // the toggle must keep working after garbage collection (config bindings are weak).
            AddStep("collect garbage", () =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            });

            AddStep("legacy counter", () => Game.LocalConfig.SetValue(OsuSetting.ToriiLegacyFpsCounter, true));
            AddUntilStep("only legacy shown", () => modernCounter.Parent!.Alpha == 0 && legacyCounter.Alpha == 1);

            AddStep("modern counter again", () => Game.LocalConfig.SetValue(OsuSetting.ToriiLegacyFpsCounter, false));
            AddUntilStep("only modern shown", () => modernCounter.Parent!.Alpha == 1 && legacyCounter.Alpha == 0);
        }
    }
}
