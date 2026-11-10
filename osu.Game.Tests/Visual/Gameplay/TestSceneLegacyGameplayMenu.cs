// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Gameplay
{
    [Description("osu!stable's pause and fail screens")]
    public partial class TestSceneLegacyGameplayMenu : OsuManualInputManagerTestScene
    {
        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private GlobalActionContainer globalActionContainer = null!;
        private GameplayMenuOverlay overlay = null!;

        private string? lastAction;

        [BackgroundDependencyLoader]
        private void load(OsuGameBase game)
        {
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourInfo.GradientHorizontal(new Color4(80, 160, 255, 255), new Color4(255, 120, 180, 255)),
                },
                globalActionContainer = new GlobalActionContainer(game),
            };
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("enable legacy pause screen", () => config.SetValue(OsuSetting.ToriiLegacyGameplayScreens, true));
            AddStep("move mouse away", () => InputManager.MoveMouseTo(Vector2.Zero));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestDefaultSkin(bool fail)
        {
            AddStep("default skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            showOverlay(fail);
        }

        [Test]
        public void TestKeyboardSelection()
        {
            showOverlay(false);

            AddStep("press enter without a selection", () => InputManager.Key(Key.Enter));
            AddAssert("resumed", () => lastAction, () => Is.EqualTo(@"resume"));

            showOverlay(false);
            AddStep("press down twice", () =>
            {
                InputManager.Key(Key.Down);
                InputManager.Key(Key.Down);
            });
            AddStep("press enter", () => InputManager.Key(Key.Enter));
            AddAssert("retried", () => lastAction, () => Is.EqualTo(@"retry"));

            showOverlay(true);
            AddStep("press up", () => InputManager.Key(Key.Up));
            AddStep("press down", () => InputManager.Key(Key.Down));
            AddStep("press enter", () => InputManager.Key(Key.Enter));
            AddAssert("retried from the fail screen", () => lastAction, () => Is.EqualTo(@"retry"));
        }

        [Test]
        public void TestNoInputWhileFadingOut()
        {
            AddStep("default skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            showOverlay(false);

            AddStep("resume", () => InputManager.Key(Key.Enter));
            AddAssert("resumed", () => lastAction, () => Is.EqualTo(@"resume"));

            AddStep("click retry while fading out", () =>
            {
                InputManager.MoveMouseTo(overlay.ScreenSpaceDrawQuad.Centre);
                InputManager.Click(MouseButton.Left);
            });
            AddStep("press enter while fading out", () => InputManager.Key(Key.Enter));
            AddAssert("nothing else happened", () => lastAction, () => Is.EqualTo(@"resume"));
        }

        [Test]
        public void TestReplaySaveFailure()
        {
            showOverlay(true);

            AddStep("press F1", () => InputManager.Key(Key.F1));
            AddWaitStep("wait for the save", 5);
            AddStep("press F1 again", () => InputManager.Key(Key.F1));
            AddWaitStep("wait for the save", 5);
            AddAssert("still on the fail screen", () => overlay.State.Value, () => Is.EqualTo(Visibility.Visible));
        }

        /// <summary>
        /// Shows the pause or fail screen with the legacy skin folder named by <c>G0V0_PREVIEW_SKIN</c>, after pressing down <paramref name="downs"/> times.
        /// </summary>
        [TestCase(false, 0)]
        [TestCase(false, 2)]
        [TestCase(true, 0)]
        [TestCase(true, 1)]
        public void TestWithSkin(bool fail, int downs)
        {
            string? directory = Environment.GetEnvironmentVariable(@"G0V0_PREVIEW_SKIN");

            if (string.IsNullOrEmpty(directory))
                Assert.Ignore(@"Set G0V0_PREVIEW_SKIN to a legacy skin folder to run this test.");

            AddStep("switch skin", () => skins.CurrentSkinInfo.Value = skins.Import(new ImportTask(directory)).GetResultSafely()!);
            showOverlay(fail);

            for (int i = 0; i < downs; i++)
                AddStep("press down", () => InputManager.Key(Key.Down));

            AddWaitStep("wait for animations", 5);
        }

        private void showOverlay(bool fail)
        {
            AddStep(fail ? "show fail screen" : "show pause screen", () =>
            {
                lastAction = null;

                globalActionContainer.Child = overlay = fail
                    ? new FailOverlay
                    {
                        OnRetry = () => lastAction = @"retry",
                        OnQuit = () => lastAction = @"quit",
                        SaveReplay = () => throw new InvalidOperationException(),
                    }
                    : new PauseOverlay
                    {
                        OnResume = () => lastAction = @"resume",
                        OnRetry = () => lastAction = @"retry",
                        OnQuit = () => lastAction = @"quit",
                    };

                overlay.Show();
            });
        }
    }
}
