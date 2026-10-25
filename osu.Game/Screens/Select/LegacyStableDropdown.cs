// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using FrameworkMenu = osu.Framework.Graphics.UserInterface.Menu;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Resources.Localisation.Web;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// An osu!stable style dropdown for the legacy song select top bar. A rounded black box with a 1px
    /// coloured border, a label and a down chevron, plus a flat black option list that lights up with the
    /// accent colour on hover. All the behaviour (open/close/select/keyboard) comes from the framework's
    /// <see cref="Dropdown{T}"/>; this only overrides the look.
    /// </summary>
    public partial class LegacyStableDropdown<T> : Dropdown<T>
    {
        /// <summary>
        /// stable's "highlight colour" for this dropdown (blue for Group, green for Sort). Defines the
        /// box's border and the hover colour of the options.
        /// </summary>
        public Color4 AccentColour { get; init; } = Color4.White;

        /// <summary>
        /// The hover tint of the options, for when it has to differ from the box border (stable's ranking
        /// dropdown has a cyan box but a pink hover). Defaults to <see cref="AccentColour"/>.
        /// </summary>
        public Color4? HoverColour { get; init; }

        /// <summary>Optional override for how each item's text is built (e.g. "Global Ranking").</summary>
        public Func<T, LocalisableString>? ItemText { get; init; }

        protected override DropdownHeader CreateHeader() => new LegacyDropdownHeader();

        protected override DropdownMenu CreateMenu() => new LegacyDropdownMenu();

        [BackgroundDependencyLoader]
        private void load()
        {
            // AccentColour isn't set yet when CreateHeader()/CreateMenu() run inside the base ctor, so the
            // coloured border and the hover tint have to be applied here (otherwise they stay at the
            // default white).
            Header.BorderColour = AccentColour;
            ((LegacyDropdownMenu)Menu).AccentColour = HoverColour ?? AccentColour;
        }

        protected override LocalisableString GenerateItemText(T item)
        {
            if (ItemText != null)
                return ItemText(item);

            if (item is Enum e)
                return e.GetLocalisableDescription();

            return base.GenerateItemText(item);
        }

        private partial class LegacyDropdownHeader : DropdownHeader
        {
            private readonly TruncatingSpriteText label;

            protected override LocalisableString Label
            {
                get => label.Text;
                set => label.Text = value;
            }

            public LegacyDropdownHeader()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.None;
                Height = 24;

                Masking = true;
                CornerRadius = 5;
                BorderThickness = 2;

                // Semi-transparent so the skinnable decoration above shows through the box (stable's
                // Group/Sort/ranking boxes are tinted, not solid black).
                BackgroundColour = new Color4(0f, 0f, 0f, 0.45f);
                BackgroundColourHover = new Color4(0.16f, 0.16f, 0.16f, 0.6f);

                Foreground.Padding = new MarginPadding { Horizontal = 8, Vertical = 2 };

                Children = new Drawable[]
                {
                    label = new TruncatingSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.X,
                        Padding = new MarginPadding { Right = 16 },
                        Font = LegacyFonts.Get(19, FontWeight.Regular),
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Icon = FontAwesome.Solid.ChevronDown,
                        Size = new Vector2(9),
                    },
                };
            }

            protected override DropdownSearchBar CreateSearchBar() => new LegacyDropdownSearchBar();

            private partial class LegacyDropdownSearchBar : DropdownSearchBar
            {
                protected override void PopIn() => this.FadeIn();

                protected override void PopOut() => this.FadeOut();

                protected override TextBox CreateTextBox() => new BasicTextBox
                {
                    PlaceholderText = HomeStrings.SearchPlaceholder,
                    FontSize = 14,
                };
            }
        }

        private partial class LegacyDropdownMenu : DropdownMenu
        {
            private Color4 accent = Color4.White;

            /// <summary>The hover tint of the options (blue/green/cyan). Also applied to already existing items.</summary>
            public Color4 AccentColour
            {
                get => accent;
                set
                {
                    accent = value;
                    foreach (var item in Children.OfType<DrawableLegacyDropdownMenuItem>())
                        item.HoverColour = value;
                }
            }

            public LegacyDropdownMenu()
            {
                MaskingContainer.CornerRadius = 5;
                Alpha = 0;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                // Medium-dark, mostly transparent black (stable's options are ~black alpha 240/255).
                BackgroundColour = new Color4(0f, 0f, 0f, 0.9f);
            }

            protected override void AnimateOpen() => this.FadeIn(300, Easing.OutQuint);

            protected override void AnimateClose() => this.FadeOut(300, Easing.OutQuint);

            private Vector2? targetSize;

            protected override void UpdateSize(Vector2 newSize)
            {
                if (newSize == targetSize)
                    return;

                targetSize = newSize;
                Width = newSize.X;
                this.ResizeHeightTo(newSize.Y, 300, Easing.OutQuint);
            }

            protected override FrameworkMenu CreateSubMenu() => new BasicMenu(Direction.Vertical);

            protected override DrawableDropdownMenuItem CreateDrawableDropdownMenuItem(MenuItem item) => new DrawableLegacyDropdownMenuItem(item) { HoverColour = accent };

            protected override ScrollContainer<Drawable> CreateScrollContainer(Direction direction) => new OsuScrollContainer(direction);

            private partial class DrawableLegacyDropdownMenuItem : DrawableDropdownMenuItem
            {
                public Color4 HoverColour
                {
                    set => BackgroundColourHover = value;
                }

                private OsuSpriteText content = null!;

                public DrawableLegacyDropdownMenuItem(MenuItem item)
                    : base(item)
                {
                    Foreground.Padding = new MarginPadding { Horizontal = 8, Vertical = 4 };
                    BackgroundColour = new Color4(0f, 0f, 0f, 0f);
                    BackgroundColourSelected = new Color4(1f, 1f, 1f, 0.12f);
                }

                protected override Drawable CreateContent() => content = new OsuSpriteText
                {
                    Font = LegacyFonts.Get(17),
                };

                protected override void UpdateForegroundColour()
                {
                    base.UpdateForegroundColour();
                    // The selected option shows in bold in the open list (as in stable).
                    content.Font = LegacyFonts.Get(17, IsSelected ? FontWeight.Bold : FontWeight.Regular);
                }
            }
        }
    }
}
