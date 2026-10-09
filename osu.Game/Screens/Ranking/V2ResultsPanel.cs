// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Overlays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Ranking.Expanded;
using osu.Game.Screens.Ranking.Expanded.Accuracy;
using osu.Game.Screens.Ranking.Expanded.Statistics;
using osu.Game.Screens.Ranking.Statistics;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using osuTK;
using SongSelect = osu.Game.Screens.Select.SongSelect;

namespace osu.Game.Screens.Ranking
{
    /// <summary>
    /// Score overview on a 1280 x 610 design surface.
    /// The left column (wedges + score content) is pinned to the left screen edge and the right column
    /// (accuracy circle) to the right edge, so the layout stretches with the aspect ratio the same way song select does.
    /// Visual language (shear, colour provider, wedge corner radius / hide offset, gradients, enter timing) follows <see cref="SongSelect"/>.
    /// </summary>
    public partial class V2ResultsPanel : CompositeDrawable
    {
        // Geometry. The wedge right edge at screen-y is: wedge_x + wedge_width - OsuGame.SHEAR.X * y.
        private const float wedge_corner_radius = 10;
        private const float wedge_gap = 4;
        private const float wedge_x = -60;
        private const float wedge_width = 680;
        private const float header_height = 162;
        private const float difficulty_strip_y = 128;

        // Enter animation.
        private const double enter_duration = SongSelect.ENTER_DURATION;
        private const float left_slide_offset = 150;
        private const float footer_slide_offset = 60;
        private const float right_slide_offset = 100;

        private readonly ScoreInfo score;
        private readonly bool withFlair;
        public readonly Bindable<ScoreInfo?> ComparisonScore = new Bindable<ScoreInfo?>();

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);

        private readonly List<StatisticDisplay> statistics = new List<StatisticDisplay>();
        private TotalScoreCounter scoreCounter = null!;
        private OsuSpriteText positionText = null!;
        private Container scoreArea = null!;
        private readonly BindableBool isPersonalBest = new BindableBool();
        private FillFlowContainer mods = null!;
        private ClickableUsername username = null!;
        private Drawable featuredArtist = null!;
        private GetBeatmapSetRequest? metadataRequest;

        private Box dimmer = null!;
        private Container leftColumn = null!;
        private Container footerStrip = null!;
        private Container rightColumn = null!;
        private Container detailsPanel = null!;
        private bool detailsVisible;
        private Sample? appearanceSample;

        public bool CloseDetails()
        {
            if (!detailsVisible)
                return false;

            setDetailsVisible(false);
            return true;
        }

        public void ShowDetails() => setDetailsVisible(true);

