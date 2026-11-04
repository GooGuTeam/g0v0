// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Overlays;

namespace osu.Game.Screens.Edit.HitSounding
{
    public partial class HitSoundingSampleGuides : Container
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
        private HitSoundingEditModel model { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colours { get; set; } = null!;

        public HitSoundingSampleGuides()
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
            double previousTime = double.NaN;

            foreach (var node in model.Nodes)
            {
                if (node.IsBody || node.Time < start)
                    continue;

                if (node.Time > end)
                    break;

                if (node.Time == previousTime)
                    continue;

                previousTime = node.Time;

                Box line;

                if (used == lines.Count)
                {
                    lines.Add(line = new Box { RelativeSizeAxes = Axes.Y, Width = 1, Colour = colours.Background2 });
                    Add(line);
                }
                else
                    line = lines[used];

                used++;

                line.X = (float)((node.Time - start) / (end - start) * DrawWidth);
                line.Alpha = 0.6f;
            }

            for (int i = used; i < lines.Count; i++)
                lines[i].Alpha = 0;
        }
    }
}
