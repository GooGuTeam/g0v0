// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osuTK;

namespace osu.Game.Screens.Ranking
{
    /// <summary>
    /// A signed score comparison. Unknown values are never presented as a zero improvement.
    /// </summary>
    public partial class V2StatisticChange : CompositeDrawable
    {
        public readonly Bindable<double?> Difference = new Bindable<double?>();
        public readonly BindableBool Pending = new BindableBool();
        public string NumberFormat { get; init; } = "0.00";
        public string Suffix { get; init; } = string.Empty;

        private bool debugPreview;

        public bool DebugPreview
        {
            get => debugPreview;
            set
            {
                debugPreview = value;
                if (IsLoaded)
                    updateDisplay();
            }
        }

        private readonly OsuSpriteText text;
        private readonly LoadingSpinner spinner;

        public V2StatisticChange()
        {
            AutoSizeAxes = Axes.X;
            Height = 14;
            InternalChildren = new Drawable[]
            {
                text = new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Font = OsuFont.Numeric.With(size: 11),
                    Shadow = false,
                },
                spinner = new LoadingSpinner
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Size = new Vector2(10),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Difference.BindValueChanged(_ => updateDisplay(), true);
            Pending.BindValueChanged(_ => updateDisplay(), true);
        }

        private void updateDisplay()
        {
            if (Pending.Value && !DebugPreview)
            {
                text.Text = string.Empty;
                spinner.Show();
                return;
            }

            spinner.Hide();

            double? displayedDifference = DebugPreview ? (Suffix == "×" ? -12 : Suffix == "pp" ? 15.25 : 0.42) : Difference.Value;
            if (displayedDifference is not { } difference || !double.IsFinite(difference))
            {
                text.Text = string.Empty;
                return;
            }

            text.Text = difference.ToString($"+{NumberFormat};-{NumberFormat};+{NumberFormat}") + Suffix;
            text.Colour = difference > 0 ? Colour4.FromHex("#B6FF86") : difference < 0 ? Colour4.FromHex("#ED1B53") : Colour4.White;
        }
    }
}
