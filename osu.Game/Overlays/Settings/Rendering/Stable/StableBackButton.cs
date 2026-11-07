// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Game.Skinning.Select;

namespace osu.Game.Overlays.Settings.Rendering.Stable
{
    /// <summary>
    /// osu!stable's back button (<c>BackButton</c>) in the bottom-left corner of the options. Stable uses one back button everywhere, so this
    /// hosts song select's <see cref="LegacyBackButton"/>: the skin's <c>menu-back</c> when it has one, otherwise the layered pink button.
    /// </summary>
    public partial class StableBackButton : CompositeDrawable
    {
        public Action? Action { get; init; }

        public StableBackButton()
        {
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            AutoSizeAxes = Axes.Both;

            InternalChild = new LegacyBackButton
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Action = () => Action?.Invoke(),
            };
        }

        // an open sub-panel would otherwise take the press as an outside click and close itself before this button steps back.
        protected override bool OnMouseDown(MouseDownEvent e) => true;
    }
}
