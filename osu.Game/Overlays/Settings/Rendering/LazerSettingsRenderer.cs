// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Numerics;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osuTK.Graphics;
using Vector2 = osuTK.Vector2;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings.Entries;
using osu.Game.Overlays.Settings.Sections.Input;

namespace osu.Game.Overlays.Settings.Rendering
{
    /// <summary>
    /// Renders settings entries with the lazer form controls (<see cref="SettingsItemV2"/> + <c>Form*</c> controls).
    /// </summary>
    public partial class LazerSettingsRenderer : ISettingsRenderer
    {
        public static readonly LazerSettingsRenderer DEFAULT = new LazerSettingsRenderer();

        public const int SUBSECTION_HEADER_HEIGHT = 43;
        public const int SUBSECTION_HEADER_FONT_SIZE = 20;

        public Drawable CreateCheckbox(CheckboxEntry entry) => createItem(entry, new FormCheckBox
        {
            Caption = entry.Caption,
            HintText = entry.HintText,
            Current = entry.Current,
        });

        public Drawable CreateSlider<T>(SliderEntry<T> entry)
            where T : struct, INumber<T>, IMinMaxValue<T>
        {
            var slider = new FormSliderBar<T>
            {
                Caption = entry.Caption,
                HintText = entry.HintText,
                Current = entry.Current,
                DisplayAsPercentage = entry.DisplayAsPercentage,
                PlaySamplesOnAdjust = entry.PlaySamplesOnAdjust,
                // FormSliderBar falls back to its label format when no tooltip format is given; FormatTooltip does the same.
                TooltipFormat = entry.FormatTooltip,
            };

            entry.TransferValueOnCommitBindable.BindValueChanged(v => slider.TransferValueOnCommit = v.NewValue, true);

            if (entry.KeyboardStep != null)
                slider.KeyboardStep = entry.KeyboardStep.Value;

            if (entry.LabelFormat != null)
                slider.LabelFormat = entry.LabelFormat;

            return createItem(entry, slider);
        }

        public Drawable CreateDropdown<T>(DropdownEntry<T> entry)
            where T : notnull
        {
            var dropdown = new EntryFormDropdown<T>(entry)
            {
                Caption = entry.Caption,
                Current = entry.Current,
                ItemSource = entry.ItemSource,
                AlwaysShowSearchBar = entry.AlwaysShowSearchBar,
                AllowNonContiguousMatching = entry.AllowNonContiguousMatching,
            };

            entry.HintTextBindable.BindValueChanged(h => dropdown.HintText = h.NewValue, true);

            return createItem(entry, dropdown);
        }

        public Drawable CreateEnumDropdown<T>(EnumDropdownEntry<T> entry)
            where T : struct, Enum
            => CreateDropdown(entry);

        public Drawable CreateTextBox(TextBoxEntry entry)
        {
            var textBox = new FormTextBox
            {
                Caption = entry.Caption,
                HintText = entry.HintText,
                PlaceholderText = entry.PlaceholderText,
                LengthLimit = entry.LengthLimit,
                Current = entry.Current,
            };
            if (entry.Commit != null)
            {
                textBox.OnCommit += (_, _) => entry.Commit();
            }
            return createItem(entry, textBox);
        }

        public bool UseStableLayouts => false;

        public Drawable CreateKeyBindingResetButton(ButtonEntry entry)
        {
            var button = new ResetButton { Action = () => entry.Action?.Invoke() };
            button.Enabled.BindTo(entry.Enabled);
            return button;
        }

        public Drawable CreateNote(NoteEntry entry)
        {
            var note = new SettingsNote
            {
                RelativeSizeAxes = Axes.X,
                // the note has its own top spacing; cancel it to sit right below the item it describes.
                Margin = new MarginPadding { Top = entry.AttachedToPrevious ? -SettingsSection.ITEM_SPACING_V2 : 0 },
            };
            note.Current.BindTo(entry.Note);

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = SettingsPanel.CONTENT_PADDING,
                Child = note,
            };
        }

        public Drawable CreateButton(ButtonEntry entry)
        {
            SettingsButtonV2 button = entry.Popover != null
                ? new PopoverSettingsButton(entry.Popover)
                : entry.Dangerous ? new DangerousSettingsButtonV2() : new SettingsButtonV2();

            button.Text = entry.Caption;
            button.TooltipText = entry.HintText;
            button.Keywords = entry.Keywords;
            button.Action = entry.Popover != null ? ((PopoverSettingsButton)button).ShowPopover : () => entry.Action?.Invoke();
            button.Enabled.BindTo(entry.Enabled);
            button.CanBeShown.BindTo(entry.CanBeShown);

            if (entry.Large)
                button.Height = 60;

            if (entry.BackgroundColour != null)
                button.BackgroundColour = entry.BackgroundColour.Value;

            return button;
        }

        public Drawable CreateSubsectionHeader(LocalisableString header) => new OsuSpriteText
        {
            Text = header,
            Font = OsuFont.GetFont(size: SUBSECTION_HEADER_FONT_SIZE),
            Margin = new MarginPadding { Vertical = SettingsSubsection.VERTICAL_PADDING, Horizontal = 5 },
            Padding = SettingsPanel.CONTENT_PADDING,
        };

