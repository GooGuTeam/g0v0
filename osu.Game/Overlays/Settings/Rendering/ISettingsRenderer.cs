// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Numerics;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Overlays.Settings.Rendering
{
    /// <summary>
    /// Turns presentation-agnostic <see cref="SettingsEntry"/>s, and the chrome of a <see cref="SettingsPanel"/>, into drawables of one particular style.
    /// </summary>
    /// <remarks>
    /// Value items returned by a renderer implement <see cref="ISettingsItem"/> and <see cref="IConditionalFilterable"/>,
    /// so search and "classic defaults" keep working regardless of the style.
    /// A renderer is cached by <see cref="SettingsPanel"/>; subsections reach it through <see cref="SettingsSubsection.Renderer"/>.
    /// </remarks>
    public interface ISettingsRenderer
    {
        #region Entries

        Drawable CreateCheckbox(CheckboxEntry entry);

        Drawable CreateSlider<T>(SliderEntry<T> entry)
            where T : struct, INumber<T>, IMinMaxValue<T>;

        Drawable CreateDropdown<T>(DropdownEntry<T> entry)
            where T : notnull;

        Drawable CreateEnumDropdown<T>(EnumDropdownEntry<T> entry)
            where T : struct, Enum;

        Drawable CreateTextBox(TextBoxEntry entry);

        Drawable CreateButton(ButtonEntry entry);

        Drawable CreateNote(NoteEntry entry);

        #endregion

        #region Sections

        /// <summary>
        /// Creates the header shown at the top of a <see cref="SettingsSubsection"/>.
        /// </summary>
        Drawable CreateSubsectionHeader(LocalisableString header);

        /// <summary>
        /// Creates the frame around a <see cref="SettingsSection"/>'s <paramref name="content"/>. The frame must contain <paramref name="content"/>.
        /// </summary>
        ISettingsSectionFrame CreateSectionFrame(LocalisableString header, Drawable content);

        #endregion

        #region Composite controls

        /// <summary>
        /// Whether composite controls with no entry equivalent (key binding rows, input device headers, the audio offset adjuster,
        /// the skin controls) use their osu!stable layouts, built from the stable elements.
        /// </summary>
        bool UseStableLayouts { get; }

        /// <summary>
        /// Creates the button resetting all bindings of a key binding subsection.
        /// </summary>
        Drawable CreateKeyBindingResetButton(ButtonEntry entry);

        #endregion

        #region Panel chrome

        /// <summary>
        /// Creates the background behind the panel's sections.
        /// </summary>
        Drawable CreatePanelBackground();

        /// <summary>
        /// Creates the panel's title area (scrolls away with the content).
        /// </summary>
        Drawable CreatePanelHeader(LocalisableString title, LocalisableString description);

        /// <summary>
        /// Creates the backdrop shown behind the pinned search bar once the title area has scrolled away.
        /// </summary>
        Drawable CreatePinnedHeaderBackground();

        /// <summary>
        /// Creates the pinned search bar around <paramref name="textBox"/>, which must stay focusable and receive the typed text.
        /// </summary>
        Drawable CreateSearchBar(SettingsSearchTextBox textBox);

        /// <summary>
        /// Creates the section navigation sidebar.
        /// </summary>
        SettingsSidebar CreateSidebar(bool showBackButton);

        /// <summary>
        /// Creates a back button owned by the root settings panel, or <c>null</c> if back navigation is left to the sidebar
        /// (see <see cref="CreateSidebar"/>). <paramref name="action"/> steps back one level: it closes an open sub-panel, or else the panel.
        /// </summary>
        Drawable? CreateRootBackButton(Action action);

        /// <summary>
        /// Creates a sidebar button navigating to <paramref name="section"/>,
        /// or <c>null</c> if the section should not be listed in the sidebar.
        /// </summary>
        ISettingsSidebarButton? CreateSidebarButton(SettingsSection section, Action action);

        #endregion
    }
}
