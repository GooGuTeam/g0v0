// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.Footer;
using osuTK;

namespace osu.Game.Screens.Ranking
{
    /// <summary>
    /// Results action with an unsheared label, generous hit area and explicit disabled/hover states.
    /// </summary>
    public partial class V2ResultsButton : OsuClickableContainer
    {
        public LocalisableString Text
        {
            get => label.Text;
            set => label.Text = value;
        }

        private readonly TruncatingSpriteText label;
        private readonly Box highlight;
        private readonly Container foreground;
        private readonly bool compact;

        public V2ResultsButton(LocalisableString text, IconUsage icon, Colour4 colour, bool compact = false, bool iconOnLeft = false, bool iconOnly = false)
        {
            this.compact = compact;
            Height = compact ? 54 : 46;
            TooltipText = iconOnly ? text : default;

            AddRange(new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Shear = iconOnly ? Vector2.Zero : new Vector2(0.14f, 0),
                    CornerRadius = iconOnly ? 17 : 6,
                    Masking = true,
                    BorderThickness = compact || iconOnly ? 0 : 1.2f,
                    BorderColour = colour.Lighten(0.3f),
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = compact ? Colour4.FromHex("#424D4C").Opacity(0.96f) : colour, Alpha = iconOnly ? 0 : 1 },
                        highlight = new Box { RelativeSizeAxes = Axes.Both, Colour = Colour4.White, Alpha = 0 },
                    },
                },
                new Container
                {
                    Name = "Button underline glow",
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.X,
                    X = -7,
                    Width = 0.98f,
                    Height = 2.5f,
                    Shear = new Vector2(0.14f, 0),
                    Masking = true,
                    CornerRadius = 1,
                    Alpha = compact ? 1 : 0,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Glow,
                        Colour = colour.Opacity(0.65f),
                        Radius = 7,
                    },
                    Child = new Box { RelativeSizeAxes = Axes.Both, Colour = colour.Lighten(0.3f) },
                },
                foreground = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = iconOnly ? Anchor.Centre : compact ? Anchor.TopCentre : iconOnLeft ? Anchor.CentreLeft : Anchor.CentreRight,
                            Origin = iconOnly ? Anchor.Centre : compact ? Anchor.TopCentre : iconOnLeft ? Anchor.CentreLeft : Anchor.CentreRight,
                            Position = iconOnly ? Vector2.Zero : compact ? new Vector2(-3, 10) : new Vector2(iconOnLeft ? 14 : -16, 0),
                            Size = new Vector2(iconOnly ? 25 : compact ? 14 : 20),
                            Icon = icon,
                            Colour = compact ? colour : Colour4.White,
                        },
                        label = new TruncatingSpriteText
                        {
                            Anchor = compact ? Anchor.BottomCentre : Anchor.Centre,
                            Origin = compact ? Anchor.BottomCentre : Anchor.Centre,
                            Position = compact ? new Vector2(-3, -10) : new Vector2(iconOnLeft ? 9 : -10, 0),
                            Text = text,
                            Alpha = iconOnly ? 0 : 1,
                            Font = OsuFont.Default.With(size: compact ? 12 : 15, weight: FontWeight.SemiBold),
                            Shadow = false,
                        },
                    },
                },
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Enabled.BindValueChanged(v =>
            {
                foreground.FadeTo(v.NewValue ? 1 : 0.45f, 120);
                if (!v.NewValue)
                    highlight.FadeOut(120);
            }, true);
        }

        protected override void Update()
        {
            base.Update();
            label.MaxWidth = System.Math.Max(1, DrawWidth - (compact ? 14 : 58));
        }

        protected override bool OnHover(HoverEvent e)
        {
            if (Enabled.Value)
                highlight.FadeTo(0.12f, 100);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            highlight.FadeOut(150);
            base.OnHoverLost(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (Enabled.Value)
                highlight.FadeTo(0.3f).Then().FadeTo(0.12f, 180);
            return base.OnClick(e);
        }
    }
}
