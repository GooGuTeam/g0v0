// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Colour;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osuTK;

namespace osu.Game.Screens.Ranking.Expanded.Statistics
{
    /// <summary>
    /// A <see cref="StatisticDisplay"/> to display general numeric values.
    /// </summary>
    public partial class CounterStatistic : StatisticDisplay
    {
        /// <summary>
        /// Suffix for overview judgement counts (for example 2137×). Does not affect other results layouts.
        /// </summary>
        public string V2Suffix { get; init; } = string.Empty;

        private readonly int count;
        private readonly int? maxCount;

        private RollingCounter<int> counter = null!;

        /// <summary>
        /// Creates a new <see cref="CounterStatistic"/>.
        /// </summary>
        /// <param name="header">The name of the statistic.</param>
        /// <param name="count">The value to display.</param>
        /// <param name="maxCount">The maximum value of <paramref name="count"/>. Not displayed if null.</param>
        public CounterStatistic(LocalisableString header, int count, int? maxCount = null)
            : base(header)
        {
            this.count = count;
            this.maxCount = maxCount;
        }

        public override void Appear()
        {
            base.Appear();
            counter.Current.Value = count;
        }

        protected override Drawable CreateContent()
        {
            var container = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Child = counter = new StatisticCounter
                {
                    UseV2Style = UseV2Style,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre
                }
            };

            if (UseV2Style && V2Suffix.Length > 0)
            {
                container.Add(new OsuSpriteText
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Font = OsuFont.Numeric.With(size: 20, weight: FontWeight.Light),
                    Text = V2Suffix,
                    Colour = ColourInfo.GradientVertical(Colour4.White, Colour4.FromHex("#AFE8FF")),
                    Shadow = false,
                });
            }

            if (maxCount != null)
            {
                container.Add(new OsuSpriteText
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Font = UseV2Style ? OsuFont.Numeric.With(size: 10) : OsuFont.MapleMono.With(size: 12, fixedWidth: true),
                    Spacing = new Vector2(UseV2Style ? 0 : -2, 0),
                    Text = $"/{maxCount}",
                    Colour = UseV2Style ? ColourInfo.GradientVertical(Colour4.White, Colour4.FromHex("#AFE8FF")) : Colour4.White,
                    Shadow = !UseV2Style,
                });
            }

            return container;
        }
    }
}
