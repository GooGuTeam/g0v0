// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osuTK.Graphics;

namespace osu.Game.Overlays.Settings.Rendering.Stable
{
    /// <summary>
    /// Metrics of the osu!stable options menu, converted to settings panel units.
    /// </summary>
    /// <remarks>
    /// Unit conversion (stable <c>WindowManager</c> / <c>pSprite</c> / <c>pText</c>):
    /// <list type="bullet">
    /// <item>positions use <c>Ratio = Height / 480</c>, lazer uses <c>Height / 768</c>, so one stable position unit is <see cref="U"/> = 1.6 panel units;</item>
    /// <item>sprite sizes are <c>texture px * Scale * Height / 768</c>, i.e. <c>texture px * Scale</c> panel units
    /// (<c>Scale = 1.6</c> on a white pixel therefore spans exactly one position unit per vector-scale unit);</item>
    /// <item><c>pText</c> renders a GDI font of <c>TextSize * Height / 768</c> <em>points</em>, so its em size is <c>TextSize * 4 / 3</c> panel units.</item>
    /// </list>
    /// </remarks>
    public static class StableOptionsMetrics
    {
        /// <summary>
        /// One stable options position unit in panel units.
        /// </summary>
        public const float U = 1.6f;

        /// <summary>
        /// Converts a <c>pText</c> text size to a font em size.
        /// </summary>
        public static float Text(float stableTextSize) => stableTextSize * 4 / 3;

        /// <summary>
        /// The font used throughout the stable styled options. Stable uses Aller; this fork renders all of it with Inter.
        /// </summary>
        public static FontUsage Font(float size, FontWeight weight = FontWeight.Regular) => OsuFont.GetFont(Typeface.Inter, size: size, weight: weight);

        // ---- panel chrome (Options) ----

        /// <summary>
        /// The panel background: #080808 at 70%.
        /// </summary>
        public static readonly Color4 PANEL_BACKGROUND = new Color4(8, 8, 8, 179);

        /// <summary>
        /// The title area is 100 units high before the search box (<c>searchBox</c> at y = 100);
        /// the title is 20pt at y = 40, the subtitle 14pt in the main colour at y = 60, both centred with a shadow.
        /// </summary>
        public const float HEADER_HEIGHT = 100 * U;

        public const float HEADER_TITLE_Y = 40 * U;

        public const float HEADER_SUBTITLE_Y = 60 * U;

        /// <summary>
        /// The pinned header backdrop (<c>headerBackground</c>): #080808, at most 50% once scrolled.
        /// </summary>
        public static readonly Color4 PINNED_HEADER_BACKGROUND = new Color4(8, 8, 8, 128);

        /// <summary>
        /// <c>pSearchBox(18, ..)</c>: centred 18pt text with a <c>size + 2</c> search icon to its left, and no box. The header backdrop is 130 units in total.
        /// </summary>
        public static readonly float SEARCH_FONT_SIZE = Text(18);

        public static readonly float SEARCH_ICON_SIZE = Text(20) * 0.75f;

        public const float SEARCH_BAR_HEIGHT = 30 * U;

        /// <summary>
        /// Sidebar tabs (<c>OptionTab</c>): 40 x 40 units on #080808, icon 18pt in (153, 153, 153) turning white on hover or when active,
        /// and an active "light" bar 4 units wide in the main colour on the right edge.
        /// </summary>
        public const float SIDEBAR_WIDTH = 40 * U;

        public const float TAB_HEIGHT = 40 * U;

        public static readonly float TAB_ICON_SIZE = Text(18) * 0.85f;

        public static readonly Color4 SIDEBAR_BACKGROUND = new Color4(8, 8, 8, 255);

        public static readonly Color4 TAB_ACTIVE_BACKGROUND = new Color4(8, 8, 8, 191);

        public static readonly Color4 TAB_ICON_COLOUR = new Color4(153, 153, 153, 255);

        public const float TAB_LIGHT_WIDTH = 4 * U;

