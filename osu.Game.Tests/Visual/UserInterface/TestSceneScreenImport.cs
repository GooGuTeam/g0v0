// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Overlays;
using osu.Game.Overlays.FirstRunSetup;
using osu.Game.Overlays.Settings;

namespace osu.Game.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneScreenImport : OsuManualInputManagerTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        private readonly Mock<LegacyImportManager> legacyImportManager = new Mock<LegacyImportManager>();

        [BackgroundDependencyLoader]
        private void load()
        {
            Dependencies.CacheAs(legacyImportManager.Object);
        }

        [SetUp]
        public void SetUp()
        {
            // The test browser runs every test on the same scene instance, so per-test overrides have to be dropped again.
            // Setups added later win, which makes re-applying the defaults sufficient.
            configureImportManager();
        }

        private void configureImportManager()
        {
            legacyImportManager.Setup(m => m.GetImportCount(It.IsAny<ImportSource>(), It.IsAny<ImportContent>(), It.IsAny<CancellationToken>()))
                               .Returns(() => Task.FromResult(5));

            legacyImportManager.Setup(m => m.GetCurrentStoragePath(It.IsAny<ImportSource>()))
                               .Returns((ImportSource source) => source == ImportSource.Stable ? @"C:\not-a-real-installation" : null);

            legacyImportManager.Setup(m => m.CheckSourceReadableAsync(It.IsAny<ImportSource>()))
                               .Returns(() => Task.FromResult(true));

            legacyImportManager.Setup(m => m.ImportAsync(It.IsAny<ImportSource>(), It.IsAny<ImportContent>(), It.IsAny<bool>()))
                               .Returns(() => Task.CompletedTask);
        }

        private ProgressRoundedButton importButton => this.ChildrenOfType<ProgressRoundedButton>().Single();

        private bool importIsCompleted() => importButton.ChildrenOfType<SpriteIcon>().Any(i => i.Icon.Equals(FontAwesome.Solid.Check) && i.Alpha > 0);

        private IBindable<ImportSource> selectedSource => this.ChildrenOfType<OverlaySortTabControl<ImportSource>>().Single().TabControl.Current;

        private TabControl<ImportSource> sourceTabControl => this.ChildrenOfType<OverlaySortTabControl<ImportSource>>().Single().TabControl;

        /// <summary>
        /// Switches the source by writing the bindable directly. Only valid while the selection is not locked.
        /// </summary>
        private void selectSource(ImportSource source) => sourceTabControl.Current.Value = source;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("load screen", () => Child = new PopoverContainer
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new ScreenStack(new ScreenImport())
                }
            });
        }

        [Test]
        public void TestSourceSelection()
        {
            AddAssert("both sources are offered", () => this.ChildrenOfType<OverlaySortTabControl<ImportSource>>().Single().TabControl.Items.Count == 2);
        }

        [Test]
        public void TestImportCountsAreShownForSelectedSource()
        {
            AddUntilStep("counts shown", () => this.ChildrenOfType<SettingsCheckbox>().Count() == 4
                                               && this.ChildrenOfType<SettingsCheckbox>().All(c => c.LabelText.ToString().Contains('5')));

            AddAssert("import enabled", () => importButton.Enabled.Value);

            AddStep("switch to lazer source", () => selectSource(ImportSource.Lazer));

            AddUntilStep("counts cleared", () => this.ChildrenOfType<SettingsCheckbox>().All(c => !c.LabelText.ToString().Contains('5')));

            AddAssert("import disabled", () => !importButton.Enabled.Value);

            AddStep("select stable source", () => selectSource(ImportSource.Stable));

            AddUntilStep("counts restored", () => this.ChildrenOfType<SettingsCheckbox>().All(c => c.LabelText.ToString().Contains('5')));

            AddAssert("import enabled again", () => importButton.Enabled.Value);
        }

        [Test]
        public void TestImportStateIsRememberedPerSource()
        {
            AddStep("make both sources usable", () =>
            {
                legacyImportManager.Setup(m => m.GetCurrentStoragePath(It.IsAny<ImportSource>()))
                                   .Returns((ImportSource source) => $@"C:\not-a-real-{source}-installation");
            });

            AddStep("switch to lazer source", () => selectSource(ImportSource.Lazer));
            AddUntilStep("import available", () => importButton.Enabled.Value);

            AddStep("run import", () => importButton.TriggerClick());
            AddUntilStep("import completed", importIsCompleted);

            AddStep("switch to stable source", () => selectSource(ImportSource.Stable));
            AddUntilStep("stable source is not completed", () => !importIsCompleted());
            AddAssert("stable import is still available", () => importButton.Enabled.Value);

            AddStep("switch back to lazer source", () => selectSource(ImportSource.Lazer));
            AddUntilStep("lazer source keeps its completed state", importIsCompleted);
            AddAssert("lazer import is no longer available", () => !importButton.Enabled.Value);
        }

        /// <summary>
        /// An installation whose database cannot be read has to remain unusable when the screen re-derives its state.
        /// The check is also specific to one installation, so it must not be repeated while the same one is selected.
        /// </summary>
        [Test]
        public void TestUnreadableSourceStaysUnusable()
        {
            const string unreadable_install = @"C:/not-a-real-unreadable-installation";

            var firstCheck = new TaskCompletionSource<bool>();
            int checks = 0;

            AddStep("make the lazer source available", () =>
            {
                legacyImportManager.Setup(m => m.GetCurrentStoragePath(It.Is<ImportSource>(s => s == ImportSource.Stable)))
                                   .Returns(@"C:/not-a-real-stable-installation");

                legacyImportManager.Setup(m => m.GetCurrentStoragePath(It.Is<ImportSource>(s => s == ImportSource.Lazer)))
                                   .Returns(unreadable_install);

                legacyImportManager.Setup(m => m.CheckSourceReadableAsync(It.IsAny<ImportSource>()))
                                   .Returns(() =>
                                   {
                                       checks++;

                                       // A repeated check never completes, so a refresh relying on it would leave the button
                                       // looking usable instead of falling back to the result which is already known.
                                       return checks == 1 ? firstCheck.Task : new TaskCompletionSource<bool>().Task;
                                   });

                legacyImportManager.Object.UpdateStorage(ImportSource.Lazer, unreadable_install);
            });

            AddStep("switch to lazer source", () => selectSource(ImportSource.Lazer));
            AddUntilStep("readability was checked", () => checks == 1);

            AddStep("report the database as unreadable", () => firstCheck.SetResult(false));

            AddUntilStep("import disabled", () => !importButton.Enabled.Value);

            AddStep("switch to stable source", () => selectSource(ImportSource.Stable));
            AddAssert("stable import is available", () => importButton.Enabled.Value);

            AddStep("switch back to lazer source", () => selectSource(ImportSource.Lazer));
            AddWaitStep("wait for a repeated check", 5);

            AddAssert("readability was not checked again", () => checks == 1);
            AddAssert("unreadable source stays disabled", () => !importButton.Enabled.Value);
        }
    }
}
