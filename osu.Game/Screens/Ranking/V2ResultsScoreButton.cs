// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.Leaderboards;
using osu.Game.Overlays;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Scoring;
using osu.Game.Screens.Select;
using osuTK;

namespace osu.Game.Screens.Ranking
{
    public partial class V2ResultsScoreButton : OsuClickableContainer, IHasPopover
    {
        private readonly Func<IEnumerable<ScoreInfo>> scores;
        private readonly Action<ScoreInfo> selectScore;
        private readonly Func<ScoreInfo?>? currentScore;

        public readonly BindableBool LeaderboardVisible = new BindableBool();

        public V2ResultsScoreButton(Func<IEnumerable<ScoreInfo>> scores, Action<ScoreInfo> selectScore, Func<ScoreInfo?>? currentScore = null)
        {
            this.scores = scores;
            this.selectScore = selectScore;
            this.currentScore = currentScore;
            TooltipText = SongSelectStrings.Ranking;
            Action = TogglePopover;
        }

        protected override bool OnHover(HoverEvent e)
        {
            this.FadeColour(Colour4.FromHex("#7CF6FF"), 120);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            this.FadeColour(Colour4.White, 120);
            base.OnHoverLost(e);
        }

        public void TogglePopover()
        {
            var popoverContainer = this.FindClosestParent<PopoverContainer>();
            if (popoverContainer == null)
                return;

            if (popoverContainer.CurrentTarget == this)
                this.HidePopover();
            else
                this.ShowPopover();
        }

        public bool ClosePopover()
        {
            var popoverContainer = this.FindClosestParent<PopoverContainer>();
            if (popoverContainer?.CurrentTarget == this)
            {
                this.HidePopover();
                return true;
            }

            return false;
        }

        public Popover GetPopover() => new ScoresPopover(scores(), selectScore, currentScore?.Invoke(), visible => LeaderboardVisible.Value = visible);

        public partial class ScoresPopover : OsuPopover
        {
            public const float POPOVER_WIDTH = 660;
            public const float MAX_LIST_HEIGHT = 420;

            private readonly Action<bool>? onVisibilityChanged;
            private readonly OsuScrollContainer? scrollContainer;
            private readonly Drawable? selectedRow;

            public ScoresPopover(IEnumerable<ScoreInfo> scores, Action<ScoreInfo> selectScore, ScoreInfo? currentScore = null, Action<bool>? onVisibilityChanged = null)
                : base(false)
            {
                this.onVisibilityChanged = onVisibilityChanged;

                var scoreList = scores.ToList();
                int scoreCount = scoreList.Count;

                float listHeight = scoreCount == 0 ? 160 : Math.Clamp(scoreCount * 56 + 10, 140, MAX_LIST_HEIGHT);

                var flow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 6),
                    Padding = new MarginPadding { Horizontal = 10, Vertical = 8 },
                };

                int rank = 0;
                foreach (var score in scoreList)
                {
                    rank++;
                    bool isSelected = isScoreSelected(score, currentScore);

                    var row = new V2LeaderboardScoreRow(score, rank, isSelected, () =>
                    {
                        selectScore(score);
                        this.HidePopover();
                    });

                    flow.Add(row);

                    if (isSelected)
                        selectedRow = row;
                }

                Drawable contentDrawable;

