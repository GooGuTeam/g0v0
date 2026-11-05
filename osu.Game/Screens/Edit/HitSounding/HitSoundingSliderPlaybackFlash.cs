// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Objects;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// Clips one outer slider contour into independently timed head, body, tail and repeat highlights.
    /// </summary>
    public partial class HitSoundingSliderPlaybackFlash : CompositeDrawable
    {
        private const float body_alpha = 0.12f;
        private const float glow_radius = 12;
        private readonly Container surface;
        private readonly Container head;
        private readonly Container body;
        private readonly Container tail;
        private readonly Dictionary<int, (Container Section, double Time)> repeats = [];
        private double startTime;
        private double endTime;
        private bool active;
        private bool tailTriggered;

        internal bool HasBody { get; set; }
        internal bool HasTail { get; set; }

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        public HitSoundingSliderPlaybackFlash()
        {
            Origin = Anchor.Centre;
            Height = HitSoundingLayout.SAMPLE_HEIGHT;
            InternalChild = surface = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    head = createSection(@"Slider head highlight"),
                    body = createSection(@"Slider body highlight"),
                    tail = createSection(@"Slider tail highlight")
                }
            };
        }

        private static Container createContour() => new Container
        {
            RelativeSizeAxes = Axes.Y,
            Masking = true,
            CornerRadius = HitSoundingLayout.SAMPLE_CORNER_RADIUS,
            BorderThickness = 1.5f,
            BorderColour = Color4.White,
            EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Glow, Colour = new Color4(1, 1, 1, 0.55f), Radius = glow_radius, Hollow = true },
            Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = HitSoundingLayout.PLAYBACK_FLASH_OPACITY }
        };

        private static Container createSection(string name) => new Container
        {
            Name = name,
            Y = -glow_radius,
            Height = HitSoundingLayout.SAMPLE_HEIGHT + 2 * glow_radius,
            Masking = true,
            Alpha = 0,
            Child = createContour()
        };

        internal void Trigger(HitSoundingNode node)
        {
            startTime = node.HitObject.StartTime;
            endTime = node.HitObject.GetEndTime();

            if (!active)
            {
                active = true;
                tailTriggered = false;
            }

            if (HasBody && !tailTriggered && node.Time < endTime)
            {
                body.ClearTransforms();
                body.Alpha = body_alpha;
            }

            if (node.IsBody)
                return;

            if (node.NodeIndex == 0)
                head.FadeOutFromOne(HitSoundingLayout.PLAYBACK_FLASH_DURATION, Easing.OutQuint);
            else if (node.Time >= endTime)
                flashTail(true);
            else
            {
                if (!repeats.TryGetValue(node.NodeIndex, out var repeat))
                {
                    var section = createSection(@"Slider repeat highlight");
                    // The separate badge outline is deliberately faint; the clipped outer contour above it
                    // supplies the strong top and bottom edges without bright internal vertical joins.
                    var sides = createContour();
                    sides.RelativeSizeAxes = Axes.Both;
                    sides.Alpha = body_alpha;
                    section.Add(sides);
                    repeats.Add(node.NodeIndex, repeat = (section, node.Time));
                    surface.Add(section);
                }

                repeat.Section.FadeOutFromOne(HitSoundingLayout.PLAYBACK_FLASH_DURATION, Easing.OutQuint);
            }
        }

        private void flashTail(bool includeTail)
        {
            if (includeTail)
                tail.FadeOutFromOne(HitSoundingLayout.PLAYBACK_FLASH_DURATION, Easing.OutQuint);

            if (tailTriggered)
                return;

            tailTriggered = true;
            if (HasBody)
                body.FadeOutFromOne(HitSoundingLayout.PLAYBACK_FLASH_DURATION, Easing.OutQuint);
        }

        protected override void Update()
        {
            base.Update();
            float cap = Math.Min(HitSoundingLayout.SAMPLE_WIDTH, DrawWidth / 2);
            layoutSection(head, 0, cap, !HasBody);
            // The tail-triggered body overlay also covers the entire head badge.
            layoutSection(body, 0, Math.Max(0, DrawWidth - (HasTail ? cap : 0)));
            layoutSection(tail, DrawWidth - cap, cap, !HasBody);

            foreach (var repeat in repeats.Values)
            {
                float fraction = endTime > startTime ? (float)((repeat.Time - startTime) / (endTime - startTime)) : 0;
                float x = HitSoundingLayout.SAMPLE_WIDTH / 2 + fraction * (DrawWidth - HitSoundingLayout.SAMPLE_WIDTH);
                layoutSection(repeat.Section, x - cap / 2, cap, !HasBody);
            }

            if (!active)
                return;

            if (editorClock.IsSeeking || editorClock.CurrentTime < startTime)
            {
                foreach (var child in surface.Children)
                {
                    child.ClearTransforms();
                    child.Alpha = 0;
                }

                active = false;
            }
            else if (editorClock.CurrentTime >= endTime)
            {
                flashTail(false);

                if (editorClock.CurrentTime >= endTime + HitSoundingLayout.PLAYBACK_FLASH_DURATION)
                    active = false;
            }
        }

        private void layoutSection(Container section, float x, float width, bool standalone = false)
        {
            float left = standalone || x <= 0 ? glow_radius : 0;
            float right = standalone || x + width >= DrawWidth ? glow_radius : 0;
            section.X = x - left;
            section.Width = width + left + right;
            var contour = section.Children[0];
            contour.RelativeSizeAxes = Axes.None;
            contour.Y = glow_radius;
            contour.Height = HitSoundingLayout.SAMPLE_HEIGHT;
            contour.X = standalone ? left : left - x;
            contour.Width = standalone ? width : DrawWidth;

            if (section.Children.Count > 1)
            {
                var sides = section.Children[1];
                sides.RelativeSizeAxes = Axes.None;
                sides.X = left;
                sides.Y = glow_radius;
                sides.Width = width;
                sides.Height = HitSoundingLayout.SAMPLE_HEIGHT;
            }
        }
    }
}
