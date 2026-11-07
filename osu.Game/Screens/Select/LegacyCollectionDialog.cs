// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// osu!stable's collection manager (<c>CollectionDialog</c> with its <c>CollectionSprites</c> rows), opened from the beatmap options.
    /// A name box creates collections; each row shows a collection with its size, bordered green when it holds the current beatmap,
    /// with Rename, +Set, +, -Set and - buttons. Clicking a row selects it for "Delete Collection".
    /// </summary>
    /// <remarks>
    /// Positions are stable's, in its 640x480 space: the name box at (95, 35), rows 17 units apart from y = 60, buttons from 280 units into the row.
    /// </remarks>
    public partial class LegacyCollectionDialog : OsuFocusedOverlayContainer
    {
        private const float element_x = 95;
        private const float row_width = 448;
        private const float row_height = 15;
        private const float row_step = 17;
        private const float list_top = 60;
        private const float list_height = 250;

        // pText sizes: n points give an em of n * 4 / 3 / 1.6 units.
        private static float text(float points) => points * 4 / 3 / 1.6f;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        private BeatmapInfo? beatmap;
        private Guid? selectedCollection;
        private Guid? renamingCollection;

        private FillFlowContainer<CollectionRow> rows = null!;
        private NameBox nameBox = null!;

        public LegacyCollectionDialog()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                // pDialog's backdrop.
                new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black, Alpha = 0.85f },
                new DrawSizePreservingFillContainer
                {
                    TargetDrawSize = new Vector2(640, 480),
                    Child = new TooltipContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Position = new Vector2(2),
                                Text = CollectionsStrings.ManageCollectionsTitle,
                                Font = OsuFont.Inter.With(size: 24),
                            },
                            new Container
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Width = 640,
                                RelativeSizeAxes = Axes.Y,
                                Children = new Drawable[]
                                {
                                    nameBox = new NameBox
                                    {
                                        Position = new Vector2(element_x, 35),
                                        Width = row_width,
                                        Height = 20,
                                        PlaceholderText = CollectionsStrings.CreateNew,
                                        CommitOnFocusLost = false,
                                    },
                                    new OsuScrollContainer
                                    {
                                        Position = new Vector2(0, list_top),
                                        Size = new Vector2(550, list_height),
                                        ScrollbarVisible = false,
                                        Child = rows = new FillFlowContainer<CollectionRow>
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, row_step - row_height),
                                            Padding = new MarginPadding { Left = element_x },
                                        },
                                    },
                                    new LegacyDialogButton(LocalisableString.Interpolate($"1. {CommonStrings.DeleteWithConfirmation}"), Color4.Red, deleteSelected)
                                    {
                                        Anchor = Anchor.TopCentre,
                                        Position = new Vector2(0, 320 + 20),
                                        LabelSize = 31,
                                    },
                                    new LegacyDialogButton(LocalisableString.Interpolate($"2. {WebCommonStrings.ButtonsClose}"), Color4.Gray, Hide)
                                    {
                                        Anchor = Anchor.TopCentre,
                                        Position = new Vector2(0, 370 + 20),
                                        LabelSize = 31,
                                    },
                                },
                            },
                        },
                    },
                },
            };

            nameBox.OnCommit += (box, _) =>
            {
                string name = box.Text.Trim();
                box.Text = string.Empty;

                // stable caps names at 25 characters and ignores duplicates (CollectionManager.AddCollection).
                if (name.Length == 0)
                    return;

                if (name.Length > 25)
                    name = name[..25];

                realm.Write(r =>
                {
                    if (!r.All<BeatmapCollection>().Any(c => c.Name == name))
                        r.Add(new BeatmapCollection(name));
                });

                refresh();
            };
        }

        /// <summary>
        /// Shows the dialog for <paramref name="current"/>, the beatmap the add and remove buttons act on.
        /// </summary>
        public void ShowFor(BeatmapInfo current)
        {
            beatmap = current;
            selectedCollection = null;
            renamingCollection = null;
            refresh();
            Show();
        }

        private void refresh()
        {
            var collections = realm.Run(r => r.All<BeatmapCollection>().OrderBy(c => c.Name).AsEnumerable()
                                                .Select(c => (c.ID, c.Name, Hashes: c.BeatmapMD5Hashes.ToHashSet()))
                                                .ToList());

            rows.Clear();

            foreach (var (id, name, hashes) in collections)
            {
                rows.Add(new CollectionRow(this, id, name, hashes.Count, beatmap != null && hashes.Contains(beatmap.MD5Hash))
                {
                    Selected = id == selectedCollection,
                    Renaming = id == renamingCollection,
                });
            }
        }

        private void select(Guid id)
        {
            selectedCollection = id;
            refresh();
        }

        private void toggleRename(Guid id)
        {
            renamingCollection = renamingCollection == id ? null : id;
            selectedCollection = id;
            refresh();
        }

        private void rename(Guid id, string newName)
        {
            newName = newName.Trim();

            if (newName.Length > 25)
                newName = newName[..25];

            if (newName.Length > 0)
            {
                realm.Write(r =>
                {
                    var collection = r.Find<BeatmapCollection>(id);
                    if (collection != null)
                        collection.Name = newName;
                });
            }

            renamingCollection = null;
            refresh();
        }

        /// <summary>
        /// Stable's <c>performAddDel</c>: adds or removes the current beatmap, or every difficulty of its set.
        /// </summary>
        private void change(Guid id, bool remove, bool wholeSet)
        {
            if (beatmap == null)
                return;

            IEnumerable<string> hashes = wholeSet && beatmap.BeatmapSet != null
                ? beatmap.BeatmapSet.Beatmaps.Select(b => b.MD5Hash)
                : new[] { beatmap.MD5Hash };

            realm.Write(r =>
            {
                var collection = r.Find<BeatmapCollection>(id);
                if (collection == null)
                    return;

                foreach (string hash in hashes)
                {
                    if (remove)
                        collection.BeatmapMD5Hashes.Remove(hash);
                    else if (!collection.BeatmapMD5Hashes.Contains(hash))
                        collection.BeatmapMD5Hashes.Add(hash);
                }
            });

            renamingCollection = null;
            refresh();
        }

        private void deleteSelected()
        {
            if (selectedCollection is not Guid id)
                return;

            var live = realm.Run(r => r.Find<BeatmapCollection>(id)?.ToLive(realm));
            if (live == null)
                return;

            dialogOverlay?.Push(new DeleteCollectionDialog(live, () =>
            {
                realm.Write(r =>
                {
                    var collection = r.Find<BeatmapCollection>(id);
                    if (collection != null)
                        r.Remove(collection);
                });

                selectedCollection = null;
                Schedule(refresh);
            }));
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (!e.Repeat && !nameBox.HasFocus && renamingCollection == null)
            {
                switch (e.Key)
                {
                    case Key.Number1:
                        deleteSelected();
                        return true;

                    case Key.Number2:
                        Hide();
                        return true;
                }
            }

            return base.OnKeyDown(e);
        }

        protected override void PopIn() => this.FadeIn(200);

        protected override void PopOut() => this.FadeOut(200);

        /// <summary>
        /// The new collection name box (stable's pTextBox: white text on black with a thin border).
        /// </summary>
        private partial class NameBox : BasicTextBox
        {
            public NameBox()
            {
                BackgroundUnfocused = Color4.Black;
                BackgroundFocused = Color4.Black;
                BackgroundCommit = Color4.Black;
                Masking = true;
                BorderThickness = 1;
                BorderColour = Color4.White;
            }

            protected override SpriteText CreatePlaceholder() => new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Font = OsuFont.Inter.With(size: text(15)),
                Colour = new Color4(200, 200, 200, 255),
                X = 2,
            };

            protected override Drawable GetDrawableCharacter(char c) => new OsuSpriteText
            {
                Text = c.ToString(),
                Font = OsuFont.Inter.With(size: text(15)),
            };
        }

        private partial class CollectionRow : CompositeDrawable
        {
            private static readonly Color4 colour_present = Color4.White;
            private static readonly Color4 colour_not_present = new Color4(150, 150, 150, 255);
            private static readonly Color4 colour_text = new Color4(200, 200, 200, 255);

            public bool Selected { get; init; }
            public bool Renaming { get; init; }

            private readonly LegacyCollectionDialog dialog;
            private readonly Guid id;
            private readonly string name;
            private readonly int count;
            private readonly bool containsCurrent;

            private OsuSpriteText? title;

            public CollectionRow(LegacyCollectionDialog dialog, Guid id, string name, int count, bool containsCurrent)
            {
                this.dialog = dialog;
                this.id = id;
                this.name = name;
                this.count = count;
                this.containsCurrent = containsCurrent;

                Size = new Vector2(row_width, row_height);
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Drawable titleContent;

                if (Renaming)
                {
                    var box = new NameBox
                    {
                        RelativeSizeAxes = Axes.Both,
                        Text = name,
                        BorderColour = Color4.Yellow,
                        CommitOnFocusLost = true,
                    };
                    box.OnCommit += (b, _) => dialog.rename(id, b.Text);
                    titleContent = box;

                    Schedule(() => GetContainingFocusManager()?.ChangeFocus(box));
                }
                else
                {
                    string shown = name.Length > 40 ? name[..40] + "..." : name;

                    titleContent = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        BorderThickness = 1,
                        BorderColour = containsCurrent ? Color4.YellowGreen : Color4.White,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black },
                            title = new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                X = 2,
                                Text = $"{shown} ({count})",
                                Font = OsuFont.Inter.With(size: text(12), weight: Selected ? FontWeight.Bold : FontWeight.Regular),
                                Colour = colour_text,
                            },
                        },
                    };
                }

                // CollectionSprites.InitializeButtons: rename 35 wide, then +Set 30, + 20, -Set 30, - 20, from 280 units in.
                InternalChildren = new[]
                {
                    titleContent,
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Position = new Vector2(280, 2),
                        Children = new Drawable[]
                        {
                            new RowButton(CommonStrings.Rename, 35, Color4.Orange, Color4.Orange, colour_present, () => dialog.toggleRename(id)) { Margin = new MarginPadding { Right = 10 } },
                            new RowButton(@"+Set", 30, Color4.Green, Color4.YellowGreen, containsCurrent ? colour_not_present : colour_present, () => dialog.change(id, false, true))
                                { Margin = new MarginPadding { Right = 5 } },
                            new RowButton(@"+", 20, Color4.Green, Color4.YellowGreen, containsCurrent ? colour_not_present : colour_present, () => dialog.change(id, false, false))
                                { Margin = new MarginPadding { Right = 5 }, TooltipText = CollectionsStrings.AddSelectedBeatmap },
                            new RowButton(@"-Set", 30, Color4.OrangeRed, Color4.DarkOrange, containsCurrent ? colour_present : colour_not_present, () => dialog.change(id, true, true))
                                { Margin = new MarginPadding { Right = 5 } },
                            new RowButton(@"-", 20, Color4.OrangeRed, Color4.DarkOrange, containsCurrent ? colour_present : colour_not_present, () => dialog.change(id, true, false))
                                { TooltipText = CollectionsStrings.RemoveSelectedBeatmap },
                        },
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                title?.FadeColour(Color4.White);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                title?.FadeColour(colour_text);
                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                if (!Renaming)
                    dialog.select(id);
                return true;
            }
        }

        /// <summary>
        /// A pText button of the collection rows: centred 10pt text on a coloured background with a 2 unit border.
        /// </summary>
        private partial class RowButton : OsuClickableContainer
        {
            public RowButton(LocalisableString label, float width, Color4 background, Color4 border, Color4 textColour, Action action)
            {
                Size = new Vector2(width, 11);
                Action = action;
                Masking = true;
                BorderThickness = 2;
                BorderColour = border;

                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = background },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = label,
                        Font = OsuFont.Inter.With(size: text(10)),
                        Colour = textColour,
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                this.FadeColour(Color4.White.Opacity(0.85f), 100);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                this.FadeColour(Color4.White, 100);
                base.OnHoverLost(e);
            }
        }
    }
}
