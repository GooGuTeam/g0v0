// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.Ranking.Expanded.Accuracy;
using osuTK;

namespace osu.Game.Screens.Ranking.Expanded.Statistics
{
    public partial class StatisticCounter : RollingCounter<int>
    {
        public bool UseV2Style { get; init; }

        protected override double RollingDuration => AccuracyCircle.ACCURACY_TRANSFORM_DURATION;

        protected override Easing RollingEasing => AccuracyCircle.ACCURACY_TRANSFORM_EASING;

        protected override OsuSpriteText CreateSpriteText() => base.CreateSpriteText().With(s =>
        {
            s.Font = UseV2Style ? OsuFont.Numeric.With(size: 20, weight: FontWeight.Light) : OsuFont.MapleMono.With(size: 20, fixedWidth: true);
            s.Shadow = !UseV2Style;
            s.Colour = UseV2Style ? ColourInfo.GradientVertical(Colour4.White, Colour4.FromHex("#AFE8FF")) : Colour4.White;
            s.Spacing = new Vector2(UseV2Style ? 0 : -2, 0);
        });
    }
}
