// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Overlays.Settings.Rendering;
using osuTK.Graphics;

namespace osu.Game.Overlays.Settings.Entries
{
    /// <summary>
    /// An action button. <see cref="SettingsEntry.Caption"/> is the button text and <see cref="SettingsEntry.HintText"/> its tooltip.
    /// </summary>
    public sealed class ButtonEntry : SettingsEntry
    {
        /// <summary>
        /// The action performed on click.
        /// </summary>
        public Action? Action { get; set; }

        /// <summary>
        /// Whether the button can currently be clicked.
        /// </summary>
        public BindableBool Enabled { get; } = new BindableBool(true);

        /// <summary>
        /// Whether this button performs a destructive action.
        /// </summary>
        public bool Dangerous { get; init; }

        /// <summary>
        /// Whether this is a prominent button (e.g. one opening a sub-panel). Renderers may draw it larger.
        /// </summary>
        public bool Large { get; init; }

        /// <summary>
        /// A custom background colour, overriding the renderer's default.
        /// </summary>
        public Color4? BackgroundColour { get; init; }

        /// <summary>
        /// When set, clicking the button shows this popover from it instead of running <see cref="Action"/>.
        /// </summary>
        public Func<Popover>? Popover { get; init; }

        public override bool IsDefault => true;

        public override bool IsDisabled => !Enabled.Value;

        public override void SetDefault()
        {
        }

        public override Drawable CreateDrawable(ISettingsRenderer renderer) => renderer.CreateButton(this);

        public override void Unbind()
        {
            base.Unbind();
            Enabled.UnbindAll();
        }
    }
}
