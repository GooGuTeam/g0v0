// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Numerics;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using Vector2 = osuTK.Vector2;
using osu.Game.Overlays.Settings.Entries;
using osu.Game.Overlays.Settings.Rendering.Stable;
using osu.Game.Overlays.Settings.Sections;

namespace osu.Game.Overlays.Settings.Rendering
{
    /// <summary>
    /// Renders settings entries with controls modelled after the osu!stable options menu.
    /// </summary>
    public class StableSettingsRenderer : ISettingsRenderer
    {
        public static readonly StableSettingsRenderer DEFAULT = new StableSettingsRenderer();

        public Drawable CreateCheckbox(CheckboxEntry entry) => new StableCheckbox(entry);

        public Drawable CreateSlider<T>(SliderEntry<T> entry)
            where T : struct, INumber<T>, IMinMaxValue<T>
            => new StableSlider<T>(entry);

        public Drawable CreateDropdown<T>(DropdownEntry<T> entry)
            where T : notnull
            => new StableDropdown<T>(entry);

        public Drawable CreateEnumDropdown<T>(EnumDropdownEntry<T> entry)
            where T : struct, Enum
            => new StableDropdown<T>(entry);

        public Drawable CreateTextBox(TextBoxEntry entry) => new StableTextBox(entry);

        public Drawable CreateButton(ButtonEntry entry) => new StableButton(entry);

        public Drawable CreateNote(NoteEntry entry) => new StableNote(entry);

        public bool UseStableLayouts => true;

        // key bindings use the same elements as the rest of the options: the reset button is an ordinary (dangerous) option button.
        public Drawable CreateKeyBindingResetButton(ButtonEntry entry) => CreateButton(entry);

        public Drawable CreateSubsectionHeader(LocalisableString header) => new StableSubsectionHeader(header);

        public ISettingsSectionFrame CreateSectionFrame(LocalisableString header, Drawable content) => new StableSectionFrame(header, content);

        public Drawable CreatePanelBackground() => new Box
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Scale = new Vector2(2, 1), // over-extend to the left for transitions
            RelativeSizeAxes = Axes.Both,
            Colour = StableOptionsMetrics.PANEL_BACKGROUND,
        };

        public Drawable CreatePanelHeader(LocalisableString title, LocalisableString description) => new StablePanelHeader(title, description);

        public Drawable CreatePinnedHeaderBackground() => new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = StableOptionsMetrics.PINNED_HEADER_BACKGROUND,
        };

        public Drawable CreateSearchBar(SettingsSearchTextBox textBox) => new StableSearchBar(textBox);

        // stable has a single back button that steps back one level, so sub-panels' sidebars get none.
        public SettingsSidebar CreateSidebar(bool showBackButton) => new StableSettingsSidebar();

        public Drawable? CreateRootBackButton(Action action) => new StableBackButton { Action = action };

        // stable has no per-ruleset categories, so their sections stay in the list but get no tab.
        public ISettingsSidebarButton? CreateSidebarButton(SettingsSection section, Action action) => section is RulesetSection
            ? null
            : new StableSidebarButton(section)
            {
                Action = action,
            };
    }
}
