using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;
using OsuLoadErrorPlaceholder = osu.Game.Graphics.Containers.LoadErrorPlaceholder;

namespace osu.Game.Tests.Visual.UserInterface
{
    public partial class TestSceneLoadErrorPlaceholder : OsuTestScene
    {
        // nothing here reads the database, so run against an isolated one rather than the user's development storage (which a schema change on
        // another branch can leave unreadable, taking every OsuTestScene fixture down with it).
        protected override bool UseFreshStoragePerRun => true;

        private static readonly Vector2 failed_size = new Vector2(300, 100);
        private static readonly Colour4 osu_placeholder_background = new OsuColour().DangerousButtonColour;

        // note that [SetUp]/[TearDown] would be the wrong hooks here: a TestScene only queues its steps during the test method, and runs them
        // later (from the runner, after [TearDown] has already fired). set-up/tear-down work therefore has to be expressed as steps.
        [SetUpSteps]
        public void SetUpSteps() => AddStep("enable load error handling", () => LoadErrorHandling.Enabled = true);

        [TearDownSteps]
        public void TearDownSteps() => AddStep("disable load error handling", () => LoadErrorHandling.Enabled = false);

        [Test]
        public void TestFailedComponentIsReplacedWithPlaceholder()
        {
            FailingRow row = null!;

            AddStep("create a row with a broken component in the middle", () => Child = row = new FailingRow(failed_size));

            // the wiring itself is worth asserting: without it the framework would fall back to its own default placeholder.
            AddAssert("a placeholder factory presents", () => LoadErrorHandling.PlaceholderFactory != null);

            // note the >= : by the time an assertion runs, the scene has already updated for a few frames, so the neighbours have progressed
            // past LoadState.Ready into LoadState.Loaded.
            AddAssert("neighbours are unaffected", () =>
                row.Before.LoadState >= LoadState.Ready && row.After.LoadState >= LoadState.Ready);

            AddAssert("broken component is discarded", () => row.Broken.Parent == null && !row.Children.Contains(row.Broken));

            AddAssert("placeholder takes the broken component's slot", () => ReferenceEquals(placeholderIn(row), row.Children[1]));

            AddAssert("placeholder is drawn between its neighbours", () =>
            {
                OsuLoadErrorPlaceholder placeholder = placeholderIn(row);

                // screen-space bounds are what actually gets drawn, so this is the closest a headless run gets to "it looks right".
                return placeholder.ScreenSpaceDrawQuad.AABBFloat.Left >= row.Before.ScreenSpaceDrawQuad.AABBFloat.Right
                       && placeholder.ScreenSpaceDrawQuad.AABBFloat.Right <= row.After.ScreenSpaceDrawQuad.AABBFloat.Left;
            });

            AddAssert("placeholder reports the failure", () =>
            {
                OsuLoadErrorPlaceholder placeholder = placeholderIn(row);

                return ReferenceEquals(placeholder.FailedDrawable, row.Broken) && placeholder.Failure is InvalidOperationException;
            });

            AddAssert("placeholder is drawn at the failed component's size", () =>
            {
                OsuLoadErrorPlaceholder placeholder = placeholderIn(row);

                return Precision.AlmostEquals(placeholder.DrawSize, failed_size) && placeholder.ScreenSpaceDrawQuad.Width > 0;
            });

            AddAssert("placeholder is styled by game", () => backgroundOf(placeholderIn(row)) == osu_placeholder_background);

            AddAssert("placeholder explains what went wrong", () =>
            {
                string[] lines = placeholderIn(row).ChildrenOfType<OsuSpriteText>().Select(text => text.Text.ToString()).ToArray();

                return lines.Any(line => line.Contains("failed to load")) && lines.Any(line => line.Contains("InvalidOperationException"));
            });

            AddAssert("text block fills the width and is centred vertically", () =>
            {
                OsuLoadErrorPlaceholder placeholder = placeholderIn(row);
                CompositeDrawable block = textBlockIn(placeholder);

                // Our implementation centres its content inside the placeholder (rather than anchoring the placeholder itself, which would break a flowing
                // parent. It also has to span the placeholder rather than hug the text, so that the lines have a width to scroll within.
                return Precision.AlmostEquals(block.DrawWidth, placeholder.DrawWidth)
                       && Precision.AlmostEquals(block.ScreenSpaceDrawQuad.AABBFloat.Centre.Y, placeholder.ScreenSpaceDrawQuad.AABBFloat.Centre.Y, 1);
            });
        }

        [Test]
        public void TestDelayedLoadFailureIsReplacedWithPlaceholder()
        {
            DelayedLoadWrapper wrapper = null!;

            AddStep("create a delayed load wrapper around a broken component", () => Child = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Child = wrapper = new DelayedLoadWrapper(new BrokenComponent(), 0)
                {
                    Size = failed_size,
                },
            });

