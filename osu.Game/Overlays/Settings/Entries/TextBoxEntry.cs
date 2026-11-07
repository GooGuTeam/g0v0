// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Overlays.Settings.Rendering;

namespace osu.Game.Overlays.Settings.Entries
{
    /// <summary>
    /// A free-form text value.
    /// </summary>
    public sealed class TextBoxEntry : ValueSettingsEntry<string>
    {
        /// <summary>
        /// Text shown while the value is empty.
        /// </summary>
        public LocalisableString PlaceholderText { get; init; }

        /// <summary>
        /// The maximum number of characters, if any.
        /// </summary>
        public int? LengthLimit { get; init; }

        public override Drawable CreateDrawable(ISettingsRenderer renderer) => renderer.CreateTextBox(this);
    }
}
