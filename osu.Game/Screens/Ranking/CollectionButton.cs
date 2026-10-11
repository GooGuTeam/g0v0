// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osuTK;
using Realms;

namespace osu.Game.Screens.Ranking
{
    public partial class CollectionButton : GrayButton, IHasPopover
    {
        public bool UseV2Style { get; init; }

        private readonly BeatmapInfo beatmapInfo;
        private readonly Bindable<bool> isInAnyCollection;

        [Resolved]
        private RealmAccess realmAccess { get; set; } = null!;

        private IDisposable? collectionSubscription;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public CollectionButton(BeatmapInfo beatmapInfo)
            : base(FontAwesome.Solid.Book)
        {
            this.beatmapInfo = beatmapInfo;
            isInAnyCollection = new Bindable<bool>(false);

            Size = new Vector2(75, 30);

            TooltipText = CommonStrings.Collections.ToLower();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (UseV2Style)
            {
                Icon.Icon = FontAwesome.Regular.Folder;
                Icon.Size = new Vector2(16);
                Icon.Shear = -Shear;
            }

            Action = this.ShowPopover;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            collectionSubscription = realmAccess.RegisterForNotifications(r => r.All<BeatmapCollection>(), collectionsChanged);

            isInAnyCollection.BindValueChanged(_ => updateState(), true);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            collectionSubscription?.Dispose();
        }

        private void collectionsChanged(IRealmCollection<BeatmapCollection> sender, ChangeSet? changes)
        {
            isInAnyCollection.Value = sender.AsEnumerable().Any(c => c.BeatmapMD5Hashes.Contains(beatmapInfo.MD5Hash));
        }

        private void updateState()
        {
            Background.FadeColour(isInAnyCollection.Value ? colours.Green : UseV2Style ? Colour4.FromHex("#293A35") : colours.Gray4, 500, Easing.InOutExpo);
        }

        public Popover GetPopover() => new CollectionPopover(beatmapInfo);
    }
}
