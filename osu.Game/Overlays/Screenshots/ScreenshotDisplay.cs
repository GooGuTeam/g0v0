// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Online.API.Requests.Responses;
using osuTK;

namespace osu.Game.Overlays.Screenshots
{
    public partial class ScreenshotDisplay : CompositeDrawable
    {
        private const float spacing = 10;

        private const int columns = 3;

        /// <summary>
        /// The aspect ratio the tiles are laid out at, matching the shape screenshots are usually taken in.
        /// </summary>
        private const float tile_aspect_ratio = 9 / 16f;

        private readonly Action<APIScreenshotInfo> onDeleteRequested;

        private readonly List<APIScreenshotInfo> screenshots = new List<APIScreenshotInfo>();

        private readonly FillFlowContainer<ScreenshotGridTile> flow;

        /// <summary>
        /// The width the tiles were last laid out for, so that the layout only runs when it can actually change.
        /// </summary>
        private float laidOutWidth;

        public ScreenshotDisplay(Action<APIScreenshotInfo> onDeleteRequested)
        {
            this.onDeleteRequested = onDeleteRequested;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = flow = new FillFlowContainer<ScreenshotGridTile>
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Full,
                Spacing = new Vector2(spacing),
            };
        }

        /// <summary>
        /// The number of screenshots currently shown.
        /// </summary>
        public int Count => screenshots.Count;

        public void AddScreenshots(IEnumerable<APIScreenshotInfo> items)
        {
            foreach (var item in items)
            {
                screenshots.Add(item);

                // the size is replaced on the next layout pass, but a sane default is used up front so that
                // nothing is ever laid out at zero size.
                flow.Add(new ScreenshotGridTile(item, onDeleteRequested) { Size = ScreenshotGridTile.DEFAULT_SIZE });
            }
        }

        /// <summary>
        /// Removes a screenshot from the display. Returns whether the screenshot was present.
        /// </summary>
        public bool RemoveScreenshot(APIScreenshotInfo screenshot)
        {
            if (!screenshots.Remove(screenshot))
                return false;

            var tile = flow.FirstOrDefault(t => t.Screenshot == screenshot);

            if (tile != null)
                flow.Remove(tile, true);

            return true;
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            updateLayout();
        }

        private void updateLayout()
        {
            float width = DrawWidth;

            if (width <= spacing * (columns - 1) || Math.Abs(width - laidOutWidth) < 0.5f)
                return;

            laidOutWidth = width;

            float tileWidth = (width - spacing * (columns - 1)) / columns;
            var tileSize = new Vector2(tileWidth, tileWidth * tile_aspect_ratio);

            foreach (var tile in flow)
                tile.Size = tileSize;
        }
    }
}