        private void setDetailsVisible(bool visible)
        {
            if (detailsVisible == visible)
                return;

            detailsVisible = visible;
            rightColumn.ClearTransforms();
            detailsPanel.ClearTransforms();

            if (detailsVisible)
            {
                rightColumn.MoveToX(760, 300, Easing.InQuint).FadeOut(250);
                detailsPanel.MoveToX(760).FadeOut();
                detailsPanel.Delay(180).MoveToX(0, 400, Easing.OutQuint).FadeIn(250);
            }
            else
            {
                detailsPanel.MoveToX(760, 300, Easing.InQuint).FadeOut(250);
                rightColumn.Delay(180).MoveToX(0, 400, Easing.OutQuint).FadeIn(250);
            }
        }

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        public V2ResultsPanel(ScoreInfo score, bool withFlair = false)
        {
            this.score = score;
            this.withFlair = withFlair;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ResultsScreen? resultsScreen, OsuColour colours, AudioManager audio)
        {
            appearanceSample = audio.Samples.Get(@"Results/score-panel-top-appear");

            if (resultsScreen != null)
            {
                isPersonalBest.BindTo(resultsScreen.IsPersonalBest);
                ComparisonScore.BindTo(resultsScreen.ComparisonScore);
            }

            var beatmap = score.BeatmapInfo!;
            var metadata = beatmap.BeatmapSet?.Metadata ?? beatmap.Metadata;
            var ruleset = score.Ruleset.CreateInstance();

            // Theme colours: everything comes from the provider / star rating, nothing is hard-coded except semantic colours.
            var difficultyColour = colours.ForStarDifficulty(beatmap.StarRating);
            var titleBackground = colourProvider.Background3;
            var stripBackground = colourProvider.Background5;
            var scoreBackground = colourProvider.Background6;
            var coverTint = colourProvider.Background6;
            var separatorColour = colourProvider.Light4.Opacity(0.3f);

            StatisticDisplay[] topStatistics =
            {
                new AccuracyStatistic(score.Accuracy) { UseV2Style = true },
                new ComboStatistic(score.MaxCombo, score.GetMaximumAchievableCombo()) { UseV2Style = true, V2Header = ResultsScreenStrings.Combo },
                new PerformanceStatistic(score) { UseV2Style = true, ComparisonScore = { BindTarget = ComparisonScore } },
            };

            var accuracyChange = new V2StatisticChange { Name = "Personal best accuracy change", Suffix = "%" };
            var comboChange = new V2StatisticChange { Name = "Personal best combo change", NumberFormat = "0", Suffix = "×" };
            ComparisonScore.BindValueChanged(v =>
            {
                accuracyChange.Difference.Value = (score.Accuracy - v.NewValue?.Accuracy) * 100;
                comboChange.Difference.Value = score.MaxCombo - v.NewValue?.MaxCombo;
            }, true);

            // Keep each ruleset's own judgement vocabulary and optional statistics.
            var hitStatistics = score.GetStatisticsForDisplay().Select(s => new HitResultStatistic(s)
            {
                UseV2Style = true,
                V2ValueScale = s.Result > HitResult.Perfect ? 1.25f : 1.85f,
                V2Suffix = s.Result <= HitResult.Perfect ? "×" : string.Empty,
                V2Header = score.Ruleset.OnlineID == 0 ? v2HeaderFor(s.Result) : null,
            }).ToArray();
            statistics.AddRange(topStatistics);
            statistics.AddRange(hitStatistics);

            // ------------------------------------------------------------------------------------------------
            // Skeleton: dimmer + scaled surface (left column pinned left, right column pinned right).
            // ------------------------------------------------------------------------------------------------

            leftColumn = new Container
            {
                RelativeSizeAxes = Axes.Y,
                Width = 700,
            };

            rightColumn = new Container
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                RelativeSizeAxes = Axes.Y,
                Width = 473,
                Children = new Drawable[]
                {
                    new Container
                    {
                        Name = "Accuracy circle",
                        Position = new Vector2(0, 118),
                        Size = new Vector2(414),
                        Child = new AccuracyCircle(score, withFlair)
                        {
                            UseV2Style = true,
                            RelativeSizeAxes = Axes.Both,
                        },
                    },
                    new V2ResultsButton(ResultsScreenStrings.MoreInfo, FontAwesome.Regular.ArrowAltCircleRight, Colour4.White, iconOnly: true)
                    {
                        Name = "Show score details",
                        Position = new Vector2(416, 298),
                        Size = new Vector2(48),
                        Action = ShowDetails,
                    },
                },
            };

