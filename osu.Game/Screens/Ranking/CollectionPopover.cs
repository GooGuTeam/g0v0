// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Screens.Ranking
{
    public partial class CollectionPopover : OsuPopover
    {
        private readonly BeatmapInfo beatmapInfo;

        [Resolved]
        private IBeatmapCollectionStore collectionStore { get; set; } = null!;

        [Resolved]
        private ManageCollectionsDialog? manageCollectionsDialog { get; set; }

        public CollectionPopover(BeatmapInfo beatmapInfo)
            : base(false)
        {
            this.beatmapInfo = beatmapInfo;

            Body.CornerRadius = 4;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new[]
            {
                new OsuMenu(Direction.Vertical, true)
                {
                    Items = items,
                    MaxHeight = 375,
                },
            };
        }

        protected override void OnFocusLost(FocusLostEvent e)
        {
            base.OnFocusLost(e);
            Hide();
        }

        private OsuMenuItem[] items
        {
            get
            {
                var collectionItems = collectionStore.GetAllDetached()
                                                     .OrderBy(c => c.Name)
                                                     .Select(c => new CollectionToggleMenuItem(collectionStore, c.ToLiveUnmanaged(), beatmapInfo)).Cast<OsuMenuItem>().ToList();

                collectionItems.Add(new OsuMenuItem(CommonStrings.Manage, MenuItemType.Standard, () => manageCollectionsDialog?.Show()));

                return collectionItems.ToArray();
            }
        }
    }
}
