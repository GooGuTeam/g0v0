// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Timing;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Tests.Visual.Gameplay
{
    [Description("osu!stable's skip button and break display")]
    public partial class TestSceneLegacySkipAndBreak : PlayerTestScene
    {
        private const double first_object = 10000;
        private const double break_start = 13000;
        private const double break_end = 23000;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        protected override bool HasCustomSteps => true;

        protected override bool Autoplay => true;

        protected override Ruleset CreatePlayerRuleset() => new OsuRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
        {
            var beatmap = new Beatmap { BeatmapInfo = { Ruleset = ruleset } };

            // Provide judged hits before the break.
            for (int i = 0; i < 10; i++)
                beatmap.HitObjects.Add(new HitCircle { StartTime = first_object + i * 300, Position = new Vector2(100 + i * 30, 192) });

            for (int i = 0; i < 10; i++)
                beatmap.HitObjects.Add(new HitCircle { StartTime = break_end + 500 + i * 300, Position = new Vector2(100 + i * 30, 192) });

            beatmap.Breaks.Add(new BreakPeriod(break_start, break_end));

            return beatmap;
        }

        [SetUpSteps]
        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("enable legacy gameplay screens", () => LocalConfig.SetValue(OsuSetting.ToriiLegacyGameplayScreens, true));
            AddStep("set skin", () =>
            {
                string? directory = Environment.GetEnvironmentVariable(@"G0V0_PREVIEW_SKIN");

                skins.CurrentSkinInfo.Value = string.IsNullOrEmpty(directory)
                    ? skins.DefaultClassicSkin.SkinInfo
                    : skins.Import(new ImportTask(directory)).GetResultSafely()!;
            });
        }

        [Test]
        public void TestSkip()
        {
            CreateTest();

            AddUntilStep("legacy skip shown", () => skipOverlay()?.IsButtonVisible == true);
            AddStep("hover skip", () => InputManager.MoveMouseTo(skipOverlay()!.ScreenSpaceDrawQuad.Centre));
            AddStep("hold the intro", () => Player.GameplayClockContainer.Stop());
            AddWaitStep("wait for hover", 5);
            AddStep("resume", () => Player.GameplayClockContainer.Start());
            AddStep("click skip", () => InputManager.Click(osuTK.Input.MouseButton.Left));
            AddAssert("skipped", () => skipOverlay()!.SkipCount, () => Is.EqualTo(1));
            AddUntilStep("skip gone", () => skipOverlay()?.IsButtonVisible != true);
        }

        [Test]
        public void TestModDisplay()
        {
            AddStep("load player with mods", () => LoadPlayer(new Mod[] { new OsuModHardRock(), new OsuModHidden(), new OsuModNightcore() }));
            AddUntilStep("player loaded", () => Player.IsLoaded && Player.Alpha == 1);

            // each icon starts fading out as soon as it's in, so none stays at full opacity.
            AddUntilStep("last icon in", () => Player.ChildrenOfType<LegacyModDisplay>().SingleOrDefault()?.ChildrenOfType<Sprite>().Any(s => s.Depth == 4 && s.Alpha > 0.8f) == true);
            AddStep("hold the frame", () => Player.GameplayClockContainer.Stop());
            AddAssert("one icon for nightcore", () => Player.ChildrenOfType<LegacyModDisplay>().Single().ChildrenOfType<Sprite>().Count(), () => Is.EqualTo(5));
            AddAssert("standard mod display hidden", () => Player.HUDOverlay.ModDisplay.Parent!.Alpha, () => Is.Zero);
        }

        [Test]
        public void TestBreakRating()
        {
            CreateTest();
            skipIntro();

            AddUntilStep("section rated", () => breakSprites().Any(s => s.Anchor == Anchor.Centre && s.Alpha == 1));
            AddAssert("passing", () => Player.HealthProcessor.Health.Value, () => Is.GreaterThanOrEqualTo(0.5));
            AddAssert("break overlay does not duplicate HUD rank", () => breakSprites().Length, () => Is.EqualTo(6));
            AddStep("hold the frame", () => Player.GameplayClockContainer.Stop());
        }

        [Test]
        public void TestBreakArrows()
        {
            CreateTest();
            skipIntro();

            // the arrows first blink on for 100ms at 22000, 1000ms before the break's end.
            AddStep("hold the first blink", () =>
            {
                Player.GameplayClockContainer.Stop();
                Player.GameplayClockContainer.Seek(break_end - 950);
            });
            AddUntilStep("arrows shown", () => breakSprites().Any(s => s.Anchor == Anchor.BottomRight && s.Parent!.Alpha == 1));
        }

        private void skipIntro()
        {
            AddUntilStep("legacy skip shown", () => skipOverlay()?.IsButtonVisible == true);
            AddStep("skip", () => InputManager.Key(osuTK.Input.Key.Space));
            AddUntilStep("break started", () => Player.GameplayClockContainer.CurrentTime, () => Is.GreaterThan(break_start));
        }

        private Sprite[] breakSprites() => Player.ChildrenOfType<LegacyBreakOverlay>().Single().ChildrenOfType<Sprite>().ToArray();

        private LegacySkipOverlay? skipOverlay() => Player.ChildrenOfType<LegacySkipOverlay>().FirstOrDefault(s => s.IsPresent);
    }
}