            detailsPanel = new Container
            {
                Name = "Score details popup",
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(760, 0),
                RelativeSizeAxes = Axes.Both,
                Width = 1,
                Padding = new MarginPadding { Left = 610, Right = 20, Top = 20, Bottom = 45 },
                Alpha = 0,
                Children = new Drawable[]
                {
                    text(20, 16, ResultsScreenStrings.MoreInfo, 18, FontWeight.Bold),
                    new V2ResultsButton(CommonStrings.Back, FontAwesome.Solid.Times, Colour4.White, iconOnly: true)
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Name = "Close score details",
                        Position = new Vector2(-6, 4),
                        Size = new Vector2(48),
                        Action = () => CloseDetails(),
                    },
                    new BasicScrollContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Top = 48, Bottom = 12 },
                        Child = new StatisticsPanel(embedded: true)
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Score = { Value = score },
                            AchievedScore = resultsScreen?.Score,
                            State = { Value = Visibility.Visible },
                        },
                    },
                },
            };

            InternalChildren = new Drawable[]
            {
                // Same idea as song select: darker on the left (behind wedges) and on the right (behind the circle),
                // and it keeps going beneath the footer instead of ending at the buttons' top edge.
                dimmer = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Height = 1.25f,
                    Colour = ColourInfo.GradientHorizontal(Colour4.Black.Opacity(0.3f), Colour4.Black.Opacity(0.5f)),
                },
                new DrawSizePreservingFillContainer
                {
                    TargetDrawSize = new Vector2(1280, 610),
                    Strategy = DrawSizePreservationStrategy.Minimum,
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[] { leftColumn, rightColumn, detailsPanel },
                    },
                },
            };

            // ------------------------------------------------------------------------------------------------
            // User strip (bottom-left): slides in slightly after the wedges.
            // ------------------------------------------------------------------------------------------------

            positionText = text(10, 13, string.Empty, 14, FontWeight.SemiBold);

            footerStrip = new Container
            {
                Name = "User strip",
                Y = 10,
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Container
                    {
                        Name = "User strip background",
                        Position = new Vector2(-8, 570),
                        Size = new Vector2(314, 40),
                        Shear = OsuGame.SHEAR,
                        CornerRadius = 5,
                        Masking = true,
                                                        EdgeEffect = new EdgeEffectParameters
                                {
                                    Type = EdgeEffectType.Shadow,
                                    Colour = Colour4.Black.Opacity(0.3f),
                                    Radius = 16,
                                    Offset = new Vector2(5, 4),
                                },
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientHorizontal(titleBackground.Opacity(0.95f), scoreBackground.Opacity(0.7f)),
                        },
                    },
                    new Container
                    {
                        Name = "User cover",
                        Position = new Vector2(97, 570),
                        Size = new Vector2(207, 40),
                        Shear = OsuGame.SHEAR,
                        Masking = true,
                        CornerRadius = 4,
                        Children = new Drawable[]
                        {
                            new UserCoverBackground { RelativeSizeAxes = Axes.Both, User = score.User, Alpha = 0.5f },
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = ColourInfo.GradientHorizontal(coverTint.Opacity(0.5f), coverTint.Opacity(0.15f)),
                            },
                        },
                    },
                    new V2ResultsScoreButton(
                        () => resultsScreen?.GetOverviewScores() ?? new[] { score },
                        selected => resultsScreen?.SelectOverviewScore(selected))
                    {
                        Name = "Score selection button",
                        Position = new Vector2(0, 570),
                        Size = new Vector2(56, 40),
                        Children = new Drawable[] { positionText },
                    },
                    new UpdateableAvatar(score.User)
                    {
                        Position = new Vector2(60, 570),
                        Size = new Vector2(40),
                        CornerRadius = 7,
                        Masking = true,
                    },
                    username = new ClickableUsername(score.User, useV2Style: true)
                    {
                        Position = new Vector2(106, 573),
                        Scale = Vector2.One,
                    },
                    new TruncatingSpriteText
                    {
                        Position = new Vector2(106, 593),
                        MaxWidth = 190,
                        Text = ResultsScreenStrings.AchievedOn(score.Date.ToLocalTime().ToString("yyyy/MM/dd HH:mm")),
                        Font = OsuFont.Default.With(size: 10),
                        Shadow = false,
                    },
                    new V2PerformanceChange(score)
                    {
                        Position = new Vector2(106, 545),
                        Width = 198,
                    },
                    text(323, 554, ResultsScreenStrings.Mods, 12, FontWeight.Bold).With(d =>
                    {
                        d.Name = "Mods header";
                        d.Alpha = score.Mods.Length > 0 ? 1 : 0;
                    }),
                    new Container
                    {
                        Name = "Mods area",
                        Position = new Vector2(320, 573),
                        Size = new Vector2(255, 36),
                        Masking = true,
                        Child = mods = new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(5, 0),
                        },
                    },
                },
            };

            // ------------------------------------------------------------------------------------------------
            // Left column content.
            // ------------------------------------------------------------------------------------------------

            leftColumn.AddRange(new Drawable[]
            {
                // Two independent wedges (title + score) separated by a 4px gap, like song select.
                // Shear is applied once on the parent so both wedges share one continuous slanted edge.
                // The title wedge sits flush with the top of the panel; the left edge is off-screen by construction.
                new Container
                {
                    Name = "Wedges",
                    Position = new Vector2(wedge_x, 0),
                    RelativeSizeAxes = Axes.Y,
                    Width = wedge_width,
                    Shear = OsuGame.SHEAR,
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            Name = "Title wedge",
                            Y = 0,
                            RelativeSizeAxes = Axes.X,
                            Height = header_height - 17,
                            CornerRadius = wedge_corner_radius,
                            Masking = true,
                            EdgeEffect = new EdgeEffectParameters
                            {
                                Type = EdgeEffectType.Shadow,
                                Colour = Colour4.Black.Opacity(0.3f),
                                Radius = 16,
                                Offset = new Vector2(5, 4),
                            },
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    Name = "Beatmap header background",
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = ColourInfo.GradientHorizontal(titleBackground.Opacity(0.95f), titleBackground.Opacity(0.9f)),
                                },
                            },
                        },
                        new Container
                        {
                            Name = "Score wedge",
                            RelativeSizeAxes = Axes.X,
                            Size = new Vector2(1.02f,553f),
                            Padding = new MarginPadding { Top = header_height + wedge_gap - 17 },
                            Child = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                CornerRadius = wedge_corner_radius,
                                Masking = true,
                                EdgeEffect = new EdgeEffectParameters
                                {
                                    Type = EdgeEffectType.Shadow,
                                    Colour = Colour4.Black.Opacity(0.3f),
                                    Radius = 16,
                                    Offset = new Vector2(5, 4),
                                },
                                Child = new Box
                                {
                                    Name = "Score panel background",
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = ColourInfo.GradientHorizontal(scoreBackground.Opacity(0.9f), scoreBackground.Opacity(0.8f)),
                                },
                            },
                        },
                        new Container
                        {
                            Name = "Difficulty strip background",
                            Size = new Vector2(wedge_width - 20, 34f),
                            Y = difficulty_strip_y,
                            Height = 34,
                            CornerRadius = wedge_corner_radius,
                            Masking = true,
                            EdgeEffect = new EdgeEffectParameters
                            {
                                Type = EdgeEffectType.Shadow,
                                Colour = Colour4.Black.Opacity(0.3f),
                                Radius = 16,
                                Offset = new Vector2(5, 4),
                            },
                            Child = new Box() {RelativeSizeAxes = Axes.Both,                                    Colour = stripBackground.Opacity(1.0f),}
                        },
                    },
                },
                new FillFlowContainer
                {
                    Name = "Status badges",
                    Position = new Vector2(66, 20),
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(4, 0),
                    Children = new Drawable[]
                    {
                        new BeatmapSetOnlineStatusPill
                        {
                            AutoSizeAxes = Axes.Both,
                            Status = beatmap.Status,
                            TextSize = 10,
                            TextPadding = new MarginPadding { Horizontal = 6, Vertical = 1 },
                        },
                        // AlwaysPresent: it is last in the flow, so reserving its slot while hidden doesn't move anything.
                        featuredArtist = badge(0, 0, ResultsScreenStrings.FeaturedArtist, Colour4.FromHex("#111A1C"), 10, Colour4.FromHex("#2FA3D1")).With(d =>
                        {
                            d.Name = "Featured artist";
                            d.Alpha = 0;
                            d.AlwaysPresent = true;
                        }),
                    },
                },
                new TruncatingSpriteText
                {
                    Position = new Vector2(66, 35),
                    MaxWidth = 480,
                    Text = new RomanisableString(metadata.TitleUnicode, metadata.Title),
                    BeatmapTags = metadata.Tags,
                    Font = OsuFont.TorusAlternate.With(size: 32, weight: FontWeight.SemiBold),
                    Shadow = false,
                },
                new TruncatingSpriteText
                {
                    Position = new Vector2(66, 68),
                    MaxWidth = 470,
                    Text = new RomanisableString(metadata.ArtistUnicode, metadata.Artist),
                    BeatmapTags = metadata.Tags,
                    Font = OsuFont.Default.With(size: 22),
                    Shadow = false,
                },
                new CollectionButton(beatmap)
                {
                    Position = new Vector2(146, 94),
                    Size = new Vector2(30, 27),
                    UseV2Style = true,
                },
                new Container
                {
                    Name = "Difficulty indicators",
                    Position = new Vector2(61, difficulty_strip_y),
                    Size = new Vector2(78, 34),
                    Children = new Drawable[]
                    {
                        ruleset.CreateIcon().With(d =>
                        {
                            d.Name = "Difficulty ruleset icon";
                            d.Anchor = d.Origin = Anchor.CentreLeft;
                            d.Size = new Vector2(20);
                            d.Colour = difficultyColour;
                        }),
                        new StarRatingDisplay(new StarDifficulty(beatmap.StarRating, 0), StarRatingDisplaySize.Small)
                        {
                            Name = "Difficulty rating",
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            X = 24,
                        },
                    },
                },
                new TruncatingSpriteText
                {
                    Position = new Vector2(144, 131),
                    MaxWidth = 407,
                    Text = beatmap.DifficultyName,
                    Font = OsuFont.Default.With(size: 15, weight: FontWeight.SemiBold),
                    Colour = difficultyColour,
                    Shadow = false,
                },
                new TruncatingSpriteText
                {
                    Position = new Vector2(144, 146),
                    MaxWidth = 407,
                    Text = ResultsScreenStrings.MappedBy(metadata.Author.Username),
                    Font = OsuFont.Default.With(size: 13),
                    Shadow = false,
                },
                scoreArea = new Container
                {
                    Position = new Vector2(48, 192),
                    Size = new Vector2(412, 101),
                    Child = scoreCounter = new TotalScoreCounter(!withFlair)
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Colour = ColourInfo.GradientVertical(Colour4.White, Colour4.FromHex("#AFE8FF")),
                    },
                },
                new V2ScoreFlair
                {
                    Position = new Vector2(478, 208),
                    Alpha = score.IsPerfectCombo() ? 1 : 0,
                },
                new FillFlowContainer
                {
                    Position = new Vector2(50, 314),
                    Width = 458,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 12),
                    Children = new[]
                    {
                        statisticRow(topStatistics, true, separatorColour, new[] { 0.34f, 0.33f, 0.33f }, new Drawable?[] { accuracyChange, comboChange, null }),
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 12),
                            ChildrenEnumerable = hitStatistics.Where(s => s.Result <= HitResult.Perfect)
                                .Chunk(score.Ruleset.OnlineID == 3 ? 3 : Math.Max(1, hitStatistics.Count(s => s.Result <= HitResult.Perfect)))
                                .Select(s => statisticRow(s, true, separatorColour)),
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Width = 1,
                            Spacing = new Vector2(0, 12),
                            ChildrenEnumerable = optionalStatisticRows(hitStatistics, score.Ruleset.OnlineID == 0)
                                .Select(s => statisticRow(s, false, separatorColour)),
                        },
                    },
                },
                footerStrip,
            });

            var personalBest = new Container
            {
                Position = new Vector2(470, 149),
                Size = new Vector2(178, 27),
                CornerRadius = 5,
                Masking = true,
                Alpha = 0,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientVertical(Colour4.FromHex("#FFE8A3"), Colour4.FromHex("#FFCA17")),
                    },
                    new TruncatingSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        MaxWidth = 166,
                        Text = ResultsScreenStrings.PersonalBest,
                        Font = OsuFont.Default.With(size: 18, weight: FontWeight.Bold),
                        Spacing = new Vector2(0.8f, 0),
                        Colour = Colour4.FromHex("#29342B"),
                        Shadow = false,
                    },
                },
            };
            leftColumn.Add(personalBest);
            isPersonalBest.BindValueChanged(v => personalBest.FadeTo(v.NewValue ? 1 : 0, 150), true);

            if (beatmap.BeatmapSet != null)
            {
                leftColumn.Add(new FavouriteButton(beatmap.BeatmapSet)
                {
                    Position = new Vector2(63, 94),
                    Size = new Vector2(69, 27),
                    ShowCount = true,
                });
            }

            if (score.Mods.Length > 0)
            {
                mods.Add(new ModDisplay(false)
                {
                    Scale = new Vector2(0.87f),
                    ExpansionMode = ExpansionMode.ExpandOnHover,
                    Current = { Value = score.Mods },
                });
            }

            // The columns start hidden, so the panel's first ScheduleAfterChildren can run
            // before the counter's LoadComplete. Initialise from the counter itself instead.
            scoreCounter.OnLoadComplete += _ =>
            {
                using (scoreCounter.BeginDelayedSequence(withFlair ? AccuracyCircle.ACCURACY_TRANSFORM_DELAY : 0))
                    scoreCounter.Current = scoreManager.GetBindableTotalScore(score);

                // Match the classic panel: browsing an existing score must not play a full
                // score-tick-lesser roll-up on every entrance or score selection.
                if (!withFlair)
                    scoreCounter.StopRolling();
            };

            // Initial state for the enter animation (played in LoadComplete).
            dimmer.Alpha = 0;

            leftColumn.Alpha = 0;
            leftColumn.X = -left_slide_offset;

            footerStrip.Alpha = 0;
            footerStrip.X = -footer_slide_offset;

            rightColumn.Alpha = 0;
            rightColumn.X = right_slide_offset;
        }

        private static LocalisableString? v2HeaderFor(HitResult result) => result switch
        {
            HitResult.Great => (LocalisableString?)"300",
            HitResult.Ok => (LocalisableString?)"100",
            HitResult.Meh => (LocalisableString?)"50",
            _ => null,
        };

        private static OsuSpriteText text(float x, float y, LocalisableString value, float size, FontWeight weight = FontWeight.Regular) => new OsuSpriteText
        {
            Position = new Vector2(x, y),
            Text = value,
            Font = OsuFont.Default.With(size: size, weight: weight),
            Shadow = false,
        };

        private static Drawable badge(float x, float y, LocalisableString value, Colour4 colour, float size = 10, Colour4? foreground = null) => new Container
        {
            Position = new Vector2(x, y),
            AutoSizeAxes = Axes.Both,
            CornerRadius = 7,
            Masking = true,
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = colour },
                new OsuSpriteText
                {
                    Margin = new MarginPadding { Horizontal = 6, Vertical = 1 },
                    Text = value,
                    Font = OsuFont.Default.With(size: size, weight: FontWeight.Bold),
                    Colour = foreground ?? Colour4.FromHex("#20322A"),
                    Shadow = false,
                },
            },
        };

        private static IEnumerable<HitResultStatistic[]> optionalStatisticRows(IEnumerable<HitResultStatistic> statistics, bool isStandard)
        {
            var optional = statistics.Where(s => s.Result > HitResult.Perfect).ToArray();
            bool isSpinner(HitResultStatistic s) => s.Result is HitResult.SmallBonus or HitResult.LargeBonus;

            foreach (var row in optional.Where(s => !isStandard || !isSpinner(s)).Chunk(3))
                yield return row;

            if (isStandard)
            {
                var spinner = optional.Where(isSpinner).ToArray();
                if (spinner.Length > 0)
                    yield return spinner;
            }
        }

        private static Drawable statisticRow(IReadOnlyList<StatisticDisplay> displays, bool separators, ColourInfo separatorColour, float[]? widths = null, Drawable?[]? changes = null)
        {
            var row = new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                ColumnDimensions = widths?.Select(w => new Dimension(GridSizeMode.Relative, w)).ToArray() ?? Array.Empty<Dimension>(),
            };
            row.Content = new[]
            {
                displays.Select((s, i) => (Drawable)new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Right = 15 },
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 4),
                            ChildrenEnumerable = changes?[i] is { } change ? new Drawable[] { s, change } : new Drawable[] { s },
                        },
                        new Box
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Position = new Vector2(-8, 4),
                            Size = new Vector2(1.5f, 28),
                            Rotation = 10,
                            Colour = separatorColour,
                            Alpha = separators && i < displays.Count - 1 ? 1 : 0,
                            BypassAutoSizeAxes = Axes.Both,
                        },
                    },
                }).ToArray(),
            };
            return row;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // TrackId is not stored in the local Realm model. Only reveal the badge after an online lookup confirms it.
            if (score.BeatmapInfo?.BeatmapSet is { OnlineID: > 0 } beatmapSet)
            {
                metadataRequest = new GetBeatmapSetRequest(beatmapSet.OnlineID);
                metadataRequest.Success += result => Schedule(() => featuredArtist.FadeTo(result.TrackId.HasValue ? 1 : 0, 150));
                api.Queue(metadataRequest);
            }

            if (scoreCounter.DrawableCount is OsuSpriteText counterText)
            {
                counterText.Font = OsuFont.Numeric.With(size: 118, weight: FontWeight.Light);
                counterText.Spacing = new Vector2(-2, 0);
                counterText.Shadow = false;
                counterText.Anchor = counterText.Origin = Anchor.TopLeft;
            }

            PlayEnter();
        }

        /// <summary>
        /// Mirrors song select's wedge entrance: <see cref="SongSelect.ENTER_DURATION"/> OutQuint slide with a shorter fade.
        /// Left wedges lead, the user strip and the right column follow slightly later, statistics appear once the wedges have mostly landed.
        /// </summary>
        public void PlayEnter()
        {
            if (withFlair)
                appearanceSample?.Play();

            dimmer.FadeIn(enter_duration / 2);

            leftColumn.MoveToX(0, enter_duration, Easing.OutQuint)
                      .FadeIn(enter_duration / 3, Easing.In);

            footerStrip.Delay(enter_duration / 8)
                       .MoveToX(0, enter_duration, Easing.OutQuint)
                       .FadeIn(enter_duration / 3, Easing.In);

            rightColumn.Delay(enter_duration / 6)
                       .MoveToX(0, enter_duration, Easing.OutQuint)
                       .FadeIn(enter_duration / 3, Easing.In);

            ScheduleAfterChildren(() => Scheduler.AddDelayed(() =>
            {
                foreach (var statistic in statistics)
                    statistic.Appear();
            }, enter_duration / 4));
        }

        /// <summary>
        /// Optional exit counterpart. Not called by this panel; hook it up from the results screen's exit transition if desired.
        /// </summary>
        public void PlayExit()
        {
            const double exit_duration = enter_duration / 2;

            dimmer.FadeOut(exit_duration);

            leftColumn.MoveToX(-left_slide_offset, exit_duration, Easing.InQuint)
                      .FadeOut(exit_duration, Easing.Out);

            rightColumn.MoveToX(right_slide_offset, exit_duration, Easing.InQuint)
                       .FadeOut(exit_duration, Easing.Out);
        }

        protected override void Dispose(bool isDisposing)
        {
            metadataRequest?.Cancel();
            base.Dispose(isDisposing);
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            // Scores fit their column. Mods retain a fixed readable scale and are clipped by their viewport.
            scoreCounter.DrawableCount.Scale = new Vector2(Math.Min(1, scoreArea.Width / Math.Max(1, scoreCounter.DrawableCount.DrawWidth)));
            username.Scale = new Vector2(Math.Min(1, 190 / Math.Max(1, username.DrawWidth)));
            positionText.Text = score.Position.HasValue ? $"#{score.Position}" : "—";
            positionText.Scale = new Vector2(Math.Min(1, 44 / Math.Max(1, positionText.DrawWidth)));
        }
    }
}
