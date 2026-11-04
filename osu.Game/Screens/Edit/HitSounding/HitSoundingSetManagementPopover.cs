// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// Lists the set lanes in their current display order and lets the user reorder them or collapse them.
    /// </summary>
    /// <remarks>
    /// This only changes <see cref="HitSoundingSetLayout"/>; the sample set indices stored in the beatmap are never touched.
    /// </remarks>
    public partial class HitSoundingSetManagementPopover : OsuPopover
    {
        private const float maximum_rows_height = 300;
        private const float row_height = 28;
        private const float row_spacing = 2;

        private readonly HitSoundingSetLayout layout;

        private OsuScrollContainer scroll = null!;
        private FillFlowContainer<SetRow> rows = null!;

        public HitSoundingSetManagementPopover(HitSoundingSetLayout layout)
        {
            this.layout = layout;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    Width = 280,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 6),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = HitSoundingStrings.ManageSets,
                            Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold),
                            Colour = colours.Light3,
                        },
                        scroll = new OsuScrollContainer(Direction.Vertical)
                        {
                            RelativeSizeAxes = Axes.X,
                            ScrollbarOverlapsContent = false,
                            Child = rows = new FillFlowContainer<SetRow>
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, row_spacing),
                            },
                        },
                    },
                },
            };

            layout.Changed += onLayoutChanged;
            rebuildRows();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                layout.Changed -= onLayoutChanged;

            base.Dispose(isDisposing);
        }

        // Deferred so that a row is never disposed from inside its own click handler.
        private void onLayoutChanged() => Schedule(rebuildRows);

        private void rebuildRows()
        {
            rows.Clear();

            foreach (int set in layout.DisplayOrder)
                rows.Add(new SetRow(layout, set));

            int count = layout.DisplayOrder.Count;
            scroll.Height = count == 0 ? 0 : Math.Min(maximum_rows_height, count * row_height + (count - 1) * row_spacing);
        }

        private partial class SetRow : CompositeDrawable
        {
            private readonly HitSoundingSetLayout layout;
            private readonly int set;

            public SetRow(HitSoundingSetLayout layout, int set)
            {
                this.layout = layout;
                this.set = set;

                RelativeSizeAxes = Axes.X;
                Height = row_height;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                bool collapsed = layout.IsCollapsed(set);
                bool canMoveUp = layout.CanMove(set, -1);
                bool canMoveDown = layout.CanMove(set, 1);

                AddRangeInternal(new Drawable[]
                {
                    new TruncatingSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 2,
                        Width = 180,
                        Text = set == 0 ? HitSoundingStrings.UserSkin : HitSoundingStrings.CustomSet(set),
                        Font = OsuFont.GetFont(size: 14),
                        Colour = collapsed ? colours.Light4 : colours.Light3,
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(2, 0),
                        Children = new[]
                        {
                            createButton(
                                collapsed ? FontAwesome.Solid.ChevronRight : FontAwesome.Solid.ChevronDown,
                                collapsed ? HitSoundingStrings.ExpandSet : HitSoundingStrings.CollapseSet,
                                () => layout.ToggleCollapsed(set),
                                colours),
                            createButton(
                                FontAwesome.Solid.ArrowUp,
                                HitSoundingStrings.MoveSetUp,
                                () =>
                                {
                                    if (canMoveUp)
                                        layout.Move(set, -1);
                                },
                                colours,
                                canMoveUp),
                            createButton(
                                FontAwesome.Solid.ArrowDown,
                                HitSoundingStrings.MoveSetDown,
                                () =>
                                {
                                    if (canMoveDown)
                                        layout.Move(set, 1);
                                },
                                colours,
                                canMoveDown),
                        },
                    },
                });
            }

            private static IconButton createButton(IconUsage icon, LocalisableString tooltip, Action action, OverlayColourProvider colours, bool enabled = true)
                => new IconButton
                {
                    Icon = icon,
                    TooltipText = tooltip,
                    Size = new Vector2(24),
                    IconScale = new Vector2(0.8f),
                    IconColour = colours.Light3,
                    Action = enabled ? action : null,
                };
        }
    }
}
