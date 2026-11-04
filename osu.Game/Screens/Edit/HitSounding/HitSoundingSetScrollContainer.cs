// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Input.Events;
using osu.Game.Graphics.Containers;
using osuTK.Input;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// Keeps the object and volume lanes pinned while the sample-set list scrolls vertically.
    /// </summary>
    public partial class HitSoundingSetScrollContainer : OsuScrollContainer
    {
        /// <summary>
        /// Scrolls the set list vertically by a wheel event.
        /// </summary>
        /// <remarks>
        /// Holding Shift makes the input system report the wheel on the horizontal axis, which the base vertical
        /// handler ignores, so the dominant axis is used instead.
        /// </remarks>
        public bool ScrollSets(ScrollEvent e)
        {
            float delta = Math.Abs(e.ScrollDelta.Y) >= Math.Abs(e.ScrollDelta.X) ? e.ScrollDelta.Y : e.ScrollDelta.X;

            if (delta == 0)
                return false;

            ScrollTo(Target - ScrollDistance * delta, true, e.IsPrecise ? 0.05 : DistanceDecayScroll);
            return true;
        }

        protected override bool OnScroll(ScrollEvent e) => e.ShiftPressed && !e.AltPressed && ScrollSets(e);

        protected override bool OnMouseDown(MouseDownEvent e) => e.Button != MouseButton.Right && base.OnMouseDown(e);

        // Empty-space drags belong to the roll's selection box; the scrollbar still handles its own drags.
        protected override bool OnDragStart(DragStartEvent e) => false;
    }
}
