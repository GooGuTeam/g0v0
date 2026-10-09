// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Screens.Ranking.Expanded.Accuracy;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Screens.Ranking.Expanded.Statistics
{
    /// <summary>
    /// A <see cref="StatisticDisplay"/> to display the player's accuracy.
    /// </summary>
    public partial class AccuracyStatistic : StatisticDisplay
    {
        private readonly double accuracy;

        private RollingCounter<double> counter = null!;

        /// <summary>
        /// Creates a new <see cref="AccuracyStatistic"/>.
        /// </summary>
        /// <param name="accuracy">The accuracy to display.</param>
        public AccuracyStatistic(double accuracy)
            : base(BeatmapsetsStrings.ShowScoreboardHeadersAccuracy)
        {
            this.accuracy = accuracy;
        }

        public override void Appear()
        {
            base.Appear();
            counter.Current.Value = accuracy;
        }

        protected override Drawable CreateContent() => counter = new Counter { UseV2Style = UseV2Style };

        private partial class Counter : RollingCounter<double>
        {
            public bool UseV2Style { get; init; }

            // FormatAccuracy doesn't round, which means if we use the OutPow10 easing the number will stick 0.01% short for some time.
            // To avoid that let's use a shorter easing which looks roughly the same.
            protected override double RollingDuration => AccuracyCircle.ACCURACY_TRANSFORM_DURATION / 2;
            protected override Easing RollingEasing => Easing.OutQuad;

            protected override LocalisableString FormatCount(double count) => count.FormatAccuracy();

            protected override OsuSpriteText CreateSpriteText() => base.CreateSpriteText().With(s =>
            {
                s.Font = UseV2Style ? OsuFont.Numeric.With(size: 20, weight: FontWeight.Light) : OsuFont.MapleMono.With(size: 20, fixedWidth: true);
                s.Shadow = !UseV2Style;
                s.Colour = UseV2Style ? ColourInfo.GradientVertical(Colour4.White, Colour4.FromHex("#AFE8FF")) : Colour4.White;
                s.Spacing = new Vector2(UseV2Style ? 0 : -2, 0);
            });
        }
    }
}
