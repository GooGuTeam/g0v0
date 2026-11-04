// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// The display order and collapsed state of the sample set lanes in <see cref="HitSoundingRoll"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This only affects how lanes are presented; the sample set indices stored on hit objects are never changed, and
    /// samples in collapsed sets keep playing back.
    /// </para>
    /// <para>
    /// Expanded sets are listed first in their user-chosen order, followed by every collapsed set in the same relative
    /// order, so collapsing a set moves its single-row card to the bottom of the list.
    /// </para>
    /// </remarks>
    public class HitSoundingSetLayout
    {
        private readonly List<int> order = [];
        private readonly HashSet<int> collapsed = [];
        private readonly List<int> display = [];
        private readonly List<float> rowY = [];

        /// <summary>
        /// Invoked after the user changed the order or collapsed state of a set.
        /// </summary>
        public event Action? Changed;

        /// <summary>
        /// Every known set, in display order.
        /// </summary>
        public IReadOnlyList<int> DisplayOrder => display;

        /// <summary>
        /// The total height of every set row including the gaps after them.
        /// </summary>
        public float TotalHeight { get; private set; }

        public bool Contains(int set) => order.Contains(set);

        public bool IsCollapsed(int set) => collapsed.Contains(set);

        /// <summary>
        /// The top of <paramref name="set"/>'s row, or <c>null</c> if the set is unknown.
        /// </summary>
        public float? RowY(int set)
        {
            int index = display.IndexOf(set);
            return index < 0 ? null : rowY[index];
        }

        public static float RowHeight(bool isCollapsed) => isCollapsed ? HitSoundingLayout.COLLAPSED_SET_HEIGHT : HitSoundingLayout.SET_HEIGHT;

        /// <summary>
        /// The set whose row covers <paramref name="y"/>, along with the offset of <paramref name="y"/> from the row's top.
        /// Gaps between rows belong to no set.
        /// </summary>
        public bool TryGetSetAt(float y, out int set, out float offset)
        {
            for (int i = 0; i < display.Count; i++)
            {
                offset = y - rowY[i];
                if (offset >= 0 && offset < RowHeight(collapsed.Contains(display[i])))
                {
                    set = display[i];
                    return true;
                }
            }

            set = 0;
            offset = 0;
            return false;
        }

        /// <summary>
        /// Appends sets which are not known yet, in ascending order, without notifying <see cref="Changed"/>.
        /// </summary>
        /// <returns>Whether any set was added.</returns>
        public bool Include(IEnumerable<int> sets)
        {
            bool added = false;

            foreach (int set in sets.Distinct().Order())
            {
                if (order.Contains(set))
                    continue;

                order.Add(set);
                added = true;
            }

            if (added)
                updateDisplay();
            return added;
        }

        public void SetCollapsed(int set, bool collapse)
        {
            if (!order.Contains(set) || (collapse ? !collapsed.Add(set) : !collapsed.Remove(set)))
                return;

            updateDisplay();
            Changed?.Invoke();
        }

        public void ToggleCollapsed(int set) => SetCollapsed(set, !collapsed.Contains(set));

        /// <summary>
        /// Swaps <paramref name="set"/> with its neighbour in <see cref="DisplayOrder"/> in the given direction.
        /// Sets never cross between the expanded and collapsed groups.
        /// </summary>
        public void Move(int set, int direction)
        {
            int index = display.IndexOf(set);
            int target = index + Math.Sign(direction);
            if (index < 0 || direction == 0 || !CanMove(set, direction))
                return;

            int other = display[target];
            int a = order.IndexOf(set);
            int b = order.IndexOf(other);
            (order[a], order[b]) = (order[b], order[a]);
            updateDisplay();
            Changed?.Invoke();
        }

        public bool CanMove(int set, int direction)
        {
            int index = display.IndexOf(set);
            int target = index + Math.Sign(direction);
            return index >= 0 && direction != 0 && target >= 0 && target < display.Count
                   && collapsed.Contains(display[target]) == collapsed.Contains(set);
        }

        private void updateDisplay()
        {
            display.Clear();
            display.AddRange(order.Where(s => !collapsed.Contains(s)));
            display.AddRange(order.Where(collapsed.Contains));

            rowY.Clear();
            float y = 0;

            foreach (int set in display)
            {
                rowY.Add(y);
                y += RowHeight(collapsed.Contains(set)) + HitSoundingLayout.SET_SPACING;
            }

            TotalHeight = y;
        }
    }
}
