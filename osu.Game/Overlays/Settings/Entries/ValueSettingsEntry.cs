// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics.UserInterface;

namespace osu.Game.Overlays.Settings.Entries
{
    /// <summary>
    /// A <see cref="SettingsEntry"/> which edits a single bindable value.
    /// </summary>
    /// <remarks>
    /// Assigning <see cref="Current"/> binds an entry-owned copy to the given bindable, so consumers may subscribe to
    /// <see cref="Current"/> without leaking into the source bindable (usually a config bindable).
    /// </remarks>
    public abstract class ValueSettingsEntry<T> : SettingsEntry, IHasCurrentValue<T>
    {
        private readonly Bindable<T> current;
        private readonly IHasCurrentValue<T> currentTarget;

        private T classicDefault = default!;
        private bool hasClassicDefault;

        protected ValueSettingsEntry()
            : this(new BindableWithCurrent<T>())
        {
        }

        /// <param name="current">The entry-owned bindable storage. Must also implement <see cref="IBindableWithCurrent{T}"/>.</param>
        protected ValueSettingsEntry(Bindable<T> current)
        {
            this.current = current;
            currentTarget = (IBindableWithCurrent<T>)current;
            current.ValueChanged += _ => TriggerValueChanged();
        }

        public Bindable<T> Current
        {
            get => current;
            set => currentTarget.Current = value;
        }

        /// <summary>
        /// A value which better aligns with osu!stable expectations. Setting this marks the entry as having a classic default.
        /// </summary>
        public T ClassicDefault
        {
            get => classicDefault;
            init
            {
                classicDefault = value;
                hasClassicDefault = true;
            }
        }

        public override bool HasClassicDefault => hasClassicDefault;

        public override void ApplyClassicDefault()
        {
            if (!hasClassicDefault)
                throw new InvalidOperationException($"{GetType().Name} has no classic default.");

            Current.Value = classicDefault;
        }

        public override bool IsDefault => Current.IsDefault;

        public override bool IsDisabled => Current.Disabled;

        public override void SetDefault()
        {
            if (!Current.Disabled)
                Current.SetDefault();
        }

        public override void Unbind()
        {
            base.Unbind();
            current.UnbindAll();
        }
    }
}
