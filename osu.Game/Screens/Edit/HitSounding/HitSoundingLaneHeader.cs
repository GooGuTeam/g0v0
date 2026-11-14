// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays;

namespace osu.Game.Screens.Edit.HitSounding
{
    public partial class HitSoundingLaneHeader : CompositeDrawable
    {
        public HitSoundingLane Lane { get; }

        public HitSoundingLaneHeader(HitSoundingLane lane)
        {
            Lane = lane;
            RelativeSizeAxes = Axes.X;
            Height = HitSoundingLayout.LANE_HEIGHT;
            Y = (float)lane * HitSoundingLayout.LANE_HEIGHT;
            Padding = new MarginPadding { Bottom = HitSoundingLayout.LANE_SPACING };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours, OsuColour osuColour)
        {
            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 5,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background5, Alpha = 0.9f },
                    new Box
                    {
                        X = 10, Width = 4, Height = 28, Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft,
                        Colour = Lane switch
                        {
                            HitSoundingLane.Whistle => osuColour.Blue,
                            HitSoundingLane.Finish => osuColour.Yellow,
                            HitSoundingLane.Clap => osuColour.Pink,
                            _ => colours.Light4
                        }
                    },
                    new OsuSpriteText
                    {
                        X = 24, Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft,
                        Font = OsuFont.Default.With(size: 17, weight: FontWeight.SemiBold), Colour = colours.Light3,
                        Text = Lane switch
                        {
                            HitSoundingLane.SampleSet => EditorStrings.SampleSet,
                            HitSoundingLane.Additions => HitSoundingStrings.Additions,
                            HitSoundingLane.Whistle => HitSoundingStrings.Whistle,
                            HitSoundingLane.Finish => HitSoundingStrings.Finish,
                            _ => HitSoundingStrings.Clap
                        }
                    }
                }
            };
        }
    }
}
