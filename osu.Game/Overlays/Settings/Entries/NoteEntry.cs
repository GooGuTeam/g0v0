// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.Overlays.Settings.Rendering;

namespace osu.Game.Overlays.Settings.Entries
{
    /// <summary>
    /// A standalone line of informational or warning text, shown from <see cref="SettingsEntry.Note"/> (hidden while it is <c>null</c>).
    /// </summary>
    public sealed class NoteEntry : SettingsEntry
    {
        /// <summary>
        /// Whether the note describes the item directly above it. Renderers may then draw it without the usual gap between items.
        /// </summary>
        public bool AttachedToPrevious { get; init; }

        public override bool IsDefault => true;

        public override bool IsDisabled => false;

        public override void SetDefault()
        {
        }

        public override Drawable CreateDrawable(ISettingsRenderer renderer) => renderer.CreateNote(this);
    }
}
