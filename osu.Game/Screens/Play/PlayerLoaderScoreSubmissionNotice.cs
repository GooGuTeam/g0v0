// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// A notice displayed by <see cref="PlayerLoader"/> when the player it has loaded will not be able to submit its score.
    /// </summary>
    public partial class PlayerLoaderScoreSubmissionNotice : CompositeDrawable
    {
        private const float width = 460;

        private readonly LocalisableString reason;

        /// <summary>
        /// Invoked when the user has confirmed that they want to start gameplay despite the score not being submitted.
        /// </summary>
        public Action? Confirmed { get; set; }

        /// <param name="reason">Why the score cannot be submitted, shown to the user.</param>
        public PlayerLoaderScoreSubmissionNotice(LocalisableString reason)
        {
            this.reason = reason;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, OverlayColourProvider colourProvider)
        {
            Width = width;
            AutoSizeAxes = Axes.Y;
            Masking = true;
            CornerRadius = 10;
            BorderThickness = 1.5f;
            BorderColour = colours.Red2;

            RoundedButton confirmButton;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background4,
                    Alpha = 0.9f,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(20),
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 10),
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(8, 0),
                                Children = new Drawable[]
                                {
                                    new SpriteIcon
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Size = new Vector2(18),
                                        Icon = FontAwesome.Solid.ExclamationTriangle,
                                        Colour = colours.Red1,
                                    },
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                                        Text = ScoreSubmissionStrings.NotSubmittingScore,
                                    },
                                },
                            },
                            new TextFlowContainer(t => t.Font = OsuFont.GetFont(size: 15))
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Text = LocalisableString.Join("\n", reason, ScoreSubmissionStrings.SubmissionBlockedHint),
                            },
                            confirmButton = new RoundedButton
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 45,
                                Text = GameplayMenuOverlayStrings.Continue,
                            },
                        },
                    },
                },
            };

            confirmButton.Action = () =>
            {
                confirmButton.Enabled.Value = false;
                Confirmed?.Invoke();
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            this.FadeInFromZero(300, Easing.OutQuint);
        }
    }
}