            // the asynchronous path can't replace the component in-place, so it hands the failure to the wrapper - which must present the same
            // placeholder as the synchronous path (i.e. go through the same factory hook).
            AddUntilStep("placeholder shown", () => wrapper.ChildrenOfType<OsuLoadErrorPlaceholder>().Any());

            AddAssert("placeholder is styled by game", () => backgroundOf(placeholderIn(wrapper)) == osu_placeholder_background);

            AddAssert("placeholder is drawn", () => placeholderIn(wrapper).ScreenSpaceDrawQuad.Width > 0);

            // an auto-sized placeholder is never handed a bounded width, and a marquee inside one would collapse to zero width and take the
            // text with it - so the lines fall back to being drawn at their natural size.
            AddAssert("no line is a marquee", () => !placeholderIn(wrapper).ChildrenOfType<MarqueeContainer>().Any());

            AddAssert("lines are still drawn", () =>
            {
                OsuLoadErrorPlaceholder placeholder = placeholderIn(wrapper);
                CompositeDrawable block = textBlockIn(placeholder);

                return block.DrawWidth > 0 && block.DrawHeight > 0;
            });
        }

        [Test]
        public void TestLinesFillThePlaceholderWidth()
        {
            FailingRow row = null!;

            AddStep("create a row with a broken component in the middle", () => Child = row = new FailingRow(failed_size));

            AddAssert("each line is a marquee", () => placeholderIn(row).ChildrenOfType<MarqueeContainer>().Count() == 3);

            // the marquee sizes itself relative to its parent, so the width it scrolls within is whatever the block hands it - and a block
            // which hugged its content would hand it nothing at all.
            AddAssert("each line is given the block's content width", () =>
            {
                OsuLoadErrorPlaceholder placeholder = placeholderIn(row);
                CompositeDrawable block = textBlockIn(placeholder);
                float expected = block.DrawWidth - block.Padding.TotalHorizontal;

                return expected > 0
                       && placeholder.ChildrenOfType<MarqueeContainer>().All(line => Precision.AlmostEquals(line.DrawWidth, expected));
            });
        }

        [Test]
        public void TestLongFailureMessageIsSummarised()
        {
            FailingRow row = null!;

            AddStep("create a row with a verbose failure", () => Child = row = new FailingRow(failed_size, new string('x', 400)));

            AddAssert("message is summarised to a single line", () =>
            {
                // distinct, because a marquee adds an invisible copy of its content to measure the loop distance with.
                string[] messages = placeholderIn(row).ChildrenOfType<OsuSpriteText>().Select(text => text.Text.ToString()).Distinct().ToArray();

                return messages.Any(message => message.StartsWith("InvalidOperationException:", StringComparison.Ordinal)
                                               && message.Length <= "InvalidOperationException: ".Length + 180 + 3
                                               && !message.Contains('\n')
                                               && message.EndsWith("...", StringComparison.Ordinal));
            });
        }

        private static OsuLoadErrorPlaceholder placeholderIn(Drawable target) => target.ChildrenOfType<OsuLoadErrorPlaceholder>().Single();

        private static Colour4 backgroundOf(OsuLoadErrorPlaceholder placeholder) => placeholder.ChildrenOfType<Box>().Single().Colour;

        /// <summary>
        /// The block holding the placeholder's lines. Scoped to direct children, as a <see cref="MarqueeContainer"/> brings a fill flow
        /// container of its own along with it.
        /// </summary>
        private static CompositeDrawable textBlockIn(OsuLoadErrorPlaceholder placeholder) => placeholder.ChildrenOfType<FillFlowContainer>().Single(flow => flow.Parent == placeholder);

        /// <summary>
        /// A row of three components, the middle of which throws while loading.
        /// </summary>
        private partial class FailingRow : FillFlowContainer
        {
            public readonly Box Before;
            public readonly BrokenComponent Broken;
            public readonly Box After;

            public FailingRow(Vector2 size, string failureMessage = "This component fails to load on purpose.")
            {
                Anchor = Anchor.Centre;
                Origin = Anchor.Centre;
                Direction = FillDirection.Horizontal;
                Spacing = new Vector2(10);
                AutoSizeAxes = Axes.Both;

                InternalChildren = new Drawable[]
                {
                    Before = new Box { Size = size, Colour = Color4.SeaGreen },
                    Broken = new BrokenComponent(failureMessage) { Size = size },
                    After = new Box { Size = size, Colour = Color4.SeaGreen },
                };
            }
        }

        /// <summary>
        /// A component which always throws from its dependency loader.
        /// </summary>
        public partial class BrokenComponent : Box
        {
            private readonly string failureMessage;

            public BrokenComponent(string failureMessage = "This component fails to load on purpose.")
            {
                this.failureMessage = failureMessage;
            }

            [BackgroundDependencyLoader]
            private void load() => throw new InvalidOperationException(failureMessage);
        }
    }
}
