// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Numerics;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using Vector2 = osuTK.Vector2;
using M = osu.Game.Overlays.Settings.Rendering.Stable.StableOptionsMetrics;

namespace osu.Game.Overlays.Settings.Rendering.Stable
{
    /// <summary>
    /// osu!stable's <c>pSliderBar</c>: a thin line, solid up to the hollow nub and half transparent after it, with the value in a tooltip.
    /// Used by the options' sliders and by gameplay's visual settings.
    /// </summary>
    public partial class StableSliderBar<T> : SliderBar<T>, IHasCustomTooltip<LocalisableString>
        where T : struct, INumber<T>, IMinMaxValue<T>
    {
        private const float nub_size = M.SLIDER_NUB_SIZE;
        private const float line_height = M.SLIDER_LINE_HEIGHT;

        private readonly Func<T, LocalisableString> formatTooltip;

        private readonly Box leftLine;
        private readonly Box rightLine;
        private readonly Container nub;

        private float normalisedValue;

        /// <param name="formatTooltip">Formats the value for the tooltip.</param>
        public StableSliderBar(Func<T, LocalisableString> formatTooltip)
        {
            this.formatTooltip = formatTooltip;

            RangePadding = nub_size / 2;

            Children = new Drawable[]
            {
                leftLine = new Box
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Height = line_height,
                    Colour = StableSettingsColours.MAIN,
                },
                rightLine = new Box
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Height = line_height,
                    Colour = StableSettingsColours.MAIN,
                    Alpha = 0.5f,
                },
                nub = new CircularContainer
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.Centre,
                    Size = new Vector2(nub_size),
                    Masking = true,
                    BorderThickness = 2,
                    BorderColour = StableSettingsColours.MAIN,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                        AlwaysPresent = true,
                    },
                },
            };
        }

        private T displayValue;

        public LocalisableString TooltipContent => formatTooltip(displayValue);

        protected override void LoadComplete()
        {
            base.LoadComplete();
            CurrentNumber.BindValueChanged(value => displayValue = value.NewValue, true);
            CurrentNumber.BindDisabledChanged(_ => displayValue = Current.Value);
        }

        protected override void OnUserChange(T value)
        {
            base.OnUserChange(value);
            displayValue = value;
        }

        public ITooltip<LocalisableString> GetCustomTooltip() => new StableTooltip();

        protected override void UpdateValue(float value) => normalisedValue = value;

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            float nubX = RangePadding + normalisedValue * UsableWidth;

            nub.X = nubX;

            leftLine.Width = Math.Max(0, nubX - nub_size / 2);
            rightLine.X = nubX + nub_size / 2;
            rightLine.Width = Math.Max(0, DrawWidth - rightLine.X);
        }
    }
}