        // ---- key bindings ----

        // Stable binds keys in a separate dialog; here a binding row is an ordinary option element laid out like a dropdown
        // (label on the left, the bound keys as dropdown boxes on the right), so it matches the rest of the options.

        /// <summary>
        /// A key box is at least 40 units wide so short keys line up; longer key names widen it.
        /// </summary>
        public const float BINDING_KEY_MIN_WIDTH = 40 * U;

        /// <summary>
        /// The key name's inset inside its box.
        /// </summary>
        public const float BINDING_KEY_PADDING = 4 * U;

        // ---- tooltip (pTooltip) ----

        /// <summary>
        /// The tooltip text is 9 to 10pt depending on the window height (<c>9 - (height - 1024) / 256</c>); 10pt is used here.
        /// </summary>
        public static readonly float TOOLTIP_FONT_SIZE = Text(10);

        public static readonly Color4 TOOLTIP_BACKGROUND = new Color4(10, 10, 10, 230);

        public static readonly Color4 TOOLTIP_BORDER_COLOUR = new Color4(80, 80, 80, 255);

        public const float TOOLTIP_BORDER = 1;

        public const float TOOLTIP_CORNER_RADIUS = 4;

        /// <summary>
        /// Text wider than 400 units wraps.
        /// </summary>
        public const float TOOLTIP_MAX_WIDTH = 400 * U;

        public const float TOOLTIP_PADDING_H = 3 * U;

        public const float TOOLTIP_PADDING_V = 1 * U;

        public const double TOOLTIP_FADE_DURATION = 200;

        // ---- back button (BackButton, unskinned) ----

        /// <summary>
        /// Two <c>back-button-layer</c> sprites (160 x 58, origin bottom-right): a 44 unit tall body whose right edge slants
        /// from 3 units in at the top to 11 units in at the bottom, with a soft shadow below; the body's bottom sits 14 units above the screen edge.
        /// </summary>
        public const float BACK_LAYER_WIDTH = 160;

        public const float BACK_LAYER_HEIGHT = 44;

        public const float BACK_LAYER_BOTTOM = 14;

        public const float BACK_LAYER_SHADOW = 4;

        /// <summary>
        /// The body's right edge at its bottom, measured from the sprite's right edge (its origin).
        /// </summary>
        public const float BACK_LAYER_INSET = 11;

        public const float BACK_LAYER_SHEAR = 8f / BACK_LAYER_HEIGHT;

        /// <summary>
        /// Right edges of the left (icon) and right (text) layers when contracted and when hovered, from the screen's left edge.
        /// </summary>
        public const float BACK_LEFT_CONTRACTED = 22 * U;

        public const float BACK_LEFT_EXPANDED = 40 * U;

        public const float BACK_RIGHT_CONTRACTED = 60 * U;

        public const float BACK_RIGHT_EXPANDED = 86 * U;

        public const float BACK_ICON_CONTRACTED = 9.166666f * U;

        public const float BACK_ICON_EXPANDED = 16.666666f * U;

        /// <summary>
        /// The icon and the 15pt lowercase text are centred 21 units above the screen's bottom edge.
        /// </summary>
        public const float BACK_CONTENT_Y = 21 * U;

        public static readonly float BACK_FONT_SIZE = Text(15);

        public static readonly float BACK_ICON_SIZE = Text(18) * 0.85f;

        /// <summary>
        /// Text wider than 38 units widens the right layer by the difference.
        /// </summary>
        public const float BACK_TEXT_ALLOWANCE = 38 * U;

        public static readonly Color4 BACK_IDLE = new Color4(238, 51, 153, 255);

        public static readonly Color4 BACK_ACTIVE = new Color4(187, 17, 119, 255);

        public const double BACK_HOVER_DURATION = 600;

        public const double BACK_CLICK_DURATION = 400;

