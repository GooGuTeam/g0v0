// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace osu.Game.Overlays.Settings
{
    /// <summary>
    /// A layer above a settings panel's sections (and below its pinned header) for content that floats over the following settings,
    /// such as an open dropdown list. Items add proxies of their floating parts here, so those draw and take input in front of the
    /// settings below them while staying laid out (and scrolled) with their item.
    /// </summary>
    public partial class SettingsFlyoutLayer : Container
    {
        public SettingsFlyoutLayer()
        {
            RelativeSizeAxes = Axes.Both;
        }

        /// <summary>
        /// Removes a flyout added for an item that is being disposed. Disposal may run off the update thread, so this is scheduled.
        /// </summary>
        public void RemoveFlyout(Drawable flyout) => Schedule(() =>
        {
            if (flyout.Parent == this)
                Remove(flyout, true);
        });
    }
}
