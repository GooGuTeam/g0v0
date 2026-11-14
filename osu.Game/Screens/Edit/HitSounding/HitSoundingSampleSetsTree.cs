// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// The bank and sample tokens the sample library stores assignments under.
    /// </summary>
    internal static class HitSoundingSampleTokens
    {
        public const string NORMAL = "normal";
        public const string SOFT = "soft";
        public const string DRUM = "drum";

        public const string HIT_NORMAL = "hitnormal";
        public const string WHISTLE = "whistle";
        public const string FINISH = "finish";
        public const string CLAP = "clap";

        public static LocalisableString BankName(string bank)
        {
            if (bank == SOFT)
                return HitSoundingStrings.Soft;

            if (bank == DRUM)
                return HitSoundingStrings.Drum;

            return HitSoundingStrings.Normal;
        }

        public static LocalisableString SampleName(string sample)
        {
            if (sample == WHISTLE)
                return HitSoundingStrings.Whistle;

            if (sample == FINISH)
                return HitSoundingStrings.Finish;

            if (sample == CLAP)
                return HitSoundingStrings.Clap;

            return HitSoundingStrings.Normal;
        }

        /// <summary>
        /// The "N" / "W" / "F" / "C" type letter shown at the start of a slot row.
        /// </summary>
        public static string TypeLetter(string sample)
        {
            if (sample == WHISTLE)
                return @"W";

            if (sample == FINISH)
                return @"F";

            if (sample == CLAP)
                return @"C";

            return @"N";
        }
    }

    /// <summary>
    /// The left half of the sample gallery: one always-expanded column per sample set, separated by thin rules and
    /// panned horizontally by the mouse wheel. Slots are the drop targets for file tiles dragged out of the library.
    /// </summary>
    public partial class HitSoundingSampleSetsTree : CompositeDrawable
    {
        public const float COLUMN_WIDTH = 230;
        public const float TREE_HEIGHT = 310;
        public const float COLUMN_SPACING = 10;
        public const float SEPARATOR_WIDTH = 1;
        public const float HEADER_HEIGHT = 40;
        public const float ADD_BUTTON_WIDTH = 130;
        public const float SCROLLBAR_HEIGHT = 8;
        public const float SET_HEADING_HEIGHT = 24;
        public const float BANK_HEADING_HEIGHT = 18;
        public const float BANK_INDENT = 16;
        public const float SLOT_HEIGHT = 18;
        public const float SLOT_INDENT = 32;
        public const float SECTION_SPACING = 5;

        private readonly HitSoundingSampleGallery gallery;
        private readonly List<HitSoundingSampleSetColumn> columns = new List<HitSoundingSampleSetColumn>();
        private readonly List<int> currentSets = new List<int>();

        private OverlayColourProvider colours = null!;
        private TreeViewport viewport = null!;
        private SetsScrollbar scrollbar = null!;
        private HitSoundingSampleSlot? hoveredSlot;
        private bool rebuildQueued = true;

        [Resolved]
        private HitSoundingSampleLibrary samples { get; set; } = null!;

        /// <summary>
        /// The sample set columns currently displayed, in order.
        /// </summary>
        public IReadOnlyList<HitSoundingSampleSetColumn> Columns => columns;

        /// <summary>
        /// Every slot row currently displayed, in reading order.
        /// </summary>
        public IEnumerable<HitSoundingSampleSlot> Slots => columns.SelectMany(column => column.Slots);

        public HitSoundingSampleSetsTree(HitSoundingSampleGallery gallery)
        {
            this.gallery = gallery;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            this.colours = colours;

            InternalChildren = new[]
            {
                createHeader(),
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = HEADER_HEIGHT + SECTION_SPACING },
                    Children = new Drawable[]
                    {
                        viewport = new TreeViewport(),
                        scrollbar = new SetsScrollbar
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                        },
                    },
                },
            };

            scrollbar.ScrollToRatio = ratio => viewport.ScrollToRatio(ratio);
            samples.Changed += onLibraryChanged;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            rebuildIfNeeded();
        }

        protected override void Update()
        {
            base.Update();

            if (rebuildQueued)
            {
                rebuildIfNeeded();
                rebuildQueued = false;
            }

            scrollbar.UpdateState(viewport.ScrollX, viewport.MaxScrollX, viewport.DrawWidth, viewport.ContentWidth);
        }

        /// <summary>
        /// Returns the slot underneath a screen space position, or <c>null</c> when the position is outside the tree.
        /// </summary>
        public HitSoundingSampleSlot? GetSlotAt(Vector2 screenSpacePosition)
        {
            Vector2 viewportLocal = viewport.ToLocalSpace(screenSpacePosition);

            if (viewportLocal.X < 0 || viewportLocal.Y < 0 || viewportLocal.X > viewport.DrawWidth || viewportLocal.Y > viewport.DrawHeight)
                return null;

            foreach (var column in columns)
            {
                foreach (var slot in column.Slots)
                {
                    Vector2 local = slot.ToLocalSpace(screenSpacePosition);

                    if (local.X >= 0 && local.Y >= 0 && local.X <= slot.DrawWidth && local.Y <= slot.DrawHeight)
                        return slot;
                }
            }

            return null;
        }

        /// <summary>
        /// Highlights the slot that a dragged file would be dropped on.
        /// </summary>
        public void UpdateDragHover(Vector2? screenSpacePosition)
        {
            HitSoundingSampleSlot? target = screenSpacePosition is Vector2 position ? GetSlotAt(position) : null;

            if (hoveredSlot == target)
                return;

            hoveredSlot?.SetDropTarget(false, null);

            hoveredSlot = target;

            hoveredSlot?.SetDropTarget(true, gallery.DraggingFilename == null ? null : gallery.GetDisplayName(gallery.DraggingFilename));
        }

        private Drawable createHeader() => new Container
        {
            RelativeSizeAxes = Axes.X,
            Height = HEADER_HEIGHT,
            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = HitSoundingStrings.SampleSetsHeader,
                    Font = OsuFont.Default.With(size: 17, weight: FontWeight.Bold),
                    Colour = colours.Light3,
                },
                new SampleTreeButton
                {
                    Name = "Add sample set",
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Width = ADD_BUTTON_WIDTH,
                    Height = HEADER_HEIGHT,
                    Text = HitSoundingStrings.AddSetButton,
                    TooltipText = HitSoundingStrings.TooltipAddSet,
                    Action = () => samples.AddSet(),
                },
            },
        };

        private void onLibraryChanged()
        {
            // Slots refresh from pushed changes rather than polling the library every frame. The rebuild below only
            // recreates the columns when the set list itself changed.
            foreach (var column in columns)
            {
                foreach (var slot in column.Slots)
                    slot.RefreshFromLibrary();
            }

            Schedule(() => rebuildQueued = true);
        }

        private void rebuildIfNeeded()
        {
            List<int> sets = samples.Sets.OrderBy(set => set).ToList();

            if (sets.SequenceEqual(currentSets))
                return;

            rebuild(sets);
        }

        private void rebuild(List<int> sets)
        {
            float scroll = viewport.ScrollX;

            columns.Clear();
            hoveredSlot = null;
            currentSets.Clear();
            currentSets.AddRange(sets);

            viewport.ClearColumns();

            foreach (int set in sets)
            {
                if (viewport.Strip.Count > 0)
                {
                    viewport.Strip.Add(new Box
                    {
                        Width = SEPARATOR_WIDTH,
                        Height = TREE_HEIGHT,
                        Colour = colours.Background3,
                    });
                }

                var column = new HitSoundingSampleSetColumn(set, gallery);
                columns.Add(column);
                viewport.Strip.Add(column);
            }

            viewport.ScrollX = scroll;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && IsLoaded)
                samples.Changed -= onLibraryChanged;

            base.Dispose(isDisposing);
        }

        private partial class SampleTreeButton : OsuButton, IHasTooltip
        {
            public LocalisableString TooltipText { get; set; }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                BackgroundColour = colours.Background3;
                SpriteText.Font = OsuFont.Default.With(size: 14);
                SpriteText.Colour = colours.Content1;
            }
        }

        /// <summary>
        /// Clips the set columns and pans them vertically with the wheel, or horizontally with Shift + wheel.
        /// </summary>
        private partial class TreeViewport : CompositeDrawable
        {
            public readonly FillFlowContainer Strip;

            public TreeViewport()
            {
                Name = "Sample sets tree";

                RelativeSizeAxes = Axes.Both;
                Masking = true;

                InternalChild = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = Strip = new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(COLUMN_SPACING),
                    },
                };
            }

            public float ScrollX { get; set; }

            public float ScrollY { get; set; }

            public float MaxScrollX => Math.Max(0, Strip.DrawWidth - DrawWidth);

            public float MaxScrollY => Math.Max(0, Strip.DrawHeight - DrawHeight);

            public float ContentWidth => Strip.DrawWidth;

            public void ClearColumns() => Strip.Clear();

            public void ScrollToRatio(float ratio)
            {
                if (MaxScrollX <= 0)
                    return;

                ScrollX = Math.Clamp(ratio, 0, 1) * MaxScrollX;
            }

            protected override void Update()
            {
                base.Update();

                ScrollX = Math.Clamp(ScrollX, 0, MaxScrollX);
                ScrollY = Math.Clamp(ScrollY, 0, MaxScrollY);

                Strip.Position = new Vector2(-ScrollX, -ScrollY);
            }

            protected override bool OnScroll(ScrollEvent e)
            {
                bool horizontal = e.ShiftPressed || e.ScrollDelta.X != 0;
                float delta = -(e.ScrollDelta.X != 0 ? e.ScrollDelta.X : e.ScrollDelta.Y) * 50;

                if (horizontal)
                    ScrollX = Math.Clamp(ScrollX + delta, 0, MaxScrollX);
                else
                    ScrollY = Math.Clamp(ScrollY + delta, 0, MaxScrollY);

                return true;
            }
        }

        /// <summary>
        /// The translucent horizontal scrollbar overlaid on the bottom edge of the tree viewport.
        /// </summary>
        private partial class SetsScrollbar : CompositeDrawable
        {
            public Action<float>? ScrollToRatio { get; set; }

            private readonly Box track;
            private readonly Container thumb;
            private readonly Box thumbFill;

            public SetsScrollbar()
            {
                Name = "Sample sets scrollbar";

                RelativeSizeAxes = Axes.X;
                Height = SCROLLBAR_HEIGHT;
                Masking = true;
                CornerRadius = SCROLLBAR_HEIGHT / 2;

                InternalChildren = new Drawable[]
                {
                    track = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0.2f },
                    thumb = new Container
                    {
                        Height = SCROLLBAR_HEIGHT,
                        Masking = true,
                        CornerRadius = SCROLLBAR_HEIGHT / 2,
                        Child = thumbFill = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0.6f },
                    },
                };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                track.Colour = colours.Background6;
                thumbFill.Colour = colours.Background1;
            }

            public void UpdateState(float scrollX, float maxScrollX, float viewportWidth, float contentWidth)
            {
                if (maxScrollX <= 0 || contentWidth <= 0 || DrawWidth <= 0)
                {
                    Hide();
                    return;
                }

                Show();

                float thumbWidth = Math.Clamp(DrawWidth * (viewportWidth / contentWidth), 24, DrawWidth);
                thumb.Width = thumbWidth;
                thumb.X = (DrawWidth - thumbWidth) * Math.Clamp(scrollX / maxScrollX, 0, 1);
            }

            protected override bool OnMouseDown(MouseDownEvent e)
            {
                dragTo(e.MousePosition.X);
                return true;
            }

            protected override bool OnDragStart(DragStartEvent e) => true;

            protected override void OnDrag(DragEvent e) => dragTo(e.MousePosition.X);

            private void dragTo(float localX) => ScrollToRatio?.Invoke(Math.Clamp(localX / Math.Max(1, DrawWidth), 0, 1));
        }
    }
}