        /// <summary>
        /// Category headers (<c>OptionCategory</c>): 24pt upper-cased in the secondary colour, right-aligned,
        /// at 40% alpha unless highlighted (then the secondary colour lightened by 0.4).
        /// </summary>
        public static readonly float CATEGORY_FONT_SIZE = Text(24);

        public const float CATEGORY_INACTIVE_ALPHA = 0.4f;

        /// <summary>
        /// A category only takes 10 units of layout height; its text overlaps the following section's padding.
        /// </summary>
        public const float CATEGORY_HEIGHT = 10 * U;

        // ---- layout (Options.performLayout) ----

        /// <summary>
        /// Section headers are placed 10 units in; their text at a further 10 (<c>OptionSection</c>: <c>new pText(.., new Vector2(10, 0))</c>).
        /// </summary>
        public const float HEADER_X = 20 * U;

        /// <summary>
        /// Non-header elements are placed 20 units in.
        /// </summary>
        public const float ELEMENT_X = 20 * U;

        /// <summary>
        /// Elements are 244 units wide (<c>OptionElement.size</c>, <c>244f - Size.X</c> in sliders and dropdowns).
        /// </summary>
        public const float ELEMENT_WIDTH = 244 * U;

        /// <summary>
        /// Elements are separated by 10 units (<c>array[num2] += Size.Y + 10f</c>); the hover highlight covers 5 above and below.
        /// </summary>
        public const float ELEMENT_GAP = 10 * U;

        /// <summary>
        /// Sections add 20 units before their header (<c>OptionSection.PaddingBefore</c>).
        /// </summary>
        public const float SECTION_PADDING_BEFORE = 20 * U;

        /// <summary>
        /// The section guide line: at x = 10 + 5, two units wide, white at alpha 40, additive (<c>OptionSection.line</c>).
        /// </summary>
        public const float SECTION_LINE_X = 15 * U;

        public const float SECTION_LINE_WIDTH = 2 * U;

        public static readonly Color4 SECTION_LINE_COLOUR = new Color4(255, 255, 255, 40);

        /// <summary>
        /// The hover highlight (<c>shiftingBackground</c>): #080808 at 70%, full panel width.
        /// </summary>
        public static readonly Color4 HOVER_COLOUR = new Color4(8, 8, 8, 255);

        public const float HOVER_ALPHA = 0.7f;

        /// <summary>
        /// The highlight follows the hovered element over 140ms.
        /// </summary>
        public const double HOVER_DURATION = 140;

        // ---- "modified" indicator (OptionElement.defaultLine) ----

        /// <summary>
        /// At the element's x - 5 (the section line), a 6px gradient at <c>Scale = 1.6</c>, <c>VectorScale.X = 2</c>.
        /// </summary>
        public const float INDICATOR_X = ELEMENT_X - 5 * U;

        public const float INDICATOR_WIDTH = 6 * 1.6f * 2;

        /// <summary>
        /// The gradient texture's alpha stops: 255, 160, 120, 80, 40, 5.
        /// </summary>
        public static readonly Color4 INDICATOR_COLOUR = new Color4(255, 186, 0, 255);

        // ---- text ----

        /// <summary>
        /// Labels are 12pt (sliders, dropdowns, text boxes, buttons; checkboxes use 15 * 0.8).
        /// </summary>
        public static readonly float LABEL_FONT_SIZE = Text(12);

        /// <summary>
        /// Section headers are 12pt bold, upper-cased.
        /// </summary>
        public static readonly float HEADER_FONT_SIZE = Text(12);

        /// <summary>
        /// Option text (<c>OptionText</c>) warnings use (255, 198, 23).
        /// </summary>
        public static readonly Color4 WARNING_TEXT_COLOUR = new Color4(255, 198, 23, 255);

        // ---- checkbox (pCheckbox, size 0.8) ----

        public const float CHECKBOX_SCALE = 0.8f;

        /// <summary>
        /// <c>circle-full</c> / <c>circle-empty</c> are 16 panel units at scale 1.
        /// </summary>
        public const float CIRCLE_SIZE = 16;

