// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Overlays.Settings.Rendering;

namespace osu.Game.Overlays.Settings.Entries
{
    /// <summary>
    /// A presentation-agnostic description of a single settings entry.
    /// </summary>
    /// <remarks>
    /// Entries hold the state and metadata of a setting (its bindable, caption, hint, keywords, notes and visibility),
    /// but no drawables. A <see cref="ISettingsRenderer"/> turns an entry into the control set of a particular style
    /// (the lazer form controls, or the osu!stable options controls), so subsections describe their settings once and
    /// every renderer stays in sync with them.
    /// </remarks>
    public abstract class SettingsEntry
    {
        /// <summary>
        /// The primary label of this entry.
        /// </summary>
        public LocalisableString Caption { get; init; }

        private readonly Bindable<LocalisableString> hintText = new Bindable<LocalisableString>();

        /// <summary>
        /// An extended description of this entry. Renderers usually display it in a tooltip.
        /// May be changed at any time; renderers follow <see cref="HintTextBindable"/> where their controls allow it.
        /// </summary>
        public LocalisableString HintText
        {
            get => hintText.Value;
            set => hintText.Value = value;
        }

        public IBindable<LocalisableString> HintTextBindable => hintText;

        /// <summary>
        /// Extra search terms which should match this entry.
        /// </summary>
        public IEnumerable<string> Keywords { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Whether this entry can currently be shown. Bind to a condition to hide the entry while it does not apply.
        /// </summary>
        public BindableBool CanBeShown { get; } = new BindableBool(true);

        /// <summary>
        /// A note to display alongside the entry.
        /// </summary>
        public Bindable<SettingsNote.Data?> Note { get; } = new Bindable<SettingsNote.Data?>();

        /// <summary>
        /// Whether renderers should offer a way to revert this entry to its default when it is modified.
        /// </summary>
        public bool ShowRevertToDefaultButton { get; init; } = true;

        /// <summary>
        /// Whether the entry is currently in its default state.
        /// </summary>
        public abstract bool IsDefault { get; }

        /// <summary>
        /// Whether the entry currently does not accept user changes.
        /// </summary>
        public abstract bool IsDisabled { get; }

        /// <summary>
        /// Resets the entry to its default state.
        /// </summary>
        public abstract void SetDefault();

        /// <summary>
        /// Invoked when the value of this entry has changed.
        /// </summary>
        public event Action? ValueChanged;

        protected void TriggerValueChanged() => ValueChanged?.Invoke();

        /// <summary>
        /// Whether this entry has a different default which better aligns with osu!stable expectations.
        /// </summary>
        public virtual bool HasClassicDefault => false;

        /// <summary>
        /// Applies the classic default. Throws if <see cref="HasClassicDefault"/> is <c>false</c>.
        /// </summary>
        public virtual void ApplyClassicDefault() => throw new InvalidOperationException($"{GetType().Name} has no classic default.");

        /// <summary>
        /// The terms which this entry should be matched by when searching.
        /// </summary>
        public virtual IEnumerable<LocalisableString> FilterTerms
        {
            get
            {
                yield return Caption;

                if (HintText != default)
                    yield return HintText;

                foreach (string keyword in Keywords)
                    yield return keyword;
            }
        }

        /// <summary>
        /// Creates the drawable representing this entry using the given renderer.
        /// </summary>
        public abstract Drawable CreateDrawable(ISettingsRenderer renderer);

        /// <summary>
        /// Releases the bindings held by this entry. Called by the rendered item once it is disposed.
        /// </summary>
        public virtual void Unbind()
        {
            CanBeShown.UnbindAll();
            Note.UnbindAll();
            hintText.UnbindAll();
        }
    }
}
