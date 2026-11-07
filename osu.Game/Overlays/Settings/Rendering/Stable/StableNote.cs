// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Overlays.Settings.Rendering.Stable
{
    /// <summary>
    /// osu!stable's text element (<c>OptionText</c>): a plain 12pt paragraph across the element width, in white or,
    /// for warnings, stable's (255, 198, 23). It has no box; the whole element is hidden while there is no note.
    /// </summary>
    public partial class StableNote : StableSettingsItem
    {
        public StableNote(NoteEntry entry)
            : base(entry)
        {
        }

        // the base frame already lays the note out as an element's text; there is no control above it.
        protected override Drawable? CreateContent() => null;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Entry.Note.BindValueChanged(note => Alpha = note.NewValue == null ? 0 : 1, true);
        }
    }
}
