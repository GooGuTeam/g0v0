// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Settings.Entries;
using osuTK;
using osuTK.Graphics;
using FrameworkMenu = osu.Framework.Graphics.UserInterface.Menu;
using M = osu.Game.Overlays.Settings.Rendering.Stable.StableOptionsMetrics;

namespace osu.Game.Overlays.Settings.Rendering.Stable
{
    /// <summary>
    /// osu!stable's options dropdown (<c>OptionDropdown</c> / <c>pDropdown</c>): "Label:" followed by a flat translucent black box
    /// taking the rest of the row. The open list floats over the following options: stable only extends the scrollable area
    /// (<c>SizeOverflow</c>) and never moves the elements below.
    /// </summary>
    public partial class StableDropdown<T> : StableSettingsItem
        where T : notnull
    {
        private readonly DropdownEntry<T> entry;

        public StableDropdown(DropdownEntry<T> entry)
            : base(entry)
        {
            this.entry = entry;
        }

        private TruncatingSpriteText label = null!;
        private OptionsDropdown dropdown = null!;
        private Container dropdownHolder = null!;
        private Drawable? menuProxy;

        [Resolved(canBeNull: true)]
        private SettingsFlyoutLayer? flyoutLayer { get; set; }

        protected override Drawable CreateContent() => new GridContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            ColumnDimensions = new[]
            {
                new Dimension(GridSizeMode.AutoSize, maxSize: 200),
                new Dimension(GridSizeMode.Absolute, M.DROPDOWN_LABEL_GAP),
                new Dimension(),
            },
            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
            Content = new[]
            {
                new Drawable[]
                {
                    label = new TruncatingSpriteText
                    {
                        // stable's dropdown labels end with a colon ("Resolution:", "Frame limiter:").
                        Text = LocalisableString.Interpolate($"{entry.Caption}:"),
                        Font = M.Font(M.LABEL_FONT_SIZE),
                        MaxWidth = 200,
                    },
                    Empty(),
                    // stable sizes the element by its label alone (OptionDropdown.Size = label size), so the 15 unit box overflows the row,
                    // and so does the open list.
                    dropdownHolder = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Child = dropdown = new OptionsDropdown(entry)
                        {
                            RelativeSizeAxes = Axes.X,
                            Current = entry.Current,
                            ItemSource = entry.ItemSource,
                            AlwaysShowSearchBar = entry.AlwaysShowSearchBar,
                            AllowNonContiguousMatching = entry.AllowNonContiguousMatching,
                        },
                    },
                },
            },
        };

        protected override void Update()
        {
            base.Update();

            // centre the box on the label, so the selected item's text lines up with the label beside it.
            float offset = (label.DrawHeight - dropdown.HeaderHeight) / 2;
            dropdown.Y = offset;

            dropdownHolder.Height = label.DrawHeight;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // draw the list in the panel's flyout layer, in front of the options below it.
            if (flyoutLayer != null)
                flyoutLayer.Add(menuProxy = dropdown.CreateMenuProxy());

            dropdown.MenuStateChanged += state => Schedule(() =>
            {
                if (state == MenuState.Open)
                    bringToFront();
                else
                    restoreDepths();
            });
        }

        private readonly List<Action> depthRestores = new List<Action>();

        /// <summary>
        /// The proxy only draws the list above the following settings; input still follows the hierarchy, where the following rows,
        /// subsections and sections come later and so take the cursor first. While the list is open, this row and each container holding it
        /// move in front of their siblings. Flows lay out by insertion order, not depth, so nothing moves.
        /// </summary>
        private void bringToFront()
        {
            restoreDepths();

            Drawable child = this;

            while (child.Parent != null)
            {
                if (child is SettingsSection section)
                {
                    if (section.FindClosestParent<SettingsPanel.SettingsSectionsContainer>() is SettingsPanel.SettingsSectionsContainer sections)
                        raise(sections, section);
                    break;
                }

                // only flows place later siblings after this row. Subsections are flows whose Content is an inner flow, which this
                // loop reaches on its own.
                if (child.Parent is FlowContainer<Drawable> container and not SettingsSubsection)
                    raise(container, child);

                child = child.Parent;
            }
        }

        private void raise<TChild>(Container<TChild> container, TChild child)
            where TChild : Drawable
        {
            // a container's internal children (outside its Content) keep their depth.
            if (!container.Children.Contains(child))
                return;

            float original = child.Depth;
            float front = container.Children.Min(c => c.Depth) - 1;

            if (front >= original)
                return;

            container.ChangeChildDepth(child, front);
            depthRestores.Add(() =>
            {
                if (container.Children.Contains(child))
                    container.ChangeChildDepth(child, original);
            });
        }

        private void restoreDepths()
        {
            foreach (var restore in depthRestores)
                restore();
            depthRestores.Clear();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (menuProxy != null)
                flyoutLayer?.RemoveFlyout(menuProxy);

            base.Dispose(isDisposing);
        }

        private partial class OptionsDropdown : Dropdown<T>
        {
            private readonly DropdownEntry<T> entry;

            public float HeaderHeight => Header.DrawHeight;

            public Drawable CreateMenuProxy() => Menu.CreateProxy();

            public event Action<MenuState> MenuStateChanged
            {
                add => Menu.StateChanged += value;
                remove => Menu.StateChanged -= value;
            }

            public OptionsDropdown(DropdownEntry<T> entry)
            {
                this.entry = entry;
            }

            protected override void UpdateAfterChildren()
            {
                base.UpdateAfterChildren();

                var viewport = this.FindClosestParent<SettingsPanel.SettingsSectionsContainer>();
                if (viewport == null)
                    return;

                float top = ToLocalSpace(viewport.ToScreenSpace(Vector2.Zero)).Y;
                float bottom = ToLocalSpace(viewport.ToScreenSpace(new Vector2(0, viewport.DrawHeight))).Y;
                if (viewport.FixedHeader != null)
                    top = Math.Max(top, ToLocalSpace(viewport.FixedHeader.ScreenSpaceDrawQuad.BottomLeft).Y);

                float below = Math.Max(0, bottom - HeaderHeight);
                float above = Math.Max(0, -top);
                bool upwards = below < 200 && above > below;
                float availableHeight = Math.Min(200, upwards ? above : below);
                Menu.MaxHeight = availableHeight;
                ((OptionsDropdownMenu)Menu).ConstrainHeight(availableHeight);
                // The framework's flow normally places the menu below the header. Override its final
                // position without changing the settings row's reserved height or moving following rows.
                Menu.Y = upwards ? -Menu.DrawHeight : HeaderHeight;
            }

            protected override LocalisableString GenerateItemText(T item) => entry.ItemLabel != null ? entry.ItemLabel(item) : base.GenerateItemText(item);

            protected override DropdownHeader CreateHeader() => new OptionsDropdownHeader();

            protected override DropdownMenu CreateMenu() => new OptionsDropdownMenu { MaxHeight = 200 };

            private partial class OptionsDropdownHeader : DropdownHeader
            {
                private readonly TruncatingSpriteText label;

                protected override LocalisableString Label
                {
                    get => label.Text;
                    set => label.Text = value;
                }

                public OptionsDropdownHeader()
                {
                    RelativeSizeAxes = Axes.X;
                    AutoSizeAxes = Axes.None;
                    Height = M.DROPDOWN_HEIGHT;

                    Masking = true;
                    CornerRadius = 5;

                    BackgroundColour = M.DROPDOWN_BACKGROUND;
                    BackgroundColourHover = M.DROPDOWN_HIGHLIGHT;

                    Foreground.Padding = new MarginPadding { Left = 3, Right = 6 };

                    Children = new Drawable[]
                    {
                        label = new TruncatingSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            RelativeSizeAxes = Axes.X,
                            Padding = new MarginPadding { Right = 16 },
                            Font = M.Font(M.DROPDOWN_FONT_SIZE),
                            Shadow = true,
                        },
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Icon = FontAwesome.Solid.ChevronDown,
                            Size = new Vector2(M.Text(14) * 0.6f),
                        },
                    };
                }

                protected override DropdownSearchBar CreateSearchBar() => new OptionsDropdownSearchBar();

                private partial class OptionsDropdownSearchBar : DropdownSearchBar
                {
                    protected override void PopIn() => this.FadeIn();

                    protected override void PopOut() => this.FadeOut();

                    protected override TextBox CreateTextBox() => new OptionsDropdownSearchTextBox
                    {
                        PlaceholderText = @"type to search",
                    };

                    /// <summary>
                    /// Stable's dropdown has no search field. Lazer's filter is kept, drawn as part of the open list:
                    /// the list's (0, 0, 0, 240) background with the header's white 13pt text.
                    /// </summary>
                    private partial class OptionsDropdownSearchTextBox : BasicTextBox
                    {
                        public OptionsDropdownSearchTextBox()
                        {
                            BackgroundFocused = M.DROPDOWN_OPTION_BACKGROUND;
                            BackgroundUnfocused = M.DROPDOWN_OPTION_BACKGROUND;
                            BackgroundCommit = M.DROPDOWN_OPTION_BACKGROUND;
                            CornerRadius = 5;
                            TextContainer.Height = 0.8f;
                        }

                        protected override SpriteText CreatePlaceholder() => new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = M.Font(M.DROPDOWN_FONT_SIZE),
                            Colour = Color4.White.Opacity(0.5f),
                            X = 3,
                        };

                        protected override Drawable GetDrawableCharacter(char c) => new OsuSpriteText
                        {
                            Text = c.ToString(),
                            Font = M.Font(M.DROPDOWN_FONT_SIZE),
                        };
                    }
                }
            }

            private partial class OptionsDropdownMenu : DropdownMenu
            {
                private Vector2? targetSize;

                public OptionsDropdownMenu()
                {
                    MaskingContainer.CornerRadius = 3;
                    BackgroundColour = M.DROPDOWN_OPTION_BACKGROUND;
                    Alpha = 0;
                }

                protected override void AnimateOpen() => this.FadeIn(200, Easing.OutQuint);

                protected override void AnimateClose() => this.FadeOut(200, Easing.OutQuint);

                public void ConstrainHeight(float maximum)
                {
                    if (Height <= maximum && (targetSize == null || targetSize.Value.Y <= maximum))
                        return;

                    // Cancel growth towards an obsolete viewport size as well as snapping existing overflow.
                    this.ResizeHeightTo(Math.Min(Height, maximum));
                    targetSize = null;
                }

                protected override void UpdateSize(Vector2 newSize)
                {
                    if (newSize == targetSize)
                        return;

                    targetSize = newSize;
                    Width = newSize.X;
                    this.ResizeHeightTo(newSize.Y, 200, Easing.OutQuint);
                }

                protected override FrameworkMenu CreateSubMenu() => new BasicMenu(Direction.Vertical);

                protected override DrawableDropdownMenuItem CreateDrawableDropdownMenuItem(MenuItem item) => new OptionsDropdownMenuItem(item);

                protected override ScrollContainer<Drawable> CreateScrollContainer(Direction direction) => new OsuScrollContainer(direction);

                private partial class OptionsDropdownMenuItem : DrawableDropdownMenuItem
                {
                    private OsuSpriteText content = null!;

                    public OptionsDropdownMenuItem(MenuItem item)
                        : base(item)
                    {
                        Foreground.Padding = new MarginPadding { Horizontal = 12 };
                        Foreground.AutoSizeAxes = Axes.None;
                        Foreground.RelativeSizeAxes = Axes.Both;
                        BackgroundColour = Color4.Transparent;
                        BackgroundColourHover = M.DROPDOWN_HIGHLIGHT;
                        BackgroundColourSelected = Color4.White.Opacity(0.12f);
                    }

                    public override void SetFlowDirection(Direction direction)
                    {
                        // stable's options are fixed 17 unit rows (pDropdown.AddOption). A fixed height keeps every row in place,
                        // instead of deriving it from the foreground, whose size changes when the selected option turns bold.
                        RelativeSizeAxes = Axes.X;
                        AutoSizeAxes = Axes.None;
                        Height = M.DROPDOWN_OPTION_HEIGHT;
                    }

                    protected override Drawable CreateContent() => content = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Font = M.Font(M.DROPDOWN_OPTION_FONT_SIZE),
                    };

                    protected override void UpdateForegroundColour()
                    {
                        base.UpdateForegroundColour();
                        content.Font = M.Font(M.DROPDOWN_OPTION_FONT_SIZE, IsSelected ? FontWeight.Bold : FontWeight.Regular);
                    }
                }
            }
        }
    }
}
