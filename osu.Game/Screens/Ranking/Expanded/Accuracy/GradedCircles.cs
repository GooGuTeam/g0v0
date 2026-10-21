// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics;
using osu.Game.Scoring;

namespace osu.Game.Screens.Ranking.Expanded.Accuracy
{
    public partial class GradedCircles : CompositeDrawable
    {
        private double progress;

        public double Progress
        {
            get => progress;
            set
            {
                progress = value;

                foreach (var circle in circles)
                    circle.RevealProgress = modernScale?.Map(value) / 360 ?? value;
            }
        }

        private readonly Container<GradedCircle> circles;
        private readonly V2AccuracyScale? modernScale;

        public GradedCircles(double accuracyC, double accuracyB, double accuracyA, double accuracyS, double accuracyX, bool useV2Style = false)
        {
            modernScale = useV2Style ? new V2AccuracyScale(accuracyC, accuracyB, accuracyA, accuracyS, accuracyX) : null;
            InternalChild = circles = new Container<GradedCircle>
            {
                RelativeSizeAxes = Axes.Both,
                Children = new[]
                {
                    new GradedCircle(0.0, accuracyC, useV2Style)
                    {
                        Colour = OsuColour.ForRank(ScoreRank.D),
                    },
                    new GradedCircle(accuracyC, accuracyB, useV2Style)
                    {
                        Colour = OsuColour.ForRank(ScoreRank.C),
                    },
                    new GradedCircle(accuracyB, accuracyA, useV2Style)
                    {
                        Colour = OsuColour.ForRank(ScoreRank.B),
                    },
                    new GradedCircle(accuracyA, accuracyS, useV2Style)
                    {
                        Colour = OsuColour.ForRank(ScoreRank.A),
                    },
                    new GradedCircle(accuracyS, accuracyX - AccuracyCircle.VIRTUAL_SS_PERCENTAGE, useV2Style)
                    {
                        Colour = OsuColour.ForRank(ScoreRank.S),
                    },
                    new GradedCircle(accuracyX - AccuracyCircle.VIRTUAL_SS_PERCENTAGE, 1.0, useV2Style)
                    {
                        Colour = OsuColour.ForRank(ScoreRank.X)
                    }
                }
            };

            if (modernScale != null)
            {
                foreach (var circle in circles)
                {
                    circle.Remap(modernScale);
                    circle.InnerRadius = 0.018f;
                    circle.RoundedCaps = true;
                }
            }
        }

        private partial class GradedCircle : CircularProgress
        {
            public double RevealProgress
            {
                set => Progress = Math.Clamp(value, startProgress, endProgress) - startProgress;
            }

            private double startProgress;
            private double endProgress;

            public void Remap(V2AccuracyScale scale)
            {
                const double gap = 0.6 / 360;
                startProgress = Math.Max(0.5, scale.Map(startProgress) / 360) + gap;
                endProgress = Math.Max(startProgress, scale.Map(endProgress) / 360 - gap);
                Rotation = (float)startProgress * 360;
            }

            public GradedCircle(double startProgress, double endProgress, bool useV2Style)
            {
                double spacing = useV2Style ? 0 : AccuracyCircle.GRADE_SPACING_PERCENTAGE * 0.5;
                this.startProgress = startProgress + spacing;
                this.endProgress = Math.Max(this.startProgress, endProgress - spacing);

                Anchor = Anchor.Centre;
                Origin = Anchor.Centre;
                RelativeSizeAxes = Axes.Both;
                InnerRadius = AccuracyCircle.RANK_CIRCLE_RADIUS;
                Rotation = (float)this.startProgress * 360;
            }
        }
    }
}
