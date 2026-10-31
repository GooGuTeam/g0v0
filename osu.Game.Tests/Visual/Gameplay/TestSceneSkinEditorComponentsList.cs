// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Testing;
using osu.Game.Overlays;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Tests.Visual.Gameplay
{
    public partial class TestSceneSkinEditorComponentsList : SkinnableTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);

        private SkinComponentToolbox? toolbox;

        private int loggedErrors;

        // the load error boundary is a process-wide switch, and is enabled by default in debug builds (but not in test runs).
        [SetUp]
        public void SetUp() => LoadErrorHandling.Enabled = false;

        [TearDown]
        public void TearDown() => LoadErrorHandling.Enabled = false;

        [Test]
        public void TestToggleEditor()
        {
            var skinComponentsContainer = new SkinnableContainer(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.SongSelect));

            AddStep("show available components", () => SetContents(_ => new SkinComponentToolbox(skinComponentsContainer, null)
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Width = 0.6f,
            }));
        }

        /// <summary>
        /// The component list is filtered by attempting to load each component in the context of the current target, so that components which cannot
        /// resolve their dependencies are not displayed. That filtering must survive the load error boundary (see <see cref="LoadErrorHandling"/>),
        /// which would otherwise isolate such a failure instead of throwing it - leaving every such component listed, with an error placeholder in place
        /// of its preview and an error reported for what is a routine probe.
        /// </summary>
        [Test]
        public void TestComponentsListedUnchangedByLoadErrorHandling()
        {
            int listedWithoutHandling = -1;

            AddStep("cache skin editor", () => Dependencies.CacheAs(new SkinEditor()));
            AddStep("disable load error handling", () => LoadErrorHandling.Enabled = false);
            AddStep("show available components", showComponentListCapturingErrors);
            AddAssert("components are listed", () => listedWithoutHandling = listedComponents().Count(), () => Is.GreaterThan(0));
            AddAssert("no errors logged", () => loggedErrors, () => Is.Zero);
            AddAssert("no failed components listed", failedComponentsListed, () => Is.Zero);

            AddStep("enable load error handling", () => LoadErrorHandling.Enabled = true);
            AddStep("show available components", showComponentListCapturingErrors);
            AddAssert("same components are listed", () => listedComponents().Count(), () => Is.EqualTo(listedWithoutHandling));
            AddAssert("no errors logged", () => loggedErrors, () => Is.Zero);
            AddAssert("no failed components listed", failedComponentsListed, () => Is.Zero);
        }

        /// <summary>
        /// Lists the components, recording how many errors were logged while doing so.
        /// </summary>
        private void showComponentListCapturingErrors()
        {
            var errors = new List<LogEntry>();

            void capture(LogEntry entry)
            {
                if (entry.Level >= LogLevel.Error)
                    errors.Add(entry);
            }

            Logger.NewEntry += capture;

            try
            {
                showComponentList();
            }
            finally
            {
                Logger.NewEntry -= capture;
            }

            loggedErrors = errors.Count;
        }

        private void showComponentList()
        {
            SetContents(_ =>
            {
                // the toolbox borrows the dependencies of its target, which therefore has to be loaded for the list to be populated at all.
                var target = new SkinnableContainer(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.SongSelect))
                {
                    Size = new Vector2(100),
                };

                return new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        target,
                        toolbox = new SkinComponentToolbox(target, null)
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            Width = 0.6f,
                        },
                    },
                };
            });
        }

        private IEnumerable<Drawable> listedComponents() => toolbox!.ChildrenOfType<SkinComponentToolbox.ToolboxComponentButton>();

        private int failedComponentsListed() => toolbox!.ChildrenOfType<Drawable>().Count(d => d is LoadErrorPlaceholder);

        protected override Ruleset CreateRulesetForSkinProvider() => new OsuRuleset();
    }
}
