// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Rendering.Stable;
using osuTK;
using osuTK.Graphics;
using CommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;
using M = osu.Game.Overlays.Settings.Rendering.Stable.StableOptionsMetrics;

namespace osu.Game.Overlays.Settings.Sections.Input
{
    /// <summary>
    /// The osu!stable look of a key binding row, used when the settings renderer asks for it. The row is an ordinary stable option element,
    /// laid out like <see cref="StableDropdown{T}"/>: the full-width hover highlight, the section guide line, the yellow "modified" indicator
    /// (click to restore the defaults), the action name as a 12pt label, and each bound key as a dropdown-style box on the right.
    /// </summary>
    public partial class KeyBindingRow
    {
        /// <summary>
        /// Like other stable elements, the row spans the panel width (so its highlight does) and insets its content itself.
        /// </summary>
        public static readonly MarginPadding StablePadding = new MarginPadding();

        private Box stableHover = null!;
        private Box stableSectionLine = null!;
        private StableSettingsItem.DefaultIndicator stableIndicator = null!;
        private TruncatingSpriteText stableCaption = null!;
        private Container stableLine = null!;

        private Drawable createStableContent() => content = new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Children = new Drawable[]
            {
                stableHover = new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Colour = M.HOVER_COLOUR,
                    Alpha = 0,
                    BypassAutoSizeAxes = Axes.Both,
                },
                stableSectionLine = new Box
                {
                    X = M.SECTION_LINE_X,
                    Width = M.SECTION_LINE_WIDTH,
                    Colour = M.SECTION_LINE_COLOUR,
                    Blending = BlendingParameters.Additive,
                    BypassAutoSizeAxes = Axes.Both,
                },
                stableIndicator = new StableSettingsItem.DefaultIndicator
                {
                    X = M.INDICATOR_X,
                    Width = M.INDICATOR_WIDTH,
                    Alpha = 0,
                    Action = RestoreDefaults,
                    BypassAutoSizeAxes = Axes.Both,
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Left = M.ELEMENT_X, Right = StableSettingsItem.CONTENT_RIGHT, Vertical = StableSettingsItem.ROW_PADDING },
                    Spacing = new Vector2(0, M.ELEMENT_GAP / 2),
                    Children = new Drawable[]
                    {
                        // as with stable's dropdown, the label alone sizes the line; the key boxes are centred on it and may overflow it.
                        stableLine = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Children = new Drawable[]
                            {
                                stableCaption = new TruncatingSpriteText
                                {
                                    Text = Action.GetLocalisableDescription(),
                                    Font = M.Font(M.LABEL_FONT_SIZE),
                                },
                                buttons = new FillFlowContainer<KeyButton>
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    AutoSizeAxes = Axes.Both,
                                    Spacing = new Vector2(M.DROPDOWN_LABEL_GAP),
                                    BypassAutoSizeAxes = Axes.Y,
                                },
                            },
                        },
                        cancelAndClearButtons = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Full,
                            Alpha = 0,
                            Children = new Drawable[]
                            {
                                pendingBindingText = new OsuSpriteText
                                {
                                    Alpha = 0.4f,
                                    Text = InputSettingsStrings.PendingBinding,
                                    Font = M.Font(M.LABEL_FONT_SIZE),
                                    Margin = new MarginPadding { Bottom = M.ELEMENT_GAP / 2 },
                                },
                                // stable's option buttons, half width each so they sit side by side.
                                new StableButton.ButtonBody(StableSettingsColours.SECONDARY)
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Width = 0.5f,
                                    Height = M.BUTTON_HEIGHT,
                                    Text = CommonStrings.ButtonsCancel,
                                    Action = () => finalise(false),
                                },
                                new StableButton.ButtonBody(StableSettingsColours.MAIN)
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Width = 0.5f,
                                    Height = M.BUTTON_HEIGHT,
                                    Text = InputSettingsStrings.ClearBindingButton,
                                    Action = clear,
                                },
                            },
                        },
                    },
                },
            },
        };

        private void updateStableLayout()
        {
            float spacing = SettingsSection.ITEM_SPACING_V2;

            // the highlight and indicator cover the element plus half the gap on each side; the guide line runs on through the gap below.
            stableHover.Y = stableIndicator.Y = -spacing / 2;
            stableHover.Height = stableIndicator.Height = content.DrawHeight + spacing;
            stableSectionLine.Height = content.DrawHeight + spacing;

            stableCaption.MaxWidth = Math.Max(0, stableLine.DrawWidth - buttons.DrawWidth - M.DROPDOWN_LABEL_GAP);
        }

        protected override void Update()
        {
            base.Update();

            if (stableStyle)
                updateStableLayout();
        }

        /// <summary>
        /// The row highlights like any stable element while hovered, and stays highlighted while it is being bound.
        /// </summary>
        private void updateStableState()
        {
            stableHover.FadeTo(IsHovered || HasFocus ? M.HOVER_ALPHA : 0, M.HOVER_DURATION, Easing.OutQuint);
        }

        private void updateStableIndicator(bool isDefault) => stableIndicator.FadeTo(isDefault ? 0 : 1, 100);

        public partial class KeyButton
        {
            /// <summary>
            /// Whether this button is drawn as a stable dropdown-style key box. Must be set before load.
            /// </summary>
            public bool StableStyle { get; init; }

            /// <summary>
            /// The stable dropdown's box: 15 units tall on (0, 0, 0, 128) with 5 unit corners and the key in white 13pt text.
            /// It turns the dropdown's highlight colour while hovered, and the main colour while being bound.
            /// </summary>
            private void applyStableStyle()
            {
                CornerRadius = 5;
                CornerExponent = 2;

                minimumWidth.Width = M.BINDING_KEY_MIN_WIDTH;
                minimumWidth.AutoSizeAxes = Axes.None;
                minimumWidth.Height = M.DROPDOWN_HEIGHT;

                Text.Font = M.Font(M.DROPDOWN_FONT_SIZE);
                Text.Spacing = Vector2.Zero;
                Text.Margin = new MarginPadding { Horizontal = M.BINDING_KEY_PADDING };
                Text.Colour = Color4.White;
            }

            private void updateStableHoverState()
            {
                const float transition_time = 120;

                Color4 colour = isBinding ? StableSettingsColours.MAIN : IsHovered ? M.DROPDOWN_HIGHLIGHT : M.DROPDOWN_BACKGROUND;

                box.FadeColour(colour, transition_time, Easing.OutQuint);
            }
        }
    }
}