        /// <summary>
        /// The circle's centre is at (8, 8) from the element.
        /// </summary>
        public const float CHECKBOX_CIRCLE_CENTRE = 8 * U;

        /// <summary>
        /// The label is at 20 * size, vertically centred on y = 8.
        /// </summary>
        public const float CHECKBOX_LABEL_X = 20 * CHECKBOX_SCALE * U;

        /// <summary>
        /// Toggling pops the circle from 1.2x (checked) or 0.8x (unchecked) back to 1 over 350ms.
        /// </summary>
        public const double CHECKBOX_POP_DURATION = 350;

        // ---- slider (pSliderBar) ----

        /// <summary>
        /// The bar starts 5 units after the label.
        /// </summary>
        public const float SLIDER_LABEL_GAP = 5 * U;

        /// <summary>
        /// Lines are <c>VectorScale.Y = 1.5</c> at scale 1; the right one at 50% alpha.
        /// </summary>
        public const float SLIDER_LINE_HEIGHT = 1.5f;

        /// <summary>
        /// The nub is <c>circle-empty</c> at scale 1, moving with a 100ms ease.
        /// </summary>
        public const float SLIDER_NUB_SIZE = CIRCLE_SIZE;

        public const double SLIDER_NUB_MOVE_DURATION = 100;

        // ---- dropdown (OptionDropdown / pDropdown) ----

        /// <summary>
        /// The dropdown starts right after the label measure plus 5 units.
        /// </summary>
        public const float DROPDOWN_LABEL_GAP = 5 * U;

        /// <summary>
        /// The main box: 13pt text, 15 units high, black at 128 alpha, 5 unit corners, chevron at width - 8.
        /// </summary>
        public static readonly float DROPDOWN_FONT_SIZE = Text(13);

        public const float DROPDOWN_HEIGHT = 15 * U;

        public static readonly Color4 DROPDOWN_BACKGROUND = new Color4(0, 0, 0, 128);

        /// <summary>
        /// Hovered main box and options use <c>NEW_SKIN_COLOUR_MAIN_DARKER</c>.
        /// </summary>
        public static readonly Color4 DROPDOWN_HIGHLIGHT = new Color4(187, 17, 119, 255);

        /// <summary>
        /// Options: 12pt text, 17 units high, (12, 3) padding, black at 240 alpha.
        /// </summary>
        public static readonly float DROPDOWN_OPTION_FONT_SIZE = Text(12);

        public const float DROPDOWN_OPTION_HEIGHT = 17 * U;

        public static readonly Color4 DROPDOWN_OPTION_BACKGROUND = new Color4(0, 0, 0, 240);

        // ---- text box (OptionTextbox / pTextBox) ----

        /// <summary>
        /// The box is 5 units below the label and 239 units wide.
        /// </summary>
        public const float TEXTBOX_GAP = 5 * U;

        public const float TEXTBOX_WIDTH = 239 * U;

        public const float TEXTBOX_BORDER = 2;

        public static readonly Color4 TEXTBOX_BORDER_COLOUR = new Color4(180, 180, 180, 255);

        public static readonly Color4 TEXTBOX_BACKGROUND = new Color4(50, 50, 50, 80);

        public static readonly Color4 TEXTBOX_BACKGROUND_FOCUSED = new Color4(50, 50, 50, 200);

        // ---- button (OptionButton / pButton) ----

        /// <summary>
        /// Buttons are 244 x 20 units with 12pt Aller text, centred.
        /// </summary>
        public const float BUTTON_HEIGHT = 20 * U;

        /// <summary>
        /// The idle colour is the button colour less 20 per channel; hovering fades to the full colour over 50ms.
        /// </summary>
        public const int BUTTON_IDLE_DARKEN = 20;

        public const double BUTTON_HOVER_DURATION = 50;

        /// <summary>
        /// Clicking flashes the button white over 400ms.
        /// </summary>
        public const double BUTTON_FLASH_DURATION = 400;
    }
}
