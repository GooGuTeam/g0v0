// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Threading;
using M = osu.Game.Overlays.Settings.Rendering.Stable.StableOptionsMetrics;

namespace osu.Game.Overlays.Settings.Rendering.Stable
{
    /// <summary>
    /// osu!stable's options tab column: a 40 unit wide #080808 strip with the tab icons centred vertically, which widens over the options
    /// to show the tabs' titles while the mouse rests on it (<c>Options.UpdateTabs</c>).
    /// It never has a back button of its own; the root panel shows stable's <see cref="StableBackButton"/> instead.
    /// </summary>
    public partial class StableSettingsSidebar : SettingsSidebar
    {
        public StableSettingsSidebar()
            : base(false, M.SIDEBAR_WIDTH, M.SIDEBAR_EXPANDED_WIDTH)
        {
            Expanded.Value = false;
        }

        // stable only reacts to the tabs themselves, not the rest of the column (e.g. the back button drawn over its bottom).
        protected override bool ExpandOnHover => false;

        private ScheduledDelegate? pendingChange;
        private bool targetExpanded;

        protected override void Update()
        {
            base.Update();

            bool hoveringTab = FillFlow.Any(tab => tab.IsHovered);

            if (hoveringTab == targetExpanded)
                return;

            // either way the change waits for the mouse to rest for a moment, as stable's expandDelayTabs does.
            targetExpanded = hoveringTab;
            pendingChange?.Cancel();
            pendingChange = Scheduler.AddDelayed(() => Expanded.Value = targetExpanded, M.SIDEBAR_EXPAND_DELAY);
        }

        // the expanded column slides over the options rather than pushing them aside.
        public override float ContentOffset => M.SIDEBAR_WIDTH;

        protected override Drawable CreateBackground(OverlayColourProvider colourProvider) => new Box
        {
            Colour = M.SIDEBAR_BACKGROUND,
            RelativeSizeAxes = Axes.Both,
        };
    }
}
