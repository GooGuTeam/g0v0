// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Overlays;

namespace osu.Game.Screens.Edit.HitSounding
{
    public partial class HitSoundingBeatGrid : Container
    {
        private readonly List<Box> lines = [];
        private double startTime;
        private double endTime;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Refresh(startTime, endTime);
        }

        [Resolved]
        private EditorBeatmap beatmap { get; set; } = null!;

        [Resolved]
        private BindableBeatDivisor divisor { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colours { get; set; } = null!;

        public HitSoundingBeatGrid()
        {
            RelativeSizeAxes = Axes.Both;
        }

        public void Refresh(double start, double end)
        {
            startTime = start;
            endTime = end;
            if (!IsLoaded || end <= start || DrawWidth <= 0)
                return;

            int used = 0;
            var points = beatmap.ControlPointInfo.TimingPoints;
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                double until = i + 1 < points.Count ? Math.Min(end, points[i + 1].Time) : end;
                if (until < start)
                    continue;
                double step = point.BeatLength / divisor.Value;
                if (step <= 0)
                    continue;
                // Avoid subpixel grid lines at extreme zoom levels.
                int stride = Math.Max(1, (int)Math.Ceiling((end - start) / DrawWidth * 4 / step));
                long first = Math.Max(0, (long)Math.Ceiling((start - point.Time) / (step * stride)) * stride);
                for (long beat = first; point.Time + beat * step < until; beat += stride)
                {
                    Box line;
                    if (used == lines.Count)
                    {
                        lines.Add(line = new Box { RelativeSizeAxes = Axes.Y, Width = 1 });
                        Add(line);
                    }
                    else
                        line = lines[used];
                    used++;
                    line.X = (float)((point.Time + beat * step - start) / (end - start) * DrawWidth);
                    line.Colour = colours.Background2;
                    line.Alpha = beat % divisor.Value == 0 ? 1 : 0.45f;
                }
            }
            for (int i = used; i < lines.Count; i++)
                lines[i].Alpha = 0;
        }
    }
}
