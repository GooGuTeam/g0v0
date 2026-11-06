// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// A single run of consecutive <see cref="HitSoundingNode"/>s sharing the same volume, drawn as a step of the
    /// volume graph in <see cref="HitSoundingVolumeTrack"/>.
    /// </summary>
    /// <remarks>
    /// Only one marker is drawn per run, at the time its volume starts changing. Runs that are completely overlapped
    /// in time (two nodes at the same time with different volumes) still get their own marker, so neither value is lost.
    /// A segment holds no run of its own: the track keeps a pool of them and <see cref="Bind"/>s one to whichever run
    /// is currently inside the visible window, so nothing here may depend on the run it showed before.
    /// </remarks>
    public partial class HitSoundingVolumeSegment : CompositeDrawable
    {
        /// <summary>
        /// The top of the volume graph, corresponding to a volume of 100.
        /// </summary>
        public const float CHART_TOP = 22;

        /// <summary>
        /// The bottom of the volume graph, corresponding to a volume of 0.
        /// </summary>
        public const float CHART_BOTTOM = 92;

        /// <summary>
        /// The nodes which share <see cref="Volume"/>, ordered by time.
        /// </summary>
        public IReadOnlyList<HitSoundingNode> Nodes { get; private set; } = Array.Empty<HitSoundingNode>();

        /// <summary>
        /// The volume stored in the beatmap for <see cref="Nodes"/>.
        /// </summary>
        public int Volume { get; private set; }

        /// <summary>
        /// The volume currently displayed. This differs from <see cref="Volume"/> while the user is dragging the run.
        /// </summary>
        public int DisplayVolume { get; private set; }

        public float StartX { get; private set; }
        public float EndX { get; private set; }

        /// <summary>
        /// The vertical position of the step line, in the coordinate space of this drawable.
        /// </summary>
        public float LineY { get; private set; }

        /// <summary>
        /// The horizontal centre of the run.
        /// </summary>
        public float CentreX => StartX + (EndX - StartX) / 2;

        /// <summary>
        /// Whether the run currently intersects the visible time window. The track materialises a segment slightly
        /// ahead of the window, so a segment living in the prefetch margin remains transparent until the window
        /// reaches it.
        /// </summary>
        internal bool IsWithinVisibleWindow { get; private set; }

        private readonly Box fill;
        private readonly Box line;
        private readonly Box connector;
        private readonly Container dot;
        private readonly Circle dotBorder;
        private readonly Circle dotCore;
        private readonly OsuSpriteText label;
        private readonly Container grip;

        private float previousLineY;
        private bool hasPrevious;
        private bool selected;
        private bool gripHovered;
        private float viewportWidth;

        private Color4 lineColour;
        private Color4 selectedLineColour;
        private Color4 pointColour;
        private Color4 fillColour;

        public HitSoundingVolumeSegment()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                fill = new Box
                {
                    Alpha = 0.15f,
                },
                connector = new Box
                {
                    Width = 2,
                },
                line = new Box
                {
                    Height = 2,
                },
                dot = new Container
                {
                    Size = new Vector2(16),
                    Children = new Drawable[]
                    {
                        dotBorder = new Circle
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(16),
                            Colour = Colour4.White,
                        },
                        dotCore = new Circle
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(8),
                        },
                    },
                },
                label = new OsuSpriteText
                {
                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                    Origin = Anchor.BottomLeft,
                    Anchor = Anchor.TopLeft,
                },
                grip = new Container
                {
                    Size = new Vector2(20, 6),
                    CornerRadius = 3,
                    Masking = true,
                    Alpha = 0,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                    }
                }
            };

            LineY = VolumeToY(DisplayVolume);
            previousLineY = LineY;

            updateLabelText();
            updateDrawables();
        }

        /// <summary>
        /// Binds this segment to a run of nodes. Segments are pooled by the track and re-bound as the visible window
        /// moves, so every piece of state which belongs to the run has to be reset here.
        /// </summary>
        /// <param name="nodes">The nodes which share <paramref name="volume"/>, ordered by time.</param>
        /// <param name="volume">The volume stored in the beatmap for the run.</param>
        /// <param name="selected">Whether the run is currently selected.</param>
        /// <param name="hasPrevious">Whether there is a preceding run to connect to.</param>
        public void Bind(IReadOnlyList<HitSoundingNode> nodes, int volume, bool selected, bool hasPrevious)
        {
            Nodes = nodes;
            Volume = volume;
            DisplayVolume = volume;

            LineY = VolumeToY(volume);
            previousLineY = LineY;
            this.hasPrevious = hasPrevious;

            // a recycled segment may still be fading the grip of the run it showed before, so take the new state
            // on instantly instead of animating from it.
            this.selected = selected;
            gripHovered = false;
            grip.ClearTransforms();
            grip.Alpha = selected ? 1 : 0;

            updateLabelText();
            applyColours();
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, OsuColour colours)
        {
            lineColour = colourProvider.Light3;
            selectedLineColour = colours.Pink1;
            pointColour = colourProvider.Content2;
            fillColour = colourProvider.Light4;

            fill.Colour = fillColour;
            applyColours();
            updateGripVisibility();
        }

        /// <summary>
        /// Updates the horizontal and vertical placement of this run.
        /// </summary>
        /// <param name="startX">The left edge of the run.</param>
        /// <param name="endX">The right edge of the run.</param>
        /// <param name="lineY">The vertical position of the run's step line.</param>
        /// <param name="previousLineY">The vertical position of the preceding run's step line.</param>
        /// <param name="hasPrevious">Whether there is a preceding run to connect to.</param>
        /// <param name="viewportWidth">The width of the visible time window.</param>
        public void UpdateLayout(float startX, float endX, float lineY, float previousLineY, bool hasPrevious, float viewportWidth)
        {
            StartX = startX;
            EndX = endX;
            LineY = lineY;
            this.previousLineY = previousLineY;
            this.hasPrevious = hasPrevious;
            this.viewportWidth = viewportWidth;

            updateDrawables();
        }

        /// <summary>
        /// Displays <paramref name="volume"/> without changing the beatmap. Used to preview a drag.
        /// </summary>
        public void SetDisplayVolume(int volume)
        {
            if (DisplayVolume == volume)
                return;

            DisplayVolume = volume;
            LineY = VolumeToY(volume);

            updateLabelText();
            updateDrawables();
        }

        /// <summary>
        /// Updates the line connecting the preceding run to this one, used while the preceding run is being dragged.
        /// </summary>
        public void SetPreviousLineY(float y)
        {
            previousLineY = y;
            hasPrevious = true;
            updateConnector();
        }

        public void SetSelected(bool value)
        {
            if (selected == value)
                return;

            selected = value;
            applyColours();
            updateGripVisibility();
        }

        public void SetGripVisible(bool visible)
        {
            if (gripHovered == visible)
                return;

            gripHovered = visible;
            updateGripVisibility();
        }

        private void updateGripVisibility() => grip.FadeTo(gripHovered || selected ? 1 : 0, 100, Easing.OutQuint);

        private void updateDrawables()
        {
            bool visible = StartX < viewportWidth && (EndX > 0 || (StartX == EndX && StartX >= 0));
            IsWithinVisibleWindow = visible;

            Alpha = visible ? 1 : 0;
            if (!visible)
                return;

            float visibleStart = Math.Max(StartX, 0);
            float visibleEnd = Math.Min(EndX, viewportWidth);
            float width = Math.Max(0, visibleEnd - visibleStart);

            fill.Position = new Vector2(visibleStart, LineY);
            fill.Size = new Vector2(width, CHART_BOTTOM - LineY);

            line.Position = new Vector2(visibleStart, LineY - line.Height / 2);
            line.Width = width;

            dot.Position = new Vector2(StartX - dot.Size.X / 2, LineY - dot.Size.Y / 2);

            // the value sits just above the line so the line never strikes through it; a run starting before the
            // visible window keeps its value readable at the left edge.
            label.Position = new Vector2(Math.Max(StartX, 0) + 6, LineY - line.Height / 2 - 1);

            grip.Position = new Vector2(visibleStart + width / 2 - grip.Size.X / 2, LineY - grip.Size.Y / 2);

            updateConnector();
        }

        private void updateLabelText() => label.Text = DisplayVolume.ToString(CultureInfo.InvariantCulture);

        private void updateConnector()
        {
            connector.Position = new Vector2(StartX - connector.Width / 2, Math.Min(previousLineY, LineY));
            connector.Height = Math.Abs(previousLineY - LineY);
            connector.Alpha = hasPrevious && connector.Height > 0 ? 1 : 0;
        }

        private void applyColours()
        {
            line.Height = selected ? 3 : 2;
            line.Colour = connector.Colour = selected ? selectedLineColour : lineColour;

            dotCore.Size = new Vector2(selected ? 12 : 8);
            dotCore.Colour = selected ? selectedLineColour : pointColour;
            dotBorder.Alpha = selected ? 1 : 0;

            label.Colour = selected ? selectedLineColour : lineColour;

            updateDrawables();
        }

        /// <summary>
        /// Maps a volume in [0, 100] to its vertical position in the graph.
        /// </summary>
        public static float VolumeToY(int volume) => CHART_BOTTOM - Math.Clamp(volume, 0, 100) / 100f * (CHART_BOTTOM - CHART_TOP);

        /// <summary>
        /// Maps a vertical position in the graph to the nearest volume.
        /// </summary>
        public static int YToVolume(float y) => (int)Math.Round((CHART_BOTTOM - y) / (CHART_BOTTOM - CHART_TOP) * 100);
    }
}
