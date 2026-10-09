// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.Leaderboards;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Screens.Ranking.Expanded.Accuracy
{
    public partial class RankBadge : CompositeDrawable
    {
        public readonly double Accuracy;
        public readonly ScoreRank Rank;
        private readonly double displayPosition;

        public bool UseV2Style { get; init; }

        private Drawable rankContainer = null!;
        private Drawable overlay = null!;
        private OsuSpriteText? modernLetter;

        public RankBadge(double accuracy, double position, ScoreRank rank)
        {
            Accuracy = accuracy;
            displayPosition = position;
            Rank = rank;
            RelativeSizeAxes = Axes.Both;
            Alpha = 0;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var container = new Container
            {
                Origin = Anchor.Centre,
                Size = UseV2Style ? new Vector2(36, 19) : new Vector2(28, 14),
            };
            InternalChild = rankContainer = container;

            if (UseV2Style)
            {
                container.Add(new CircularContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = OsuColour.ForRank(Rank) },
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Size = new Vector2(0.32f, 2),
                            Rotation = -25,
                            Colour = Colour4.White.Opacity(0.18f),
                        },
                    },
                });
                container.Add(modernLetter = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = DrawableRank.GetRankLetter(Rank),
                    Font = OsuFont.Default.With(size: Rank >= ScoreRank.X ? 16 : 20, weight: FontWeight.Bold),
                    Colour = Rank >= ScoreRank.S ? Colour4.FromHex("#FFE43B") : Colour4.Black.Opacity(0.55f),
                    Shadow = false,
                });
            }
            else
                container.Add(new DrawableRank(Rank));

            container.Add(overlay = new CircularContainer
            {
                RelativeSizeAxes = Axes.Both,
                Blending = BlendingParameters.Additive,
                Masking = true,
                Alpha = 0,
                EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Glow,
                    Colour = OsuColour.ForRank(Rank).Opacity(0.2f),
                    Radius = 10,
                },
                Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0, AlwaysPresent = true },
            });
        }

        public void Appear()
        {
            this.FadeIn(50);
            overlay.FadeIn().FadeOut(500, Easing.In);
        }

        protected override void Update()
        {
            base.Update();

            double position = UseV2Style ? V2AccuracyScale.BadgeAngle(Rank) / 360 : displayPosition;
            float angle = -MathF.PI / 2 - (1 - (float)position) * MathF.PI * 2;
            rankContainer.Position = DrawSize / 2 + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * DrawSize / 2;

            if (modernLetter != null)
            {
                float rotation = angle * 180 / MathF.PI;
                rankContainer.Rotation = rotation;
                modernLetter.Rotation = -rotation;
            }
        }
    }
}
