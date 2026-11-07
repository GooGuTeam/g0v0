// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osuTK;
using osuTK.Graphics;
using M = osu.Game.Overlays.Settings.Rendering.Stable.StableOptionsMetrics;

namespace osu.Game.Overlays.Settings.Rendering.Stable
{
    /// <summary>
    /// osu!stable's unskinned back button (<c>BackButton</c>): two slanted pink layers in the bottom-left corner, the left one holding
    /// a beat-pulsing chevron and the right one the word "back". Hovering slides both layers out and darkens the left one.
    /// </summary>
    /// <remarks>
    /// Stable draws its <c>back-button-layer</c> texture; the shape is reproduced procedurally here instead of shipping that texture.
    /// </remarks>
    public partial class StableBackButton : OsuClickableContainer
    {
        private readonly Container layerLeft;
        private readonly Container layerRight;
        private readonly Box layerLeftFill;
        private readonly Container icon;
        private readonly OsuSpriteText text;

        private bool expanded;

        public StableBackButton()
        {
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            Size = new Vector2(M.BACK_RIGHT_EXPANDED + M.BACK_LAYER_WIDTH, M.BACK_LAYER_BOTTOM + M.BACK_LAYER_HEIGHT);

            Children = new Drawable[]
            {
                layerRight = createLayer(out _),
                text = new OsuSpriteText
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.Centre,
                    Y = -M.BACK_CONTENT_Y,
                    Text = CommonStrings.Back.ToLower(),
                    Font = M.Font(M.BACK_FONT_SIZE),
                },
                layerLeft = createLayer(out layerLeftFill),
                icon = new PulsingIcon
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.Centre,
                    Y = -M.BACK_CONTENT_Y,
                    Size = new Vector2(M.BACK_ICON_SIZE),
                    IsExpanded = () => expanded,
                    Child = new SpriteIcon
                    {
                        RelativeSizeAxes = Axes.Both,
                        Icon = FontAwesome.Solid.ChevronCircleLeft,
                    },
                },
            };
        }

        private static Container createLayer(out Box fill) => new Container
        {
            Anchor = Anchor.BottomLeft,
            Origin = Anchor.BottomRight,
            Y = -M.BACK_LAYER_BOTTOM,
            Size = new Vector2(M.BACK_LAYER_WIDTH, M.BACK_LAYER_HEIGHT),
            Shear = new Vector2(M.BACK_LAYER_SHEAR, 0),
            Children = new Drawable[]
            {
                // the texture's soft shadow below the body: 20% black fading out over 4 units.
                new Box
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.TopLeft,
                    RelativeSizeAxes = Axes.X,
                    Height = M.BACK_LAYER_SHADOW,
                    Colour = ColourInfo.GradientVertical(Color4.Black.Opacity(0.2f), Color4.Black.Opacity(0)),
                    EdgeSmoothness = new Vector2(1),
                },
                // a masked container would leave the slanted edge aliased; edge smoothing antialiases it like the texture.
                fill = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = M.BACK_IDLE,
                    EdgeSmoothness = new Vector2(1),
                },
            },
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();
            contract(Easing.OutElastic, 0);
        }

        // only the right layer (which the left one sits on) takes input, as in stable.
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => layerRight.ReceivePositionalInputAt(screenSpacePos);

        protected override bool OnHover(HoverEvent e)
        {
            expand(Easing.OutElastic, M.BACK_HOVER_DURATION);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            contract(Easing.OutElastic, M.BACK_HOVER_DURATION);
            base.OnHoverLost(e);
        }

        // an open sub-panel would otherwise take the press as an outside click and close itself before this button steps back.
        protected override bool OnMouseDown(MouseDownEvent e)
        {
            base.OnMouseDown(e);
            return true;
        }

        protected override bool OnClick(ClickEvent e)
        {
            bool result = base.OnClick(e);
            contract(Easing.InOutBack, M.BACK_CLICK_DURATION);
            return result;
        }

        /// <summary>
        /// The extra width given to the right layer when the text is wider than stable's 38 units.
        /// </summary>
        private float textAllowance => Math.Max(0, text.DrawWidth - M.BACK_TEXT_ALLOWANCE);

        private void expand(Easing easing, double duration)
        {
            expanded = true;
            moveTo(M.BACK_LEFT_EXPANDED, M.BACK_ICON_EXPANDED, M.BACK_RIGHT_EXPANDED, easing, duration);
            layerLeftFill.FadeColour(M.BACK_ACTIVE, duration, easing);
        }

        private void contract(Easing easing, double duration)
        {
            expanded = false;
            moveTo(M.BACK_LEFT_CONTRACTED, M.BACK_ICON_CONTRACTED, M.BACK_RIGHT_CONTRACTED, easing, duration);
            layerLeftFill.FadeColour(M.BACK_IDLE, duration, easing);
        }

        private void moveTo(float left, float iconX, float right, Easing easing, double duration)
        {
            right += textAllowance;

            // stable positions the sprites by their right edge; the body's bottom-right corner lies BACK_LAYER_INSET inside it.
            layerLeft.MoveToX(left - M.BACK_LAYER_INSET, duration, easing);
            layerRight.MoveToX(right - M.BACK_LAYER_INSET, duration, easing);
            icon.MoveToX(iconX, duration, easing);
            // stable centres the text at (right - left) / 2.4 + left.
            text.MoveToX((right - left) / 2.4f + left, duration, easing);
        }

        /// <summary>
        /// Stable's icon pulses on every beat: it pops to 0.9 (1.5 while hovered) and settles back over three beats.
        /// </summary>
        private partial class PulsingIcon : BeatSyncedContainer
        {
            public Func<bool> IsExpanded = () => false;

            protected override void OnNewBeat(int beatIndex, TimingControlPoint timingPoint, EffectControlPoint effectPoint, ChannelAmplitudes amplitudes)
            {
                base.OnNewBeat(beatIndex, timingPoint, effectPoint, amplitudes);

                double beatLength = timingPoint.BeatLength;

                this.ScaleTo(IsExpanded() ? 1.5f : 0.9f);

                // stable starts the settle a quarter beat early.
                using (BeginDelayedSequence(-beatLength / 4))
                    this.ScaleTo(1, beatLength * 3, Easing.OutElastic);
            }
        }
    }
}
