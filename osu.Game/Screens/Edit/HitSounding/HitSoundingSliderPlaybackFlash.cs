// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// A single outer contour for a slider, drawn above its independently editable sound points.
    /// </summary>
    public partial class HitSoundingSliderPlaybackFlash : CompositeDrawable
    {
        private readonly Container outline;
        private readonly Container headEdge;
        private readonly Container tailEdge;
        private bool active;
        private double endTime;
        private double startTime;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        public HitSoundingSliderPlaybackFlash()
        {
            Origin = Anchor.Centre;
            Height = HitSoundingLayout.SAMPLE_HEIGHT;
            InternalChildren = new Drawable[]
            {
                outline = new Container
                {
                    Name = @"Slider outer contour",
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        createContour(1.5f, 0.55f, 12),
                        headEdge = createEndEdge(Anchor.CentreLeft),
                        tailEdge = createEndEdge(Anchor.CentreRight)
                    }
                }
            };
        }

        private static Container createContour(float thickness, float glowAlpha, float radius) => new Container
        {
            RelativeSizeAxes = Axes.Both,
            Masking = true,
            CornerRadius = HitSoundingLayout.SAMPLE_CORNER_RADIUS,
            BorderThickness = thickness,
            BorderColour = Color4.White,
            EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Glow, Colour = new Color4(1, 1, 1, glowAlpha), Radius = radius, Hollow = true }
        };

        private static Container createEndEdge(Anchor anchor) => new Container
        {
            Anchor = anchor,
            Origin = anchor,
            RelativeSizeAxes = Axes.Y,
            Width = HitSoundingLayout.SAMPLE_CORNER_RADIUS + 5,
            Masking = true,
            Alpha = 0,
            Child = createContour(3, 0.9f, 4)
        };

        internal void Trigger(double nodeTime, double startTime, double sliderEndTime)
        {
            this.startTime = startTime;
            endTime = sliderEndTime;

            if (!active)
            {
                active = true;
                outline.ClearTransforms();
                outline.Alpha = 1;
            }

            if (nodeTime <= startTime)
                headEdge.FadeOutFromOne(750, Easing.OutQuint);
            if (nodeTime >= sliderEndTime)
                tailEdge.FadeOutFromOne(750, Easing.OutQuint);
        }

        protected override void Update()
        {
            base.Update();
            // Clip each endpoint from the same full contour, never add an internal vertical edge.
            updateEndContour(headEdge);
            updateEndContour(tailEdge);

            if (!active)
                return;

            if (editorClock.IsSeeking || editorClock.CurrentTime < startTime)
            {
                active = false;
                outline.ClearTransforms();
                outline.Alpha = 0;
            }
            else if (editorClock.CurrentTime >= endTime)
            {
                active = false;
                outline.FadeOut(750, Easing.OutQuint);
            }
        }

        private void updateEndContour(Container edge)
        {
            var contour = (Container)edge.Child;
            contour.RelativeSizeAxes = Axes.Y;
            contour.Width = DrawWidth;
            contour.Anchor = edge.Anchor;
            contour.Origin = edge.Origin;
        }
    }
}