        public ISettingsSectionFrame CreateSectionFrame(LocalisableString header, Drawable content) => new SectionFrame(header, content);

        public Drawable CreatePanelBackground() => new ColourProviderBox(c => c.Background4)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Scale = new Vector2(2, 1), // over-extend to the left for transitions
            RelativeSizeAxes = Axes.Both,
        };

        public Drawable CreatePanelHeader(LocalisableString title, LocalisableString description) => new SettingsHeader(title, description);

        public Drawable CreatePinnedHeaderBackground() => new ColourProviderBox(c => c.Background5)
        {
            RelativeSizeAxes = Axes.Both,
        };

        public Drawable CreateSearchBar(SettingsSearchTextBox textBox)
        {
            textBox.RelativeSizeAxes = Axes.X;
            textBox.Anchor = Anchor.TopCentre;
            textBox.Origin = Anchor.TopCentre;

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding
                {
                    Vertical = 6,
                    Left = SettingsPanel.CONTENT_PADDING.Left,
                    Right = SettingsPanel.CONTENT_PADDING.Right,
                },
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Child = textBox,
            };
        }

        public SettingsSidebar CreateSidebar(bool showBackButton) => new SettingsSidebar(showBackButton);

        // lazer only shows a back button in sub-panels' sidebars.
        public Drawable? CreateRootBackButton(Action action) => null;

        public ISettingsSidebarButton? CreateSidebarButton(SettingsSection section, Action action) => new SidebarIconButton
        {
            Section = section,
            Action = action,
        };

        private static SettingsItemV2 createItem(SettingsEntry entry, IFormControl control)
        {
            var item = new SettingsItemV2(control)
            {
                Keywords = entry.Keywords,
                ShowRevertToDefaultButton = entry.ShowRevertToDefaultButton,
            };

            if (entry.HasClassicDefault)
                item.ApplyClassicDefault = _ => entry.ApplyClassicDefault();

            item.Note.BindTo(entry.Note);
            item.CanBeShown.BindTo(entry.CanBeShown);

            return item;
        }

        /// <summary>
        /// A <see cref="FormDropdown{T}"/> which uses <see cref="DropdownEntry{T}.ItemLabel"/> when provided.
        /// </summary>
        private partial class PopoverSettingsButton : SettingsButtonV2, IHasPopover
        {
            private readonly Func<Popover> createPopover;

            public PopoverSettingsButton(Func<Popover> createPopover)
            {
                this.createPopover = createPopover;
            }

            public Popover GetPopover() => createPopover();
        }

        private partial class EntryFormDropdown<T> : FormDropdown<T>
            where T : notnull
        {
            private readonly DropdownEntry<T> entry;

            public EntryFormDropdown(DropdownEntry<T> entry)
            {
                this.entry = entry;
            }

            protected override LocalisableString GenerateItemText(T item)
                => entry.ItemLabel != null ? entry.ItemLabel(item) : base.GenerateItemText(item);
        }

        /// <summary>
        /// A box coloured from the <see cref="OverlayColourProvider"/>.
        /// </summary>
        private partial class ColourProviderBox : Box
        {
            private readonly Func<OverlayColourProvider, Color4> colour;

            public ColourProviderBox(Func<OverlayColourProvider, Color4> colour)
            {
                this.colour = colour;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                Colour = colour(colourProvider);
            }
        }

        /// <summary>
        /// The lazer section frame: a separator, the MapleMono header, and a dim over sections which are not current.
        /// </summary>
        private partial class SectionFrame : CompositeDrawable, ISettingsSectionFrame
        {
            private const int header_size = 30;
            private const int border_size = 2;
            private const float inactive_alpha = 0.8f;

            private readonly LocalisableString header;
            private readonly Drawable content;

            private Box dim = null!;

            public bool RequiresSelectionForInput => true;

            public SectionFrame(LocalisableString header, Drawable content)
            {
                this.header = header;
                this.content = content;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                content.Margin = new MarginPadding { Top = header_size + 6 };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        Name = "separator",
                        Colour = colourProvider.Background6,
                        RelativeSizeAxes = Axes.X,
                        Height = border_size,
                    },
                    new Container
                    {
                        Padding = new MarginPadding { Top = border_size },
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Children = new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Padding = new MarginPadding
                                {
                                    Top = 24,
                                    Bottom = 40,
                                },
                                Children = new[]
                                {
                                    new OsuSpriteText
                                    {
                                        Font = OsuFont.MapleMono.With(size: header_size),
                                        Text = header,
                                        Margin = SettingsPanel.CONTENT_PADDING,
                                    },
                                    content
                                }
                            },
                            dim = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = colourProvider.Background5,
                                Alpha = inactive_alpha,
                            },
                        }
                    },
                };
            }

            public void UpdateState(bool isCurrent, bool isHovered)
            {
                float dimFade = 0;

                if (!isCurrent)
                    dimFade = isHovered ? 0.5f : inactive_alpha;

                dim.FadeTo(dimFade, 300, Easing.OutQuint);
            }
        }
    }
}
