// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;
using osuTK;
using M = osu.Game.Overlays.Settings.Rendering.Stable.StableOptionsMetrics;

namespace osu.Game.Overlays.Settings.Rendering.Stable
{
    /// <summary>
    /// osu!stable's <c>pCheckbox</c> at size 0.8: <c>circle-empty</c> / <c>circle-full</c> centred at (8, 8), the label at 20 * 0.8.
    /// Toggling pops the circle. Used by the options' checkboxes and by gameplay's visual settings.
    /// </summary>
    public partial class StableCheckboxControl : CompositeDrawable, IHasCurrentValue<bool>
    {
        private static readonly float circle_size = M.CIRCLE_SIZE * M.CHECKBOX_SCALE;

        /// <summary>
        /// <c>circle-empty</c> has a 2px ring at 1x.
        /// </summary>
        private const float ring_thickness = 2 * M.CHECKBOX_SCALE;

        private readonly BindableWithCurrent<bool> current = new BindableWithCurrent<bool>();

        public Bindable<bool> Current
        {
            get => current.Current;
            set => current.Current = value;
        }

        /// <summary>
        /// Whether clicking the checkbox toggles it. The options turn this off, as their whole row toggles it instead.
        /// </summary>
        public bool ToggleOnClick { get; init; } = true;

        private readonly Container circle;
        private readonly Box fill;

        /// <param name="label">The text beside the circle.</param>
        /// <param name="wrapLabel">Whether to fill the available width and wrap the label, as the options do, instead of sizing to it.</param>
        public StableCheckboxControl(LocalisableString label, bool wrapLabel = false)
        {
            OsuTextFlowContainer text;

            if (wrapLabel)
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
            }
            else
                AutoSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                circle = new CircularContainer
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.Centre,
                    X = M.CHECKBOX_CIRCLE_CENTRE,
                    Size = new Vector2(circle_size),
                    Masking = true,
                    BorderThickness = ring_thickness,
                    BorderColour = StableSettingsColours.MAIN,
                    Child = fill = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = StableSettingsColours.MAIN,
                        AlwaysPresent = true,
                    },
                },
                text = new OsuTextFlowContainer(s => s.Font = M.Font(M.Text(15 * M.CHECKBOX_SCALE)))
                {
                    Padding = new MarginPadding { Left = M.CHECKBOX_LABEL_X },
                    Text = label,
                },
            };

            if (wrapLabel)
            {
                text.RelativeSizeAxes = Axes.X;
                text.AutoSizeAxes = Axes.Y;
            }
            else
                text.AutoSizeAxes = Axes.Both;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            current.BindValueChanged(v =>
            {
                fill.Alpha = v.NewValue ? 1 : 0;

                // pCheckbox.UpdateStatus: scale from 1.2 (checked) / 0.8 (unchecked) back to 1 over 350ms.
                circle.ScaleTo(v.NewValue ? 1.2f : 0.8f).ScaleTo(1, M.CHECKBOX_POP_DURATION, Easing.Out);
            });
            fill.Alpha = current.Value ? 1 : 0;
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (!ToggleOnClick || current.Disabled)
                return false;

            current.Value = !current.Value;
            return true;
        }
    }
}
