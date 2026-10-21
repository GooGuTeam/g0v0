// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Graphics;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Screens.Ranking.Expanded.Statistics
{
    public partial class HitResultStatistic : CounterStatistic
    {
        public readonly HitResult Result;

        public HitResultStatistic(HitResultDisplayStatistic result)
            : base(result.DisplayName, result.Count, result.MaxCount)
        {
            Result = result.Result;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            HeaderText.Colour = UseV2Style ? v2ResultColour(Result, colours) : colours.ForHitResult(Result);
        }

        /// <summary>
        /// The overview uses its own judgement palette; optional statistics stay neutral so they read as secondary.
        /// </summary>
        private static Colour4 v2ResultColour(HitResult result, OsuColour colours) => result switch
        {
            HitResult.Miss => Colour4.FromHex("#ED1B53"),
            HitResult.Meh => Colour4.FromHex("#FFB800"),
            HitResult.Ok => Colour4.FromHex("#69EE00"),
            HitResult.Good => colours.ForHitResult(HitResult.Good),
            HitResult.Great => Colour4.FromHex("#0CF7EA"),
            HitResult.Perfect => colours.ForHitResult(HitResult.Perfect),
            _ => Colour4.White,
        };
    }
}
