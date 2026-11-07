// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Tests.Visual.Gameplay
{
    [Description("osu!stable's loading screen")]
    public partial class TestSceneLegacyPlayerLoader : ScreenTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private PlayerLoader loader = null!;
        private BlockingPlayer? player;

        [SetUpSteps]
        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("enable legacy gameplay screens", () => config.SetValue(OsuSetting.ToriiLegacyGameplayScreens, true));
            AddStep("handle leaderboard requests", () => ((DummyAPIAccess)API).HandleRequest = req =>
            {
                if (req is not GetScoresRequest getScores)
                    return false;

                getScores.TriggerSuccess(new APIScoresCollection { Scores = [] });
                return true;
            });
        }

        public override void TearDownSteps()
        {
            AddStep("let the player load", () => player?.AllowLoad.Set());
            base.TearDownSteps();
        }

        [Test]
        public void TestLoading()
        {
            loadPlayer();
            AddWaitStep("wait for the spinner", 20);
            AddAssert("legacy screen shown", () => loader.ChildrenOfType<LegacyPlayerLoaderOverlay>().Single().IsPresent);
        }

        [Test]
        public void TestSkinEditorConfigurationLease()
        {
            Bindable<bool> setting = null!;
            LeasedBindable<bool> lease = null!;
            loadPlayer();
            AddStep("lease beatmap skins as skin editor does", () =>
            {
                setting = config.GetBindable<bool>(OsuSetting.BeatmapSkins);
                lease = setting.BeginLease(true);
                lease.Value = !lease.Value;
            });
            AddAssert("configuration remains leased", () => setting.Disabled);
            AddStep("return lease", () => lease.Return());
            AddAssert("configuration unlocked", () => !setting.Disabled);
        }

        [Test]
        public void TestHoverSettings()
        {
            loadPlayer();
            AddStep("move away", () => InputManager.MoveMouseTo(loader.ScreenSpaceDrawQuad.Centre));
            AddStep("hover the bar", () => InputManager.MoveMouseTo(loader.ScreenSpaceDrawQuad.BottomLeft + new Vector2(200, -20)));
            AddUntilStep("bar extended", () => loader.ChildrenOfType<LegacyPlayerLoaderOverlay>().Single().Extended);
            AddWaitStep("wait for the bar", 20);
        }

        [Test]
        public void TestExtendedSettingsHoldGameplay()
        {
            loadPlayer();

            AddStep("move away", () => InputManager.MoveMouseTo(loader.ScreenSpaceDrawQuad.Centre));
            AddStep("hover the bar", () => InputManager.MoveMouseTo(loader.ScreenSpaceDrawQuad.BottomLeft + new Vector2(200, -20)));
            AddUntilStep("bar extended", () => loader.ChildrenOfType<LegacyPlayerLoaderOverlay>().Single().Extended);

            AddStep("let the player load", () => player!.AllowLoad.Set());
            AddWaitStep("wait", 30);
            AddAssert("still loading", () => loader.IsCurrentScreen());

            AddStep("move away", () => InputManager.MoveMouseTo(loader.ScreenSpaceDrawQuad.Centre));
            AddUntilStep("gameplay started", () => player?.IsCurrentScreen() == true);
        }

        private void loadPlayer()
        {
            AddStep("load beatmap", () =>
            {
                var working = CreateWorkingBeatmap(new OsuRuleset().RulesetInfo);
                working.Beatmap.AudioLeadIn = 60000;
                Beatmap.Value = working;
            });
            AddStep("load screen", () =>
            {
                InputManager.MoveMouseTo(Vector2.Zero);
                LoadScreen(loader = new PlayerLoader(() => player = new BlockingPlayer()));
            });
            AddUntilStep("wait for current", () => loader.IsCurrentScreen());
        }

        private partial class BlockingPlayer : TestPlayer
        {
            public readonly ManualResetEventSlim AllowLoad = new ManualResetEventSlim();

            public BlockingPlayer()
                : base(false, false)
            {
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                AllowLoad.Wait(30000);
            }
        }
    }
}
