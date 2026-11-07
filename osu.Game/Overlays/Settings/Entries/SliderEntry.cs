// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Numerics;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Settings.Rendering;
using osu.Game.Extensions;

namespace osu.Game.Overlays.Settings.Entries
{
    /// <summary>
    /// A numeric value adjusted with a slider. Range and precision come from the bound <see cref="BindableNumber{T}"/>.
    /// </summary>
    public sealed class SliderEntry<T> : ValueSettingsEntry<T>
        where T : struct, INumber<T>, IMinMaxValue<T>
    {
        private readonly BindableNumberWithCurrent<T> current;

        public SliderEntry()
            : this(new BindableNumberWithCurrent<T>())
        {
        }

        private SliderEntry(BindableNumberWithCurrent<T> current)
            : base(current)
        {
            this.current = current;
        }

        /// <summary>
        /// <see cref="ValueSettingsEntry{T}.Current"/> as a number, exposing its range and precision.
        /// </summary>
        public BindableNumber<T> CurrentNumber => current;

        /// <summary>
        /// A custom step for each key press. <c>null</c> lets the renderer pick one from the precision.
        /// </summary>
        public float? KeyboardStep { get; init; }

        /// <summary>
        /// Whether the default value label shows the value as a percentage.
        /// </summary>
        public bool DisplayAsPercentage { get; init; }

        /// <summary>
        /// Whether sound effects should play while adjusting.
        /// </summary>
        public bool PlaySamplesOnAdjust { get; init; } = true;

        private readonly BindableBool transferValueOnCommit = new BindableBool();

        /// <summary>
        /// If <c>true</c>, the value is only written to <see cref="ValueSettingsEntry{T}.Current"/> once an adjustment is committed.
        /// May be changed at any time; renderers follow <see cref="TransferValueOnCommitBindable"/>.
        /// </summary>
        public bool TransferValueOnCommit
        {
            get => transferValueOnCommit.Value;
            set => transferValueOnCommit.Value = value;
        }

        public IBindable<bool> TransferValueOnCommitBindable => transferValueOnCommit;

        /// <summary>
        /// A custom formatter for the displayed value.
        /// </summary>
        public Func<T, LocalisableString>? LabelFormat { get; init; }

        /// <summary>
        /// A custom formatter for the value tooltip. Falls back to <see cref="FormatValue"/>.
        /// </summary>
        public Func<T, LocalisableString>? TooltipFormat { get; init; }

        /// <summary>
        /// Formats a value for a tooltip, using <see cref="TooltipFormat"/> if provided.
        /// </summary>
        public LocalisableString FormatTooltip(T value) => TooltipFormat?.Invoke(value) ?? FormatValue(value);

        /// <summary>
        /// Formats a value for display, using <see cref="LabelFormat"/> if provided.
        /// </summary>
        public LocalisableString FormatValue(T value)
            => LabelFormat?.Invoke(value) ?? value.ToStandardFormattedString(OsuSliderBar<T>.MAX_DECIMAL_DIGITS, DisplayAsPercentage);

        public override Drawable CreateDrawable(ISettingsRenderer renderer) => renderer.CreateSlider(this);

        public override void Unbind()
        {
            base.Unbind();
            transferValueOnCommit.UnbindAll();
        }
    }
}
