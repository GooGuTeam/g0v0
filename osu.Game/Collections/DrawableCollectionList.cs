// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Database;
using osu.Game.Graphics.Containers;
using osuTK;

namespace osu.Game.Collections
{
    /// <summary>
    /// Visualises a list of <see cref="BeatmapCollection"/>s.
    /// </summary>
    public partial class DrawableCollectionList : OsuRearrangeableListContainer<Live<BeatmapCollection>>
    {
        public new MarginPadding Padding
        {
            get => base.Padding;
            set => base.Padding = value;
        }

        protected override ScrollContainer<Drawable> CreateScrollContainer() => scroll = new Scroll();

        [Resolved]
        private IBeatmapCollectionStore collectionStore { get; set; } = null!;

        private Scroll scroll = null!;

        private IDisposable? storeSubscription;

        private Flow flow = null!;

        public IEnumerable<Drawable> OrderedItems => flow.FlowingChildren;

        public string SearchTerm
        {
            get => flow.SearchTerm;
            set => flow.SearchTerm = value;
        }

        protected override FillFlowContainer<RearrangeableListItem<Live<BeatmapCollection>>> CreateListFillFlowContainer() => flow = new Flow
        {
            DragActive = { BindTarget = DragActive }
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            storeSubscription = collectionStore.Subscribe(collectionsChanged);
        }

        /// <summary>
        /// When non-null, signifies that a new collection was created and should be presented to the user.
        /// </summary>
        private Guid? lastCreated;

        protected override void OnItemsChanged()
        {
            base.OnItemsChanged();

            if (lastCreated != null)
            {
                var createdItem = flow.Children.SingleOrDefault(item => item.Model.Value.ID == lastCreated);

                if (createdItem != null)
                {
                    ScheduleAfterChildren(() => scroll.ScrollIntoView(createdItem));
                }

                lastCreated = null;
            }
        }

        private void collectionsChanged()
        {
            // The store callback may run off the update thread and only signals that the data
            // changed, so fetch a detached snapshot (safe on any thread) and rebuild on the
            // update thread.
            var collections = collectionStore.GetAllDetached().OrderBy(c => c.Name).ToList();

            Schedule(() =>
            {
                // The store contract is an invalidation hint only, so a full snapshot replace is
                // used rather than incremental updates. Preserve scroll-to-created behaviour by
                // detecting a single newly added collection.
                var previousIds = Items.Select(i => i.ID).ToHashSet();
                var added = collections.Where(c => !previousIds.Contains(c.ID)).ToList();

                if (added.Count == 1)
                    lastCreated = added[0].ID;

                Items.Clear();
                Items.AddRange(collections.Select(c => c.ToLiveUnmanaged()));
            });
        }

        protected override OsuRearrangeableListItem<Live<BeatmapCollection>> CreateOsuDrawable(Live<BeatmapCollection> item) =>
            new DrawableCollectionListItem(item, true);

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            storeSubscription?.Dispose();
        }

        /// <summary>
        /// The scroll container for this <see cref="DrawableCollectionList"/>.
        /// Contains the main flow of <see cref="DrawableCollectionListItem"/> and attaches a placeholder item to the end of the list.
        /// </summary>
        private partial class Scroll : OsuScrollContainer
        {
            protected override Container<Drawable> Content => content;
            private readonly FillFlowContainer content;

            public Scroll()
            {
                ScrollbarOverlapsContent = false;

                base.Content.Add(content = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    LayoutDuration = 200,
                    LayoutEasing = Easing.OutQuint,
                });
            }
        }

        /// <summary>
        /// The flow of <see cref="DrawableCollectionListItem"/>. Disables layout easing unless a drag is in progress.
        /// </summary>
        private partial class Flow : SearchContainer<RearrangeableListItem<Live<BeatmapCollection>>>
        {
            public readonly IBindable<bool> DragActive = new Bindable<bool>();

            public Flow()
            {
                Spacing = new Vector2(0, 5);
                LayoutEasing = Easing.OutQuint;

                Padding = new MarginPadding { Right = 5 };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                DragActive.BindValueChanged(active => LayoutDuration = active.NewValue ? 200 : 0);
            }
        }
    }
}
