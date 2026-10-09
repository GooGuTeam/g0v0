// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;

namespace osu.Game.Screens.Ranking.Expanded.Statistics
{
    /// <summary>
    /// A statistic from the score to be displayed in the <see cref="ExpandedPanelMiddleContent"/>.
    /// </summary>
    public abstract partial class StatisticDisplay : CompositeDrawable, IHasTooltip
    {
        protected SpriteText HeaderText { get; private set; } = null!;

        /// <summary>
        /// Uses an open, left-aligned presentation for the full-width results layout.
        /// </summary>
        public bool UseV2Style { get; init; }

        public float V2ValueScale { get; init; } = 1.85f;

        /// <summary>
        /// Optional compact label for the overview; the original ruleset label remains available in the tooltip.
        /// </summary>
        public LocalisableString? V2Header { get; init; }

        LocalisableString IHasTooltip.TooltipText => UseV2Style ? header : default;

        /// <summary>
        /// The label shown above the value, after any modern-style override has been applied.
        /// </summary>
        public LocalisableString DisplayedHeader => IsLoaded ? HeaderText.Text : UseV2Style ? V2Header ?? header : header;

        private readonly LocalisableString header;
        private Drawable content = null!;
        private Container valueContainer = null!;

        /// <summary>
        /// Creates a new <see cref="StatisticDisplay"/>.
        /// </summary>
        /// <param name="header">The name of the statistic.</param>
        protected StatisticDisplay(LocalisableString header)
        {
            this.header = header;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Children = new[]
                {
                    new CircularContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = UseV2Style ? 14 : 12,
                        Masking = !UseV2Style,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4Extensions.FromHex("#222"),
                                Alpha = UseV2Style ? 0 : 1,
                            },
                            HeaderText = new TruncatingSpriteText
                            {
                                Anchor = UseV2Style ? Anchor.CentreLeft : Anchor.Centre,
                                Origin = UseV2Style ? Anchor.CentreLeft : Anchor.Centre,
                                Font = UseV2Style ? OsuFont.Default.With(size: 14) : OsuFont.MapleMono.With(size: 12, weight: FontWeight.SemiBold),
                                Shadow = !UseV2Style,
                                Text = (UseV2Style ? V2Header ?? header : header).ToUpper(),
                            }
                        }
                    },
                    valueContainer = new Container
                    {
                        Anchor = UseV2Style ? Anchor.TopLeft : Anchor.TopCentre,
                        Origin = UseV2Style ? Anchor.TopLeft : Anchor.TopCentre,
                        AutoSizeAxes = Axes.Both,
                        Scale = new osuTK.Vector2(UseV2Style ? V2ValueScale : 1),
                        Children = new[]
                        {
                            content = CreateContent().With(d =>
                            {
                                d.Anchor = UseV2Style ? Anchor.TopLeft : Anchor.TopCentre;
                                d.Origin = UseV2Style ? Anchor.TopLeft : Anchor.TopCentre;
                                d.Alpha = 0;
                                d.AlwaysPresent = true;
                            }),
                        }
                    }
                }
            };
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            if (UseV2Style)
            {
                ((TruncatingSpriteText)HeaderText).MaxWidth = Math.Max(1, DrawWidth);
                valueContainer.Scale = new osuTK.Vector2(Math.Min(V2ValueScale, DrawWidth / Math.Max(1, valueContainer.DrawWidth)));
            }
        }

        /// <summary>
        /// Shows the statistic value.
        /// </summary>
        public virtual void Appear() => content.FadeIn(100);

        /// <summary>
        /// Creates the content for this <see cref="StatisticDisplay"/>.
        /// </summary>
        protected abstract Drawable CreateContent();
    }
}
