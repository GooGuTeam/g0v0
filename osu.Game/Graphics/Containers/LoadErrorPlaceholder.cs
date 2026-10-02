#nullable disable

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.TypeExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Graphics.Containers
{
    /// <summary>
    /// A drawable which is displayed in place of a component which threw an exception while loading.
    /// </summary>
    public partial class LoadErrorPlaceholder : CompositeDrawable
    {
        /// <summary>
        /// The component which failed to load, and was replaced by this placeholder.
        /// </summary>
        public Drawable FailedDrawable { get; }

        /// <summary>
        /// The exception which was thrown while loading <see cref="FailedDrawable"/>.
        /// </summary>
        public Exception Failure { get; }

        private readonly FillFlowContainer textFlow;

        public LoadErrorPlaceholder(Drawable failedDrawable, Exception failure)
        {
            ArgumentNullException.ThrowIfNull(failedDrawable);
            ArgumentNullException.ThrowIfNull(failure);

            FailedDrawable = failedDrawable;
            Failure = failure;

            // auto-sized by default, for the cases where there is no failed component to inherit a slot from (e.g. an asynchronous load failure,
            // which is reported rather than replaced in-place). where there is one, the framework aligns this drawable with that slot.
            AutoSizeAxes = Axes.Both;
            Masking = true;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new OsuColour().DangerousButtonColour,
                },
                // centred within this placeholder rather than anchoring this placeholder itself, as the parent may well be a flowing layout
                // (which requires all of its children to agree on an anchor).
                //
                // Whether the lines can be marquees depends on the sizing the framework settles on after this constructor returns.
                textFlow = new FillFlowContainer
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding(10),
                    Spacing = new Vector2(0, 2),
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // a marquee only scrolls long text within the width it is given, and it takes that width from its parent.
            // asking for a marquee inside an auto-sized block therefore collapses it to zero width, and the text
            // disappears entirely without an error.
            //
            // this placeholder is auto-sized whenever there is no failed component to take a slot from: an asynchronous load failure, or a
            // failed component which was itself auto-sizing. there is then no bounded width to scroll within, so the lines are drawn at their
            // natural size instead. the marquee would have had nothing to scroll into anyway.
            bool hasBoundedWidth = (AutoSizeAxes & Axes.X) == 0 && ((RelativeSizeAxes & Axes.X) != 0 || Size.X > 0);

            if (hasBoundedWidth)
            {
                // fill the placeholder so that each line is handed a width to scroll within.
                // the axes have to be set in this order, as auto-sizing and relative sizing are mutually exclusive per axis.
                textFlow.AutoSizeAxes = Axes.Y;
                textFlow.RelativeSizeAxes = Axes.X;
            }
            else
            {
                textFlow.AutoSizeAxes = Axes.Both;
            }

            textFlow.AddRange(new[]
            {
                createLine("Oops... This component failed to load.", 15, FontWeight.SemiBold, hasBoundedWidth),
                createLine(FailedDrawable.GetType().ReadableName(), 15, FontWeight.SemiBold, hasBoundedWidth),
                createLine($"{Failure.GetType().Name}: {summarise(Failure.Message)}", 12, FontWeight.Regular, hasBoundedWidth),
            });
        }

        private static Drawable createLine(string text, float size, FontWeight weight, bool scrollable)
        {
            var font = OsuFont.GetFont(size: size, weight: weight);

            if (!scrollable)
                return new OsuSpriteText { Text = text, Font = font };

            // note the lack of an explicit anchor/origin: a marquee fills the width it is given rather than being centred within it, and a
            // vertical fill flow positions its children by their origin (so varying origins between lines would also throw).
            return new MarqueeContainer
            {
                NonOverflowingContentAnchor = Anchor.CentreLeft,
                CreateContent = () => new OsuSpriteText
                {
                    Text = text,
                    Font = font,
                },
            };
        }

        private const int max_message_length = 180;

        private static string summarise(string message)
        {
            // messages can be arbitrarily long, which would make for a very unwieldy placeholder. newlines have to go regardless, as a
            // marquee scrolls a single line.
            string singleLine = message.Replace('\r', ' ').Replace('\n', ' ').Trim();

            return singleLine.Length > max_message_length ? $"{singleLine[..max_message_length]}..." : singleLine;
        }
    }
}
