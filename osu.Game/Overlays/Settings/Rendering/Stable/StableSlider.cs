// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Numerics;
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
        private readonly StableSliderBar<T> bar;

        public StableSlider(SliderEntry<T> entry)
            : base(entry)
        {
            this.entry = entry;

            // like FormSliderBar, the framework slider is created and bound in the constructor so that range changes
            // always reach it, even if this item is unloaded before LoadComplete.
            bar = new StableSliderBar<T>(entry.FormatTooltip)
            {
                RelativeSizeAxes = Axes.Both,
                Current = entry.Current,
            };

            entry.TransferValueOnCommitBindable.BindValueChanged(v => bar.TransferValueOnCommit = v.NewValue, true);

            if (entry.KeyboardStep != null)
                bar.KeyboardStep = entry.KeyboardStep.Value;
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
