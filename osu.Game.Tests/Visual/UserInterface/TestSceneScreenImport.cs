// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
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
            legacyImportManager.Setup(m => m.GetImportCount(It.IsAny<ImportSource>(), It.IsAny<ImportContent>(), It.IsAny<CancellationToken>()))
                               .Returns(() => Task.FromResult(5));

            legacyImportManager.Setup(m => m.GetCurrentStoragePath(It.IsAny<ImportSource>()))
                               .Returns((ImportSource source) => source == ImportSource.Stable ? @"C:\not-a-real-installation" : null);

            Dependencies.CacheAs(legacyImportManager.Object);
        }

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

            AddAssert("import enabled", () => this.ChildrenOfType<ProgressRoundedButton>().Single().Enabled.Value);

            AddStep("switch to lazer source", () => this.ChildrenOfType<OverlaySortTabControl<ImportSource>>().Single().TabControl.Current.Value = ImportSource.Lazer);

            AddUntilStep("counts cleared", () => this.ChildrenOfType<SettingsCheckbox>().All(c => !c.LabelText.ToString().Contains('5')));

            AddAssert("import disabled", () => !this.ChildrenOfType<ProgressRoundedButton>().Single().Enabled.Value);

            AddStep("select stable source", () => this.ChildrenOfType<OverlaySortTabControl<ImportSource>>().Single().TabControl.Current.Value = ImportSource.Stable);

            AddUntilStep("counts restored", () => this.ChildrenOfType<SettingsCheckbox>().All(c => c.LabelText.ToString().Contains('5')));

            AddAssert("import enabled again", () => this.ChildrenOfType<ProgressRoundedButton>().Single().Enabled.Value);
        }
    }
}
