// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Utils;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osuTK;

namespace osu.Game.Rulesets.Osu.Skinning
{
    /// <summary>
    /// A <see cref="SliderBody"/> which changes its curve depending on the snaking progress.
    /// </summary>
    public abstract partial class SnakingSliderBody : SliderBody, ISliderProgress
    {
        private readonly List<Vector2> currentCurve = new List<Vector2>();
        private bool currentCurveValid;

        public List<Vector2> CurrentCurve
        {
            get
            {
                if (!currentCurveValid)
                {
                    currentCurve.Clear();
                    if (Path.DrawRange is { } range)
                    {
                        currentCurve.Add(range.StartPoint);
                        for (int i = range.FirstVertex; i < range.FirstVertex + range.VertexCount; i++)
                            currentCurve.Add(Path.Vertices[i]);
                        currentCurve.Add(range.EndPoint);
                    }
                    currentCurveValid = true;
                }
                return currentCurve;
            }
        }

        /// <summary>
        /// Finds the first distinct point from a snaking endpoint without materialising the visible curve.
        /// </summary>
        public Vector2 GetSnakingDirection(Vector2 position, bool fromEnd)
        {
            if (Path.DrawRange is not { } range)
                return Vector2.Zero;

            int count = range.VertexCount + 2;
            for (int i = fromEnd ? count - 1 : 0; i >= 0 && i < count; i += fromEnd ? -1 : 1)
            {
                Vector2 point = i == 0 ? range.StartPoint
                    : i == count - 1 ? range.EndPoint
                    : Path.Vertices[range.FirstVertex + i - 1];
                if (!Precision.AlmostEquals(point, position))
                    return point - position;
            }
            return -position;
        }

        public readonly Bindable<bool> SnakingIn = new Bindable<bool>();
        public readonly Bindable<bool> SnakingOut = new Bindable<bool>();

        public double? SnakedStart { get; private set; }
        public double? SnakedEnd { get; private set; }

        public override float PathRadius
        {
            get => base.PathRadius;
            set
            {
                if (base.PathRadius == value)
                    return;

                base.PathRadius = value;

                Refresh();
            }
        }

        public override Vector2 PathOffset => snakedPathOffset;

        public override Vector2 PathEndOffset => snakedPathEndOffset;

        /// <summary>
        /// The start of the full path relative to its bounding box.
        /// </summary>
        private Vector2 snakedPathOffset;

        /// <summary>
        /// The end of the full path relative to its bounding box.
        /// </summary>
        private Vector2 snakedPathEndOffset;

        private DrawableSlider drawableSlider = null!;
        private SliderPath? currentPath;
        private int currentPathVersion;

        [BackgroundDependencyLoader]
        private void load(DrawableHitObject drawableObject)
        {
            drawableSlider = (DrawableSlider)drawableObject;

            Refresh();
        }

        public void UpdateProgress(double completionProgress)
        {
            if (drawableSlider.HitObject == null)
                return;

            Slider slider = drawableSlider.HitObject;

            int span = slider.SpanAt(completionProgress);
            double spanProgress = slider.ProgressAt(completionProgress);

            double start = 0;
            double end = SnakingIn.Value ? Math.Clamp((Time.Current - (slider.StartTime - slider.TimePreempt)) / (slider.TimePreempt / 3), 0, 1) : 1;

            if (span >= slider.SpanCount() - 1)
            {
                if (Math.Min(span, slider.SpanCount() - 1) % 2 == 1)
                {
                    start = 0;
                    end = SnakingOut.Value ? spanProgress : 1;
                }
                else
                {
                    start = SnakingOut.Value ? spanProgress : 0;
                }
            }

            setRange(start, end);
        }

        public void Refresh()
        {
            if (drawableSlider.HitObject == null)
                return;

            double lastSnakedStart = SnakedStart ?? 0;
            double lastSnakedEnd = SnakedEnd ?? 0;

            refreshPath(drawableSlider.HitObject.Path);
            setRange(lastSnakedStart, lastSnakedEnd);
        }

        private void refreshPath(SliderPath sliderPath)
        {
            // Geometry and bounds remain fixed while only the visible range changes.
            SetVertices(sliderPath.CalculatedPath);
            currentPath = sliderPath;
            currentPathVersion = sliderPath.Version.Value;
            currentCurveValid = false;
            Path.Position = Vector2.Zero;

            // Force the body to be the final path size to avoid excessive autosize computations
            Path.AutoSizeAxes = Axes.Both;
            Size = Path.Size;

            updatePathSize();

            snakedPathOffset = Path.PositionInBoundingBox(Path.Vertices.Count > 0 ? Path.Vertices[0] : Vector2.Zero);
            snakedPathEndOffset = Path.PositionInBoundingBox(Path.Vertices.Count > 0 ? Path.Vertices[^1] : Vector2.Zero);

            SnakedStart = null;
            SnakedEnd = null;
        }

        public override void RecyclePath()
        {
            base.RecyclePath();
            currentCurveValid = false;
            updatePathSize();
        }

        private void updatePathSize()
        {
            // Force the path to its final size to avoid excessive framebuffer resizes
            Path.AutoSizeAxes = Axes.None;
            Path.Size = Size;
        }

        private void setRange(double p0, double p1)
        {
            SliderPath sliderPath = drawableSlider.HitObject.Path;

            // A pooled body can receive progress before its scheduled refresh, especially after seeking.
            // Check identity and version before reusing the range, even when progress has not changed.
            if (currentPath != sliderPath || currentPathVersion != sliderPath.Version.Value)
                refreshPath(sliderPath);

            if (p0 > p1)
                (p0, p1) = (p1, p0);

            if (SnakedStart == p0 && SnakedEnd == p1) return;

            Path.DrawRange = sliderPath.GetPathToProgressRange(p0, p1);
            SnakedStart = p0;
            SnakedEnd = p1;
            currentCurveValid = false;
        }
    }
}
