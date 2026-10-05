// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osuTK;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// A dropdown to select the collection to be used to filter results.
    /// </summary>
    public partial class CollectionDropdown : ShearedDropdown<CollectionFilterMenuItem> // TODO: partial class under FilterControl?
    {
        /// <summary>
        /// Whether to show the "manage collections..." menu item in the dropdown.
        /// </summary>
        protected virtual bool ShowManageCollectionsItem => true;

        private readonly BindableList<CollectionFilterMenuItem> filters = new BindableList<CollectionFilterMenuItem>();
        private readonly Bindable<string> configCollectionFilter = new Bindable<string>();

        [Resolved]
        private ManageCollectionsDialog? manageCollectionsDialog { get; set; }

        [Resolved]
        private IBeatmapCollectionStore collectionStore { get; set; } = null!;

        private IDisposable? storeSubscription;

        private readonly CollectionFilterMenuItem allBeatmapsItem = new AllBeatmapsCollectionFilterMenuItem();

        /// <summary>
        /// The id of the collection the user last actually selected, or null for "all beatmaps".
        /// This is the authoritative selection: the framework clears <see cref="Dropdown{T}.Current"/>
        /// to the first menu item whenever the menu is opened, so neither it nor the persisted filter
        /// (which is written back from selection changes) can be trusted to reflect the selection
        /// while the menu is open. Written back to the persisted filter for cross-session restore.
        /// </summary>
        private Guid? selectedCollectionId;

        public CollectionDropdown()
            : base(CollectionsStrings.Collection)
        {
            ItemSource = filters;

            Current.Value = allBeatmapsItem;
            AlwaysShowSearchBar = true;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager configManager)
        {
            configManager.BindWith(OsuSetting.SongSelectCollectionFilter, configCollectionFilter);

            if (Guid.TryParse(configCollectionFilter.Value, out var persistedId))
                selectedCollectionId = persistedId;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            storeSubscription = collectionStore.Subscribe(collectionsChanged);

            Current.BindValueChanged(selectionChanged);
        }

        private void collectionsChanged()
        {
            // The store callback may run off the update thread and only signals that the data
            // changed, so fetch a detached snapshot (safe on any thread) and rebuild on the
            // update thread.
            var collections = collectionStore.GetAllDetached().OrderBy(c => c.Name).ToList();

            Schedule(() => rebuild(collections));
        }

        private void rebuild(List<BeatmapCollection> collections)
        {
            // The store contract is an invalidation hint only, so a full snapshot replace is used
            // rather than incremental updates.
            filters.Clear();
            filters.Add(allBeatmapsItem);
            filters.AddRange(collections.Select(c => new CollectionFilterMenuItem(c.ToLiveUnmanaged())));
            if (ShowManageCollectionsItem)
                filters.Add(new ManageCollectionsFilterMenuItem());

            // Restore the selection from the authoritative id (the framework's Current is not
            // reliable while the menu is open). Re-created items compare equal to the previous
            // selection (equality is by collection id), so assigning one may not raise a change
            // notification and the dropdown header can display stale text. Force a header refresh
            // by clearing and re-selecting when a collection is selected.
            var restored = filters.SingleOrDefault(item => item.Collection != null && item.Collection.ID == selectedCollectionId) ?? allBeatmapsItem;

            if (restored.Collection != null)
            {
                Current.Value = allBeatmapsItem;
                Schedule(() =>
                {
                    // Current may have changed before the scheduled call is run.
                    if (Current.Value == allBeatmapsItem)
                        Current.Value = restored;
                });
            }
            else
            {
                Current.Value = restored;
            }
        }

        private void selectionChanged(ValueChangedEvent<CollectionFilterMenuItem> filter)
        {
            // May be null during .Clear().
            if (filter.NewValue.IsNull())
                return;

            switch (filter.NewValue)
            {
                case ManageCollectionsFilterMenuItem:
                    // Never select the manage collection filter - rollback to the previous filter.
                    // This is done after the above since it is important that bindable is unbound from OldValue, which is lost after forcing it back to the old value.
                    Current.Value = filter.OldValue;
                    manageCollectionsDialog?.Show();
                    break;

                case CollectionFilterMenuItem collectionMenuItem when collectionMenuItem.Collection != null:
                    // Ignore framework-driven resets: opening the menu clears the selection to the
                    // first item, which is not a user choice. Only record selections made while the
                    // menu is closed (an actual user pick closes the menu first).
                    if (Menu.State != MenuState.Open)
                    {
                        selectedCollectionId = collectionMenuItem.Collection.ID;
                        configCollectionFilter.Value = collectionMenuItem.Collection.ID.ToString();
                    }

                    break;

                case AllBeatmapsCollectionFilterMenuItem:
                    // See above: don't let the menu-open reset clear the authoritative selection.
                    if (Menu.State != MenuState.Open)
                    {
                        selectedCollectionId = null;
                        configCollectionFilter.Value = string.Empty;
                    }

                    break;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            storeSubscription?.Dispose();
        }

        protected override LocalisableString GenerateItemText(CollectionFilterMenuItem item) => item.CollectionName;

        protected sealed override DropdownMenu CreateMenu() => CreateCollectionMenu();

        protected virtual ShearedCollectionDropdownMenu CreateCollectionMenu() => new ShearedCollectionDropdownMenu();

        protected partial class ShearedCollectionDropdownMenu : ShearedDropdownMenu
        {
            public ShearedCollectionDropdownMenu()
            {
                MaxHeight = 200;
            }

            protected override DrawableDropdownMenuItem CreateDrawableDropdownMenuItem(MenuItem item) => new DrawableCollectionMenuItem(item)
            {
                BackgroundColourHover = HoverColour,
                BackgroundColourSelected = SelectionColour
            };
        }

        protected partial class DrawableCollectionMenuItem : ShearedDropdownMenu.ShearedMenuItem
        {
            private IconButton addOrRemoveButton = null!;

            private bool beatmapInCollection;

            private readonly Live<BeatmapCollection>? collection;

            [Resolved]
            private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

            public DrawableCollectionMenuItem(MenuItem item)
                : base(item)
            {
                collection = ((DropdownMenuItem<CollectionFilterMenuItem>)item).Value.Collection;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                AddInternal(addOrRemoveButton = new NoFocusChangeIconButton
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Shear = -OsuGame.SHEAR,
                    X = -OsuScrollContainer.SCROLL_BAR_WIDTH,
                    Scale = new Vector2(0.65f),
                    Action = addOrRemove,
                });
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                if (collection != null)
                {
                    beatmap.BindValueChanged(_ =>
                    {
                        beatmapInCollection = collection.PerformRead(c => c.BeatmapMD5Hashes.Contains(beatmap.Value.BeatmapInfo.MD5Hash));

                        addOrRemoveButton.Enabled.Value = !beatmap.IsDefault;
                        addOrRemoveButton.Icon = beatmapInCollection ? FontAwesome.Solid.MinusSquare : FontAwesome.Solid.PlusSquare;
                        addOrRemoveButton.TooltipText = beatmapInCollection ? CollectionsStrings.RemoveSelectedBeatmap : CollectionsStrings.AddSelectedBeatmap;

                        updateButtonVisibility();
                    }, true);
                }

                updateButtonVisibility();
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateButtonVisibility();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateButtonVisibility();
                base.OnHoverLost(e);
            }

            protected override void OnSelectChange()
            {
                base.OnSelectChange();
                updateButtonVisibility();
            }

            private void updateButtonVisibility()
            {
                if (collection == null)
                    addOrRemoveButton.Alpha = 0;
                else
                    addOrRemoveButton.Alpha = IsHovered || IsPreSelected || beatmapInCollection ? 1 : 0;
            }

            [Resolved]
            private IBeatmapCollectionStore collectionStore { get; set; } = null!;

            private void addOrRemove()
            {
                Debug.Assert(collection != null);

                Task.Run(() => collectionStore.Update(collection.ID, c =>
                {
                    if (!c.BeatmapMD5Hashes.Remove(beatmap.Value.BeatmapInfo.MD5Hash))
                        c.BeatmapMD5Hashes.Add(beatmap.Value.BeatmapInfo.MD5Hash);
                }));
            }

            protected override Drawable CreateContent() => (Content)base.CreateContent();

            private partial class NoFocusChangeIconButton : IconButton
            {
                public override bool ChangeFocusOnClick => false;
            }
        }
    }
}
