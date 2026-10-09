// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.Leaderboards;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Screens.Ranking
{
    /// <summary>
    /// Bundled-font rank lettering and accent strokes for the score overview.
    /// </summary>
    public partial class V2RankEmblem : CompositeDrawable
    {
        private readonly OsuSpriteText rankText;

        public V2RankEmblem(ScoreRank rank)
        {
            Size = new Vector2(284, 271);
            bool isS = rank >= ScoreRank.S;
            Colour4 accent = isS ? Colour4.FromHex("#C40087") : OsuColour.ForRank(rank);

            AddInternal(streak(new Vector2(73, 44), 47, 9, accent));
            AddInternal(streak(new Vector2(90, 88), 77, 21, accent));
            AddInternal(rankText = new OsuSpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = DrawableRank.GetRankLetter(rank),
                Font = OsuFont.Default.With(size: 180, weight: FontWeight.Bold),
                Colour = ColourInfo.GradientVertical(Colour4.White, isS ? Colour4.FromHex("#F6AFE2") : OsuColour.ForRank(rank)),
                Shadow = false,
                UseFullGlyphHeight = false,
            });
            AddInternal(streak(new Vector2(187, 209), 70, 14, Colour4.White));
            AddInternal(streak(new Vector2(142, 227), 40, 8, Colour4.White));
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();
            rankText.Scale = new Vector2(Math.Min(1, 180 / Math.Max(1, rankText.DrawWidth)));
        }

        private static Drawable streak(Vector2 position, float length, float width, Colour4 colour) => new Container
        {
            Position = position,
            Origin = Anchor.Centre,
            Size = new Vector2(length, width),
            Rotation = 45,
            CornerRadius = width / 2,
            Masking = true,
            Child = new Box { RelativeSizeAxes = Axes.Both, Colour = colour },
        };
    }
}
