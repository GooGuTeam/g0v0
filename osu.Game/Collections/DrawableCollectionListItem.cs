// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Collections
{
    /// <summary>
    /// Visualises a <see cref="BeatmapCollection"/> inside a <see cref="DrawableCollectionList"/>.
    /// </summary>
    public partial class DrawableCollectionListItem : OsuRearrangeableListItem<Live<BeatmapCollection>>, IFilterable
    {
        private const float item_height = 45;
        private const float button_width = item_height * 0.75f;

        protected TextBox TextBox => content.TextBox;

        private ItemContent content = null!;

        /// <summary>
        /// Creates a new <see cref="DrawableCollectionListItem"/>.
        /// </summary>
        /// <param name="item">The <see cref="BeatmapCollection"/>.</param>
        /// <param name="isCreated">Whether <paramref name="item"/> currently exists in the backing store
        /// (false marks the placeholder row used to create a new collection).</param>
        public DrawableCollectionListItem(Live<BeatmapCollection> item, bool isCreated)
            : base(item)
        {
            this.isCreated = isCreated;

            // For now we don't support rearranging and always use alphabetical sort.
            // Change this to:
            //
            // ShowDragHandle.Value = isCreated;
            //
            // if we want to support user sorting (but changes will need to be made to the store to persist).
            ShowDragHandle.Value = false;

            Masking = true;

            // This doesn't match the latest design spec (should be 5) but is an in-between that feels right to the eye
            // until we move everything over to Form controls.
            CornerRadius = 10;
            CornerExponent = 2.5f;
        }

        private readonly bool isCreated;

        protected override Drawable CreateContent() => content = new ItemContent(Model, isCreated);

        /// <summary>
        /// The main content of the <see cref="DrawableCollectionListItem"/>.
        /// </summary>
        private partial class ItemContent : CompositeDrawable
        {
            [Resolved]
            private IBeatmapCollectionStore collectionStore { get; set; } = null!;

            private readonly Live<BeatmapCollection> collection;
            private readonly bool isCreated;

            public ItemTextBox TextBox { get; private set; } = null!;

            public ItemContent(Live<BeatmapCollection> collection, bool isCreated)
            {
                this.collection = collection;
                this.isCreated = isCreated;

                RelativeSizeAxes = Axes.X;
                Height = item_height;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChildren = new[]
                {
                    isCreated
                        ? new DeleteButton(collection)
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            IsTextBoxHovered = v => TextBox.ReceivePositionalInputAt(v)
                        }
                        : Empty(),
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Right = isCreated ? button_width : 0 },
                        Children = new Drawable[]
                        {
                            TextBox = new ItemTextBox(collection, isCreated)
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = item_height,
                                CommitOnFocusLost = true,
                            },
                        }
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                // Bind late, as the collection name may change externally while still loading.
                TextBox.Current.Value = collection.PerformRead(c => c.IsValid ? c.Name : string.Empty);
                TextBox.OnCommit += onCommit;
            }

            private void onCommit(TextBox sender, bool newText)
            {
                if (isCreated && collection.Value.Name != TextBox.Current.Value)
                    collectionStore.Update(collection.ID, c => c.Name = TextBox.Current.Value);
            }
        }

        private partial class ItemTextBox : OsuTextBox
        {
            protected override float LeftRightPadding => item_height / 2;

            private const float count_text_size = 12;

            private readonly Live<BeatmapCollection> collection;
            private readonly bool isCreated;

            private OsuSpriteText countText = null!;

            public ItemTextBox(Live<BeatmapCollection> collection, bool isCreated)
            {
                this.collection = collection;
                this.isCreated = isCreated;

                CornerRadius = 10;
                CornerExponent = 2.5f;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                BackgroundUnfocused = colours.GreySeaFoamDarker.Darken(0.5f);
                BackgroundFocused = colours.GreySeaFoam;

                if (isCreated)
                {
                    TextContainer.Height *= (Height - count_text_size) / Height;
                    TextContainer.Margin = new MarginPadding { Bottom = count_text_size };

                    TextContainer.Add(countText = new OsuSpriteText
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.TopLeft,
                        Depth = float.MinValue,
                        Font = OsuFont.Default.With(size: count_text_size, weight: FontWeight.SemiBold),
                        Margin = new MarginPadding { Top = 2, Left = 2 },
                        Colour = colours.Yellow
                    });

                    // it is not required to subscribe to change notifications on this collection for this to stay correct:
                    // `DrawableCollectionList` takes out a subscription on the store and rebuilds list items on any
                    // change (including `BeatmapMD5Hashes`), so this count text is re-created whenever the value changes.
                    countText.Text = CommonStrings.ItemsCount(collection.PerformRead(c => c.BeatmapMD5Hashes.Count));
                }
                else
                {
                    PlaceholderText = CollectionsStrings.CreateNew;
                }
            }
        }

        public partial class DeleteButton : OsuClickableContainer
        {
            public Func<Vector2, bool> IsTextBoxHovered = null!;

            [Resolved]
            private IDialogOverlay? dialogOverlay { get; set; }

            [Resolved]
            private IBeatmapCollectionStore collectionStore { get; set; } = null!;

            private readonly Live<BeatmapCollection> collection;

            private Drawable fadeContainer = null!;
            private Drawable background = null!;

            public DeleteButton(Live<BeatmapCollection> collection)
            {
                this.collection = collection;
                RelativeSizeAxes = Axes.Y;

                Width = button_width + item_height / 2; // add corner radius to cover with fill
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                Child = fadeContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0.1f,
                    Children = new[]
                    {
                        background = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colours.Red
                        },
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.Centre,
                            X = -button_width * 0.6f,
                            Size = new Vector2(10),
                            Icon = FontAwesome.Solid.Trash
                        }
                    }
                };

                Action = () =>
                {
                    if (collection.PerformRead(c => c.BeatmapMD5Hashes.Count) == 0)
                        deleteCollection();
                    else
                        dialogOverlay?.Push(new DeleteCollectionDialog(collection, deleteCollection));
                };
            }

            public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => base.ReceivePositionalInputAt(screenSpacePos) && !IsTextBoxHovered(screenSpacePos);

            protected override bool OnHover(HoverEvent e)
            {
                fadeContainer.FadeTo(1f, 100, Easing.Out);
                return false;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                fadeContainer.FadeTo(0.1f, 100);
            }

            protected override bool OnClick(ClickEvent e)
            {
                background.FlashColour(Color4.White, 150);

                return base.OnClick(e);
            }

            private void deleteCollection() => collectionStore.Delete(collection.ID);
        }

        public IEnumerable<LocalisableString> FilterTerms => Model.PerformRead(m => m.IsValid ? new[] { (LocalisableString)m.Name } : []);

        private bool matchingFilter = true;

        public bool MatchingFilter
        {
            get => matchingFilter;
            set
            {
                matchingFilter = value;

                if (matchingFilter)
                    this.FadeIn(200);
                else
                    Hide();
            }
        }

        public bool FilteringActive { get; set; }
    }
}
