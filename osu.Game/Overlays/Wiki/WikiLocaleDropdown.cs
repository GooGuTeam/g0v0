// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Wiki
{
    public partial class WikiLocaleDropdown : OsuDropdown<Language>
    {
        private const float flag_width = 20;
        private const float flag_height = 15;
        private const float flag_spacing = 8;

        // the spacing below mimics the margins and paddings of osu-web's wiki locales menu.
        private const float menu_padding = 8;
        private const float menu_item_padding_vertical = 3;
        private const float menu_item_padding_horizontal = 5;
        private const float menu_flag_offset = 18;
        private const float menu_flag_spacing = 14;
        private const float menu_text_left_padding = menu_flag_offset + flag_width + menu_flag_spacing;
        private const float menu_horizontal_padding = menu_text_left_padding + (menu_padding + menu_item_padding_horizontal) * 2;

        private WikiLocaleDropdownHeader header = null!;
        private WikiLocaleDropdownMenu menu = null!;
        private readonly List<OsuSpriteText> menuMeasurementTexts = new List<OsuSpriteText>();
        private readonly Container<OsuSpriteText> menuMeasurementContainer;
        private readonly Bindable<Language> dropdownCurrent = new Bindable<Language>();

        private bool suppressCurrentPropagation;

        public readonly Bindable<Language> ExternalCurrent = new Bindable<Language>();

        public WikiLocaleDropdown()
        {
            Current = dropdownCurrent;
            Menu.RelativeSizeAxes = Axes.None;

            ExternalCurrent.BindValueChanged(_ => syncExternalToDropdown(), true);
            dropdownCurrent.BindValueChanged(e =>
            {
                if (suppressCurrentPropagation)
                    return;

                ExternalCurrent.Value = e.NewValue;
            });

            AddInternal(menuMeasurementContainer = new Container<OsuSpriteText>
            {
                Alpha = 0,
                AlwaysPresent = true,
            });
        }

        public void SetAvailableLanguages(Language[] languages)
        {
            suppressCurrentPropagation = true;
            Items = languages;
            syncExternalToDropdown();
            suppressCurrentPropagation = false;
        }

        private void syncExternalToDropdown()
        {
            suppressCurrentPropagation = true;
            dropdownCurrent.Value = ExternalCurrent.Value;
            suppressCurrentPropagation = false;
        }

        protected override DropdownHeader CreateHeader() => header = new WikiLocaleDropdownHeader();

        protected override DropdownMenu CreateMenu() => menu = new WikiLocaleDropdownMenu();

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            float headerWidth = MathF.Ceiling(header.DesiredWidth);
            float menuWidth = MathF.Ceiling(Math.Max(headerWidth, getMenuTextWidth() + menu_horizontal_padding));

            menu.TargetWidth = menuWidth;

            if (Width != headerWidth)
                Width = headerWidth;
        }

        private float getMenuTextWidth()
        {
            LocalisableString[] itemTexts = MenuItems.Select(item => item.Text.Value).ToArray();

            while (menuMeasurementTexts.Count < itemTexts.Length)
            {
                var text = new OsuSpriteText
                {
                    AlwaysPresent = true,
                };

                menuMeasurementTexts.Add(text);
                menuMeasurementContainer.Add(text);
            }

            for (int i = 0; i < menuMeasurementTexts.Count; i++)
                menuMeasurementTexts[i].Text = i < itemTexts.Length ? itemTexts[i] : string.Empty;

            return menuMeasurementTexts.Take(itemTexts.Length).DefaultIfEmpty().Max(text => text?.DrawWidth ?? 0);
        }

        private partial class WikiLocaleDropdownMenu : OsuDropdownMenu
        {
            private float targetWidth;

            public WikiLocaleDropdownMenu()
            {
                Anchor = Anchor.TopRight;
                Origin = Anchor.TopRight;
                CornerRadius = 5;
                CornerExponent = 2.5f;
                MaskingContainer.CornerRadius = 5;
                MaskingContainer.CornerExponent = 2.5f;
            }

            [BackgroundDependencyLoader(true)]
            private void load(OverlayColourProvider overlayColourProvider, OsuColour colours)
            {
                BackgroundColour = overlayColourProvider?.Colour3 ?? colours.Blue3;
                // matching the web menu, hovering a locale inverts the colours rather than just tinting the background.
                HoverColour = overlayColourProvider?.Highlight1 ?? colours.Pink1;
                SelectionColour = BackgroundColour;
                ItemsContainer.Padding = new MarginPadding(menu_padding);
            }

            public float TargetWidth
            {
                get => targetWidth;
                set
                {
                    if (targetWidth == value)
                        return;

                    targetWidth = value;
                    Width = value;
                }
            }

            protected override DrawableDropdownMenuItem CreateDrawableDropdownMenuItem(MenuItem item) => new WikiLocaleDropdownMenuItem(item)
            {
                BackgroundColourHover = HoverColour,
                BackgroundColourSelected = SelectionColour
            };

            protected override void UpdateSize(Vector2 newSize)
            {
                if (TargetWidth > 0)
                    newSize = new Vector2(TargetWidth, newSize.Y);

                base.UpdateSize(newSize);
            }

            private partial class WikiLocaleDropdownMenuItem : DrawableOsuDropdownMenuItem
            {
                private WikiLocaleDropdownMenuItemContent content = null!;

                public WikiLocaleDropdownMenuItem(MenuItem item)
                    : base(item)
                {
                    Foreground.Padding = new MarginPadding { Vertical = menu_item_padding_vertical, Horizontal = menu_item_padding_horizontal };
                }

                protected override Drawable CreateContent()
                {
                    var itemContent = new WikiLocaleDropdownMenuItemContent();
                    if (Item is DropdownMenuItem<Language> dropdownItem)
                        itemContent.CountryCode = LanguageExtensions.GetCountryCode(dropdownItem.Value);
                    return content = itemContent;
                }

                protected override void UpdateForegroundColour()
                {
                    base.UpdateForegroundColour();

                    content.Hovering = IsHovered;
                    // the menu keeps the last hovered item preselected after the cursor leaves, and preselection
                    // is what drives the inverted background.
                    content.Inverted = IsPreSelected;
                }

                private partial class WikiLocaleDropdownMenuItemContent : CompositeDrawable, IHasText
                {
                    private const float chevron_offset = -3;

                    private readonly Container flagContainer;

                    private readonly OsuSpriteText label;
                    private readonly SpriteIcon chevron;

                    public LocalisableString Text
                    {
                        get => label.Text;
                        set => label.Text = value;
                    }

                    public CountryCode CountryCode
                    {
                        set => flagContainer.Child = new DrawableFlag(value) { RelativeSizeAxes = Axes.Both };
                    }

                    public WikiLocaleDropdownMenuItemContent()
                    {
                        RelativeSizeAxes = Axes.X;
                        AutoSizeAxes = Axes.Y;

                        InternalChildren = new Drawable[]
                        {
                            chevron = new SpriteIcon
                            {
                                Icon = FontAwesome.Solid.ChevronRight,
                                Size = new Vector2(8),
                                Alpha = 0,
                                X = chevron_offset,
                                Y = 1,
                                Margin = new MarginPadding { Left = 3, Right = 3 },
                                Origin = Anchor.CentreLeft,
                                Anchor = Anchor.CentreLeft,
                            },
                            flagContainer = new Container
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Size = new Vector2(flag_width, flag_height),
                                X = menu_flag_offset,
                            },
                            label = new TruncatingSpriteText
                            {
                                Padding = new MarginPadding { Left = menu_text_left_padding },
                                Origin = Anchor.CentreLeft,
                                Anchor = Anchor.CentreLeft,
                                RelativeSizeAxes = Axes.X,
                            },
                        };
                    }

                    private bool hovering;
                    private bool inverted;

                    [Resolved]
                    private OverlayColourProvider? overlayColourProvider { get; set; }

                    private Color4 invertedColour => overlayColourProvider?.Background6 ?? Color4.Black;

                    public bool Hovering
                    {
                        get => hovering;
                        set
                        {
                            if (value == hovering)
                                return;

                            hovering = value;

                            if (hovering)
                            {
                                chevron.FadeIn(400, Easing.OutQuint);
                                chevron.MoveToX(0, 400, Easing.OutQuint);
                            }
                            else
                            {
                                chevron.FadeOut(200);
                                chevron.MoveToX(chevron_offset, 200, Easing.In);
                            }
                        }
                    }

                    /// <summary>
                    /// Whether this item is currently sitting on the inverted (light) highlight background.
                    /// </summary>
                    public bool Inverted
                    {
                        get => inverted;
                        set
                        {
                            if (value == inverted)
                                return;

                            inverted = value;

                            label.FadeColour(value ? invertedColour : Color4.White, 200, Easing.OutQuint);
                            chevron.FadeColour(value ? invertedColour : Color4.White, 200, Easing.OutQuint);
                        }
                    }
                }
            }
        }

        private partial class WikiLocaleDropdownHeader : OsuDropdownHeader
        {
            private const float horizontal_padding = 8;
            private const float chevron_spacing = 6;
            private const float chevron_size = 10;
            private const float text_extra_width = 6;

            private readonly Container flagContainer;
            private readonly OsuSpriteText measurementText;

            [Resolved]
            private OverlayColourProvider? overlayColourProvider { get; set; }

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            public float DesiredWidth => Math.Max(WikiHeader.CONTROL_HEIGHT,
                measurementText.DrawWidth + horizontal_padding * 2 + flag_width + flag_spacing + chevron_spacing + chevron_size + text_extra_width);

            private Color4 backgroundColour => overlayColourProvider?.Colour3 ?? colours.Blue3;
            private Color4 hoverColour => backgroundColour.Lighten(0.2f);

            public WikiLocaleDropdownHeader()
            {
                Height = WikiHeader.CONTROL_HEIGHT;
                Margin = new MarginPadding();
                CornerRadius = 5;
                CornerExponent = 2.5f;
                Masking = true;

                Foreground.Padding = new MarginPadding { Horizontal = horizontal_padding };

                Foreground.Add(flagContainer = new Container
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Size = new Vector2(flag_width, flag_height),
                });

                Text.RelativeSizeAxes = Axes.X;
                Text.Margin = new MarginPadding { Left = flag_width + flag_spacing };

                Chevron.Size = new Vector2(chevron_size);
                Chevron.Margin = new MarginPadding { Left = chevron_spacing };

                AddInternal(measurementText = new OsuSpriteText
                {
                    Alpha = 0,
                    AlwaysPresent = true,
                });
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Background.Colour = IsHovered ? hoverColour : backgroundColour;
                Dropdown?.Current.BindValueChanged(language => updateFlag(language.NewValue), true);
            }

            protected override void Update()
            {
                base.Update();

                measurementText.Text = Text.Text;
                measurementText.Font = Text.Font;
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (Enabled.Value)
                    Background.FadeColour(hoverColour, 300, Easing.OutQuint);

                return false;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                Background.FadeColour(backgroundColour, 300, Easing.OutQuint);
            }

            private void updateFlag(Language language)
                => flagContainer.Child = new DrawableFlag(LanguageExtensions.GetCountryCode(language), false)
                {
                    RelativeSizeAxes = Axes.Both,
                };
        }
    }
}
