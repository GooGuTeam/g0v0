// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Numerics;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Settings.Entries;
using M = osu.Game.Overlays.Settings.Rendering.Stable.StableOptionsMetrics;

namespace osu.Game.Overlays.Settings.Rendering.Stable
{
    /// <summary>
    /// osu!stable's options slider (<c>OptionSlider</c> / <c>pSliderBar</c>): the label, 5 units of space, then a thin line to the end of the row
    /// (solid up to the hollow nub, half transparent after it). The value is only shown in the tooltip.
    /// </summary>
    public partial class StableSlider<T> : StableSettingsItem
        where T : struct, INumber<T>, IMinMaxValue<T>
    {
        private readonly SliderEntry<T> entry;
        private readonly BindableNumber<T> barCurrent = new BindableNumber<T>();
        private readonly StableSliderBar<T> bar;

        private bool updatingFromEntry;

        public StableSlider(SliderEntry<T> entry)
            : base(entry)
        {
            this.entry = entry;

            // Populate numeric bounds, value, and default before applying Disabled so SliderBar<T>.Current
            // never attempts to set CurrentNumber.Default or clamp CurrentNumber.Value while Disabled is true.
            barCurrent.Precision = entry.CurrentNumber.Precision;
            barCurrent.MinValue = entry.CurrentNumber.MinValue;
            barCurrent.MaxValue = entry.CurrentNumber.MaxValue;
            barCurrent.Value = entry.CurrentNumber.Value;
            barCurrent.Default = entry.CurrentNumber.Default;

            // like FormSliderBar, the framework slider is created and bound in the constructor so that range changes
            // always reach it, even if this item is unloaded before LoadComplete.
            bar = new StableSliderBar<T>(entry.FormatTooltip)
            {
                RelativeSizeAxes = Axes.Both,
                Current = barCurrent,
            };

            barCurrent.Disabled = entry.CurrentNumber.Disabled;

            barCurrent.ValueChanged += e =>
            {
                if (!updatingFromEntry)
                    entry.CurrentNumber.Value = e.NewValue;
            };

            entry.CurrentNumber.ValueChanged += e => applyFromEntry(() => barCurrent.Value = e.NewValue);
            entry.CurrentNumber.MinValueChanged += v => applyFromEntry(() =>
            {
                barCurrent.MinValue = v;
                barCurrent.Value = entry.CurrentNumber.Value;
            });
            entry.CurrentNumber.MaxValueChanged += v => applyFromEntry(() =>
            {
                barCurrent.MaxValue = v;
                barCurrent.Value = entry.CurrentNumber.Value;
            });
            entry.CurrentNumber.PrecisionChanged += v => applyFromEntry(() =>
            {
                barCurrent.Precision = v;
                barCurrent.Value = entry.CurrentNumber.Value;
            });
            entry.CurrentNumber.DefaultChanged += e => applyFromEntry(() => barCurrent.Default = e.NewValue);
            entry.CurrentNumber.DisabledChanged += disabled =>
            {
                if (disabled)
                    applyFromEntry(() => barCurrent.Value = entry.CurrentNumber.Value);

                barCurrent.Disabled = disabled;
            };

            entry.TransferValueOnCommitBindable.BindValueChanged(v => bar.TransferValueOnCommit = v.NewValue, true);

            if (entry.KeyboardStep != null)
                bar.KeyboardStep = entry.KeyboardStep.Value;
        }

        private void applyFromEntry(Action action)
        {
            updatingFromEntry = true;
            bool wasDisabled = barCurrent.Disabled;
            barCurrent.Disabled = false;

            try
            {
                action();
            }
            finally
            {
                barCurrent.Disabled = wasDisabled;
                updatingFromEntry = false;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            barCurrent.UnbindAll();
            base.Dispose(isDisposing);
        }

        protected override Drawable CreateContent() => new GridContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            ColumnDimensions = new[]
            {
                new Dimension(GridSizeMode.AutoSize, maxSize: 200),
                new Dimension(GridSizeMode.Absolute, M.SLIDER_LABEL_GAP),
                new Dimension(),
            },
            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
            Content = new[]
            {
                new Drawable[]
                {
                    new TruncatingSpriteText
                    {
                        Text = entry.Caption,
                        Font = M.Font(M.LABEL_FONT_SIZE),
                        MaxWidth = 200,
                    },
                    Empty(),
                    bar,
                },
            },
        };
    }
}
