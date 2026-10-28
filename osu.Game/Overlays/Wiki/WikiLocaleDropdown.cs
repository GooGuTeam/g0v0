// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
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
        private const float menu_text_left_padding = 52;
        private const float menu_horizontal_padding = menu_text_left_padding + 30;

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
                CornerRadius = 10;
                CornerExponent = 2.5f;
                MaskingContainer.CornerRadius = 10;
                MaskingContainer.CornerExponent = 2.5f;
            }

            [BackgroundDependencyLoader(true)]
            private void load(OverlayColourProvider overlayColourProvider, OsuColour colours)
            {
                BackgroundColour = overlayColourProvider?.Colour3 ?? colours.Blue3;
                HoverColour = BackgroundColour.Lighten(0.2f);
                SelectionColour = BackgroundColour;
                ItemsContainer.Padding = new MarginPadding { Vertical = 10, Horizontal = 10 };
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
                    Foreground.Padding = new MarginPadding { Vertical = 4, Horizontal = 2 };
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
                }

                private partial class WikiLocaleDropdownMenuItemContent : CompositeDrawable, IHasText
                {
                    private const float chevron_offset = -3;

                    private readonly Container flagContainer;

                    public readonly OsuSpriteText Label;
                    public readonly SpriteIcon Chevron;

                    public LocalisableString Text
                    {
                        get => Label.Text;
                        set => Label.Text = value;
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
                            Chevron = new SpriteIcon
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
                                X = 23,
                            },
                            Label = new TruncatingSpriteText
                            {
                                Padding = new MarginPadding { Left = menu_text_left_padding },
                                Origin = Anchor.CentreLeft,
                                Anchor = Anchor.CentreLeft,
                                RelativeSizeAxes = Axes.X,
                            },
                        };
                    }

                    private bool hovering;

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
                                Chevron.FadeIn(400, Easing.OutQuint);
                                Chevron.MoveToX(0, 400, Easing.OutQuint);
                            }
                            else
                            {
                                Chevron.FadeOut(200);
                                Chevron.MoveToX(chevron_offset, 200, Easing.In);
                            }
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

            private TrianglesV2 triangles = null!;

            [Resolved]
            private OverlayColourProvider? overlayColourProvider { get; set; }

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            public float DesiredWidth => Math.Max(WikiHeader.CONTROL_HEIGHT,
                measurementText.DrawWidth + horizontal_padding * 2 + flag_width + flag_spacing + chevron_spacing + chevron_size + text_extra_width);

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

                Add(triangles = new TrianglesV2
                {
                    Thickness = 0.02f,
                    SpawnRatio = 0.6f,
                    RelativeSizeAxes = Axes.Both,
                    Depth = float.MaxValue,
                });

                updateColours();
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
                bool handled = base.OnHover(e);

                if (Enabled.Value)
                    Background.FadeColour(getHoverColour(), 300, Easing.OutQuint);

                return handled;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                base.OnHoverLost(e);

                Background.FadeColour(getBackgroundColour(), 300, Easing.OutQuint);
            }

            private void updateColours()
            {
                Color4 backgroundColour = getBackgroundColour();
                Color4 hoverColour = getHoverColour();

                Background.Colour = IsHovered ? hoverColour : backgroundColour;
                triangles.Colour = ColourInfo.GradientVertical(hoverColour, backgroundColour);
            }

            private Color4 getBackgroundColour() => overlayColourProvider?.Colour3 ?? colours.Blue3;

            private Color4 getHoverColour() => getBackgroundColour().Lighten(0.2f);

            private void updateFlag(Language language)
                => flagContainer.Child = new DrawableFlag(LanguageExtensions.GetCountryCode(language), false)
                {
                    RelativeSizeAxes = Axes.Both,
                };
        }
    }
}
