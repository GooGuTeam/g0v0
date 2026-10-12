// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Graphics.UserInterfaceV2
{
    public partial class FormFieldCaption : CompositeDrawable, IHasTooltip
    {
        private OsuTextFlowContainer textFlow = null!;

        private LocalisableString caption;

        public LocalisableString Caption
        {
            get => caption;
            set
            {
                caption = value;

                if (IsLoaded)
                    updateDisplay();
            }
        }

        private LocalisableString tooltipText;

        public LocalisableString TooltipText
        {
            get => tooltipText;
            set
            {
                tooltipText = value;

                if (IsLoaded)
                    updateDisplay();
            }
        }

        // Torii: when true, a small "+18" red pill is appended after
        // the tooltip icon. Used for settings that toggle access to
        // adult-rated content — currently the NSFW profile media
        // toggle in Settings → Torii → Interface. The pill is a
        // visual cue only; the enforcement is server-side via
        // UserPreference.profile_media_show_nsfw + apply_nsfw_media_policy.
        private bool showExplicitContentBadge;

        public bool ShowExplicitContentBadge
        {
            get => showExplicitContentBadge;
            init
            {
                showExplicitContentBadge = value;

                if (IsLoaded)
                    updateDisplay();
            }
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = textFlow = new OsuTextFlowContainer(t => t.Font = OsuFont.Style.Caption1)
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updateDisplay();
        }

        private void updateDisplay()
        {
            textFlow.Text = caption;

            if (TooltipText != default)
            {
                textFlow.AddArbitraryDrawable(new SpriteIcon
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Size = new Vector2(10),
                    Icon = FontAwesome.Solid.QuestionCircle,
                    Margin = new MarginPadding { Left = 5 },
                    Y = 1f,
                });
            }

            if (showExplicitContentBadge)
            {
                textFlow.AddArbitraryDrawable(new ExplicitContentBadge
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Margin = new MarginPadding { Left = 6 },
                });
            }
        }

        /// <summary>
        /// Small red "+18" pill rendered inline in the caption flow when
        /// <see cref="ShowExplicitContentBadge"/> is set. Visual cue
        /// flagging that the host setting unlocks adult-rated content.
        /// </summary>
        private partial class ExplicitContentBadge : CompositeDrawable
        {
            public ExplicitContentBadge()
            {
                AutoSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 3.5f;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Red.Opacity(0.85f),
                    },
                    new OsuSpriteText
                    {
                        Text = "+18",
                        Font = OsuFont.Default.With(weight: FontWeight.Bold, size: 10),
                        Colour = Color4.White,
                        Margin = new MarginPadding { Horizontal = 5, Vertical = 1 },
                    },
                };
            }
        }
    }
}
