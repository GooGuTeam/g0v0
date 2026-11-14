// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// The right half of the sample gallery: the beatmap's sample files as large draggable tiles, filtered by alias
    /// or original filename.
    /// </summary>
    public partial class HitSoundingSampleFileLibrary : CompositeDrawable
    {
        public const float HEADER_HEIGHT = 40;
        public const float SEARCH_HEIGHT = 40;
        public const float ADD_BUTTON_WIDTH = 145;
        public const float TILE_SPACING = 10;

        private readonly HitSoundingSampleGallery gallery;
        private readonly List<HitSoundingSampleFileTile> tiles = new List<HitSoundingSampleFileTile>();

        private OverlayColourProvider colours = null!;
        private OsuScrollContainer scroll = null!;
        private SampleSearchBox search = null!;
        private FillFlowContainer tileFlow = null!;

        private string filter = string.Empty;
        private bool rebuildQueued = true;
        private bool rebuildSuppressed;

        [Resolved]
        private HitSoundingSampleLibrary samples { get; set; } = null!;

        /// <summary>
        /// The file tiles currently displayed, in display order.
        /// </summary>
        public IReadOnlyList<HitSoundingSampleFileTile> Tiles => tiles;

        public HitSoundingSampleFileLibrary(HitSoundingSampleGallery gallery)
        {
            this.gallery = gallery;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            this.colours = colours;

            InternalChild = scroll = new OsuScrollContainer
            {
                Name = "Sample files",
                RelativeSizeAxes = Axes.Both,
                ScrollbarVisible = false,
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 10),
                    Children = new[]
                    {
                        createHeader(),
                        search = new SampleSearchBox { FilterChanged = filterChanged },
                        tileFlow = new FillFlowContainer
                        {
                            Name = "Sample file tiles",
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Full,
                            Spacing = new Vector2(TILE_SPACING),
                        },
                        new OsuSpriteText
                        {
                            Name = "Sample files hint",
                            Text = HitSoundingStrings.FilesHint,
                            Font = OsuFont.Default.With(size: 12),
                            Colour = colours.Light3,
                        },
                    },
                },
            };

            samples.Changed += onLibraryChanged;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            rebuild();
        }

        protected override void Update()
        {
            base.Update();

            if (rebuildQueued)
                rebuild();
        }

        internal void BeginAliasEdit() => rebuildSuppressed = true;

        internal void EndAliasEdit()
        {
            if (!rebuildSuppressed)
                return;

            rebuildSuppressed = false;

            if (rebuildQueued)
                rebuild();
        }

        internal void RefreshDragging()
        {
            foreach (var tile in tiles)
                tile.RefreshDragging();
        }

        private Drawable createHeader() => new Container
        {
            Name = "Sample files header",
            RelativeSizeAxes = Axes.X,
            Height = HEADER_HEIGHT,
            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = HitSoundingStrings.SampleFilesHeader,
                    Font = OsuFont.Default.With(size: 17, weight: FontWeight.Bold),
                    Colour = colours.Light3,
                },
                new SampleHeaderButton
                {
                    Name = "Add sample files",
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Width = ADD_BUTTON_WIDTH,
                    Height = HEADER_HEIGHT,
                    Text = HitSoundingStrings.AddFilesButton,
                    TooltipText = HitSoundingStrings.TooltipAddFiles,
                    Action = gallery.AddSampleFiles,
                },
            },
        };

        private void filterChanged(string value)
        {
            filter = value;
            rebuildQueued = true;
        }

        private void onLibraryChanged()
        {
            // Aliases are not polled per frame, so every change is pushed to the existing tiles before the rebuild that
            // eventually replaces them.
            foreach (var tile in tiles)
                tile.RefreshFromLibrary();

            Schedule(() => rebuildQueued = true);
        }

        private void rebuild()
        {
            if (rebuildSuppressed)
                return;

            rebuildQueued = false;

            double scrollOffset = scroll.Target;
            string query = filter.Trim();

            tiles.Clear();
            tileFlow.Clear();

            foreach (string file in samples.Files.OrderBy(file => file, StringComparer.OrdinalIgnoreCase))
            {
                if (query.Length > 0 && !matches(file, query))
                    continue;

                var tile = new HitSoundingSampleFileTile(file, gallery);
                tiles.Add(tile);
                tileFlow.Add(tile);
            }

            if (tiles.Count == 0)
            {
                tileFlow.Add(new OsuSpriteText
                {
                    Name = "Sample files empty state",
                    Text = samples.Files.Count == 0 ? HitSoundingStrings.NoSampleFiles : HitSoundingStrings.NoMatchingFiles,
                    Font = OsuFont.Default.With(size: 12),
                    Colour = colours.Light3,
                });
            }

            search.SetCount(tiles.Count);
            scroll.ScrollTo(scrollOffset, false);
        }

        private bool matches(string file, string query)
            => samples.GetAlias(file).Contains(query, StringComparison.OrdinalIgnoreCase)
               || Path.GetFileName(file).Contains(query, StringComparison.OrdinalIgnoreCase);

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && IsLoaded)
                samples.Changed -= onLibraryChanged;

            base.Dispose(isDisposing);
        }

        private partial class SampleHeaderButton : OsuButton, IHasTooltip
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
        /// The alias or filename filter field, with the number of matching files shown on the right.
        /// </summary>
        private partial class SampleSearchBox : CompositeDrawable, IHasTooltip
        {
            public Action<string>? FilterChanged { get; set; }

            public LocalisableString TooltipText => HitSoundingStrings.TooltipSearchFiles;

            private OsuTextBox textBox = null!;
            private OsuSpriteText count = null!;

            public SampleSearchBox()
            {
                Name = "Sample file search";

                RelativeSizeAxes = Axes.X;
                Height = SEARCH_HEIGHT;
                Masking = true;
                CornerRadius = 5;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background4.Darken(0.1f) },
                    textBox = new SearchTextBox { Name = "Sample file search input" },
                    count = new OsuSpriteText
                    {
                        Name = "Sample file count",
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Position = new Vector2(-10, 0),
                        Font = OsuFont.Default.With(size: 12),
                        Colour = colours.Light3,
                    },
                };

                textBox.Current.BindValueChanged(value => FilterChanged?.Invoke(value.NewValue), true);
            }

            public void SetCount(int value) => count.Text = value == 1 ? HitSoundingStrings.FileCountSingular(value) : HitSoundingStrings.FilesCount(value);

            protected override void Update()
            {
                base.Update();

                float width = Math.Max(0, DrawWidth - count.DrawWidth - 20);

                if (textBox.Width != width)
                    textBox.Width = width;
            }

            private partial class SearchTextBox : OsuTextBox
            {
                public SearchTextBox()
                {
                    Height = SEARCH_HEIGHT;
                    FontSize = 14;
                    LengthLimit = 128;
                    PlaceholderText = HitSoundingStrings.SearchFilesPlaceholder;

                    // The row owns the visible field background.
                    BackgroundUnfocused = Color4.Transparent;
                    BackgroundFocused = Color4.Transparent;
                }

                [BackgroundDependencyLoader]
                private void load(OverlayColourProvider colours)
                {
                    Placeholder.Colour = colours.Light3;
                }
            }
        }
    }
}
