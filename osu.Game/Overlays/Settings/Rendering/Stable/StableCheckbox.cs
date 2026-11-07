// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Overlays.Settings.Rendering.Stable
{
    /// <summary>
    /// osu!stable's options checkbox (<c>OptionCheckbox</c> / <c>pCheckbox</c> at size 0.8): <c>circle-empty</c> / <c>circle-full</c> centred at (8, 8),
    /// the label at 20 * 0.8. Toggling pops the circle, and clicking anywhere on the row toggles it.
    /// </summary>
    public partial class StableCheckbox : StableSettingsItem
    {
        private readonly CheckboxEntry entry;

        public StableCheckbox(CheckboxEntry entry)
            : base(entry)
        {
            this.entry = entry;
        }

        protected override Drawable CreateContent() => new StableCheckboxControl(entry.Caption, wrapLabel: true)
        {
            Current = entry.Current,
            // the whole row toggles it.
            ToggleOnClick = false,
        };

        protected override void OnRowClicked()
        {
            if (!entry.Current.Disabled)
                entry.Current.Value = !entry.Current.Value;
        }
    }
}