                if (scoreCount == 0)
                {
                    contentDrawable = new Container
                    {
                        Width = POPOVER_WIDTH,
                        Height = listHeight,
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 8),
                                Shear = -OsuGame.SHEAR,
                                Children = new Drawable[]
                                {
                                    new SpriteIcon
                                    {
                                        Anchor = Anchor.TopCentre,
                                        Origin = Anchor.TopCentre,
                                        Size = new Vector2(36),
                                        Icon = FontAwesome.Solid.Trophy,
                                        Colour = Colour4.FromHex("#7CF6FF").Opacity(0.4f),
                                    },
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.TopCentre,
                                        Origin = Anchor.TopCentre,
                                        Text = LeaderboardStrings.NoRecordsYet,
                                        Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                                        Colour = Colour4.White.Opacity(0.85f),
                                    },
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.TopCentre,
                                        Origin = Anchor.TopCentre,
                                        Text = "暂无排行榜成绩",
                                        Font = OsuFont.Default.With(size: 12),
                                        Colour = Colour4.White.Opacity(0.5f),
                                    }
                                }
                            }
                        }
                    };
                }
                else
                {
                    contentDrawable = scrollContainer = new OsuScrollContainer
                    {
                        Width = POPOVER_WIDTH,
                        Height = listHeight,
                        ScrollbarVisible = true,
                        Child = flow,
                    };
                }

                // Popover body glassmorphism styling
                Body.Masking = true;
                Body.CornerRadius = 14;
                Body.Margin = new MarginPadding(10);
                Body.BorderThickness = 1.5f;
                Body.BorderColour = Colour4.FromHex("#7CF6FF").Opacity(0.25f);
                Body.Shear = OsuGame.SHEAR;
                Body.EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Offset = new Vector2(0, 6),
                    Radius = 24,
                    Colour = Colour4.Black.Opacity(0.6f),
                };

                Content.Padding = new MarginPadding();

                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientVertical(Colour4.FromHex("#1A2024").Opacity(0.96f), Colour4.FromHex("#121619").Opacity(0.98f)),
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            // Header
                            new Container
                            {
                                Width = POPOVER_WIDTH,
                                Height = 46,
                                Padding = new MarginPadding { Horizontal = 18, Top = 12, Bottom = 10 },
                                Children = new Drawable[]
                                {
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(10, 0),
                                        Shear = -OsuGame.SHEAR,
                                        Children = new Drawable[]
                                        {
                                            new SpriteIcon
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Size = new Vector2(16),
                                                Icon = FontAwesome.Solid.Trophy,
                                                Colour = Colour4.FromHex("#7CF6FF"),
                                            },
                                            new OsuSpriteText
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Text = SongSelectStrings.Ranking,
                                                Font = OsuFont.Torus.With(size: 16, weight: FontWeight.Bold),
                                                Colour = Colour4.White,
                                            },
                                        }
                                    },
                                    new Container
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        AutoSizeAxes = Axes.Both,
                                        CornerRadius = 8,
                                        Masking = true,
                                        BorderThickness = 1,
                                        BorderColour = Colour4.FromHex("#7CF6FF").Opacity(0.2f),
                                        Children = new Drawable[]
                                        {
                                            new Box
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Colour = Colour4.Black.Opacity(0.4f),
                                            },
                                            new OsuSpriteText
                                            {
                                                Margin = new MarginPadding { Horizontal = 10, Vertical = 4 },
                                                Text = scoreCount == 1 ? "1 score" : $"{scoreCount} scores",
                                                Font = OsuFont.Default.With(size: 11, weight: FontWeight.SemiBold),
                                                Colour = Colour4.FromHex("#7CF6FF"),
                                                Shear = -OsuGame.SHEAR,
                                            }
                                        }
                                    }
                                }
                            },
                            // Header separator
                            new Box
                            {
                                Width = POPOVER_WIDTH,
                                Height = 1,
                                Colour = ColourInfo.GradientHorizontal(Colour4.FromHex("#7CF6FF").Opacity(0.4f), Colour4.White.Opacity(0.06f)),
                            },
                            // List / Empty placeholder
                            contentDrawable,
                        }
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                if (selectedRow != null && scrollContainer != null)
                {
                    ScheduleAfterChildren(() => scrollContainer.ScrollTo(selectedRow, false));
                }
            }

            private bool wasOpened;

            protected override void PopIn()
            {
                base.PopIn();
                wasOpened = true;
                onVisibilityChanged?.Invoke(true);

                this.ClearTransforms();
                this.FadeIn(220, Easing.OutQuint);

                Body.ScaleTo(0.96f)
                    .Then()
                    .ScaleTo(1f, 320, Easing.OutQuint);
            }

            protected override void PopOut()
            {
                base.PopOut();
                if (wasOpened)
                    onVisibilityChanged?.Invoke(false);

                this.ClearTransforms();
                this.FadeOut(180, Easing.OutQuint);

                Body.ScaleTo(0.96f, 180, Easing.OutQuint);
            }

            private static bool isScoreSelected(ScoreInfo score, ScoreInfo? current)
            {
                if (current == null)
                    return false;

                if (ReferenceEquals(score, current))
                    return true;

                if (score.ID != Guid.Empty && current.ID != Guid.Empty && score.ID == current.ID)
                    return true;

                if (score.OnlineID > 0 && current.OnlineID > 0 && score.OnlineID == current.OnlineID)
                    return true;

                if (score.TotalScore == current.TotalScore
                    && score.User?.OnlineID == current.User?.OnlineID
                    && score.Date == current.Date)
                    return true;

                return false;
            }
        }

        public partial class V2LeaderboardScoreRow : CompositeDrawable
        {
            private readonly ScoreInfo score;
            private readonly int rank;
            private readonly bool isSelected;
            private readonly Action action;

            private Container borderContainer = null!;
            private Box hoverOverlay = null!;

            [Resolved]
            private IAPIProvider api { get; set; } = null!;

            public V2LeaderboardScoreRow(ScoreInfo score, int rank, bool isSelected, Action action)
            {
                this.score = score;
                this.rank = rank;
                this.isSelected = isSelected;
                this.action = action;

                RelativeSizeAxes = Axes.X;
                Height = BeatmapLeaderboardScore.HEIGHT;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                bool isOwnScore = score.User != null && api.LocalUser.Value != null && score.User.OnlineID == api.LocalUser.Value.Id;

                InternalChild = borderContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    CornerRadius = 10,
                    Masking = true,
                    BorderThickness = isSelected ? 2 : 0,
                    BorderColour = Colour4.FromHex("#7CF6FF"),
                    EdgeEffect = isSelected ? new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Glow,
                        Colour = Colour4.FromHex("#7CF6FF").Opacity(0.35f),
                        Radius = 8,
                    } : default,
                    Children = new Drawable[]
                    {
                        new BeatmapLeaderboardScore(score, sheared: true)
                        {
                            Rank = rank,
                            Highlight = isOwnScore ? BeatmapLeaderboardScore.HighlightType.Own : null,
                            Action = action,
                            Shear = Vector2.Zero,
                        },
                        // Background subtle accent tint for selected score
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientHorizontal(Colour4.FromHex("#7CF6FF").Opacity(0.12f), Colour4.Transparent),
                            Alpha = isSelected ? 1 : 0,
                            Depth = 1,
                        },
                        // Left active accent marker bar
                        new Box
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Width = 4,
                            RelativeSizeAxes = Axes.Y,
                            Colour = Colour4.FromHex("#7CF6FF"),
                            Alpha = isSelected ? 1 : 0,
                            Depth = -1,
                        },
                        // Hover overlay
                        hoverOverlay = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Colour4.White,
                            Alpha = 0,
                            Blending = BlendingParameters.Additive,
                            Depth = -2,
                        }
                    }
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (!isSelected)
                {
                    borderContainer.BorderThickness = 1.5f;
                    borderContainer.BorderColour = Colour4.FromHex("#7CF6FF").Opacity(0.5f);
                }
                hoverOverlay.FadeTo(0.04f, 150, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                if (!isSelected)
                {
                    borderContainer.BorderThickness = 0;
                    borderContainer.BorderColour = Colour4.Transparent;
                }
                hoverOverlay.FadeTo(0, 150, Easing.OutQuint);
                base.OnHoverLost(e);
            }
        }
    }
}
