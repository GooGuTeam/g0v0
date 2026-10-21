// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;

namespace osu.Game.Screens.Ranking
{
    /// <summary>
    /// Lightweight perfect-score decoration, drawn as geometry rather than an enlarged bitmap.
    /// </summary>
    public partial class V2ScoreFlair : CompositeDrawable
    {
        public V2ScoreFlair()
        {
            Size = new Vector2(58, 68);
            AddInternal(new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(38),
                Icon = FontAwesome.Regular.Star,
                Rotation = 12,
                Colour = ColourInfo.GradientHorizontal(Colour4.FromHex("#35FFD8"), Colour4.FromHex("#F7A4E2")),
            });

            for (int i = 0; i < 8; i++)
            {
                float angle = i * MathF.PI / 4;
                AddInternal(new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Position = new Vector2(MathF.Sin(angle) * 30, MathF.Cos(angle) * 30),
                    Size = new Vector2(4, i % 2 == 0 ? 10 : 5),
                    Rotation = -i * 45,
                    CornerRadius = 2,
                    Masking = true,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = i < 4 ? Colour4.FromHex("#E3B5EA") : Colour4.FromHex("#54FFE1"),
                    },
                });
            }
        }
    }
}
