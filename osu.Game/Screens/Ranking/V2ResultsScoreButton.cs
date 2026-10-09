// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Scoring;
using osu.Game.Screens.Select;

namespace osu.Game.Screens.Ranking
{
    public partial class V2ResultsScoreButton : OsuClickableContainer, IHasPopover
    {
        private readonly Func<IEnumerable<ScoreInfo>> scores;
        private readonly Action<ScoreInfo> selectScore;

        public V2ResultsScoreButton(Func<IEnumerable<ScoreInfo>> scores, Action<ScoreInfo> selectScore)
        {
            this.scores = scores;
            this.selectScore = selectScore;
            TooltipText = "Select another score";
            Action = this.ShowPopover;
        }

        protected override bool OnHover(HoverEvent e)
        {
            this.FadeColour(Colour4.FromHex("#7CF6FF"), 120);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            this.FadeColour(Colour4.White, 120);
            base.OnHoverLost(e);
        }

        public Popover GetPopover() => new ScoresPopover(scores(), selectScore);

        private partial class ScoresPopover : OsuPopover
        {
            public ScoresPopover(IEnumerable<ScoreInfo> scores, Action<ScoreInfo> selectScore)
                : base(false)
            {
                var flow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new osuTK.Vector2(0, 4),
                    Padding = new MarginPadding(10),
                };

                int rank = 0;
                foreach (var score in scores)
                {
                    flow.Add(new BeatmapLeaderboardScore(score, sheared: false)
                    {
                        Rank = ++rank,
                        Action = () =>
                        {
                            selectScore(score);
                            this.HidePopover();
                        },
                    });
                }

                Children = new Drawable[]
                {
                    new BasicScrollContainer
                    {
                        Width = 620,
                        Height = 360,
                        Child = flow,
                    },
                };
            }

            protected override void PopIn()
            {
                base.PopIn();
                this.MoveToX(-80).Then().MoveToX(0, 300, Easing.OutQuint);
            }
        }
    }
}
