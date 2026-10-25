// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.Leaderboards;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Skinning;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;
using CommonStrings = osu.Game.Localisation.CommonStrings;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// The osu!stable style leaderboard panel at the bottom left of the legacy song select UI.
    /// A scope dropdown ("Local Ranking") plus the beatmap's score list (rank badge / player / score /
    /// accuracy), and stable's "selection-norecords" graphic when there is nothing to show. Uses the
    /// shared <see cref="LeaderboardManager"/> only while the legacy UI is active so it doesn't fight
    /// the modern leaderboard's fetches.
    /// </summary>
    public partial class LegacyLeaderboard : CompositeDrawable
    {
        public readonly Bindable<BeatmapLeaderboardScope> Scope = new Bindable<BeatmapLeaderboardScope>(BeatmapLeaderboardScope.Local);

        /// <summary>
        /// Fired by SongSelect (via carousel.ScrollToSelection). Hovering the leaderboard brings the
        /// carousel back to the selected song, as in normal mode. Only fires over the leaderboard's real
        /// region (scores/dropdown/PB), not over the empty area next to it.
        /// </summary>
        public Action? HoverScrollRequested { get; init; }

        private InputManager? inputManager;
        private bool wasLeaderboardHovered;

        // The scope the user wants for maps with an online leaderboard (Global by default when logged
        // in, like stable); maps without one force Local without clobbering this preference.
        private BeatmapLeaderboardScope preferredScope = BeatmapLeaderboardScope.Local;
        private bool updatingScopeProgrammatically;

        // The last criteria we requested: if another component asks the shared LeaderboardManager for
        // something else while we're visible, we reclaim ours once.
        private LeaderboardCriteria? lastFetchedCriteria;
        private bool reclaimedForeignFetch;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        /// <summary>Whether the legacy UI (and therefore this leaderboard) is currently visible.</summary>
        private Bindable<bool> legacyActive = null!;

        [Resolved]
        private LeaderboardManager leaderboardManager { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        private readonly IBindable<LeaderboardScores?> scores = new Bindable<LeaderboardScores?>();

        private OsuScrollContainer scoreScroll = null!;
        private Container scoreContainer = null!;
        private Container personalBest = null!;
        private Sprite noRecords = null!;
        private LoadingSpinner loadingSpinner = null!;

        private const float row_step = 52;

        // In stable the row content reaches ~x258 (time-ago) / x236 (acc) in 480 space; the card is a
        // little wider than that -> ~430 in the x1.6 space.
        private const float list_width = 410;
        private const int max_rows = 8;

        // The list shows max_rows at a time (like stable); anything beyond that scrolls.
        private const float list_top = 150;
        private const float scroll_height = max_rows * row_step;

        // Personal Best sits right below the scroll (it isn't part of it), with a small gap.
        private const float pb_top = list_top + scroll_height + 12;

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                // Drag-scrollable score list. Rows are positioned by hand (Y = i * step) inside the
                // scroll so each one can do its own slide-in without fighting the flow. Fixed height
                // (max_rows) so the bottom left area always keeps the input (the carousel doesn't drag
                // there) and any extra scores scroll.
                scoreScroll = new OsuScrollContainer
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Position = new Vector2(2, list_top),
                    Width = list_width,
                    Height = scroll_height,
                    ScrollbarVisible = false,
                    Child = scoreContainer = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                    },
                },
                loadingSpinner = new LoadingSpinner
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.Centre,
                    Position = new Vector2(2 + list_width / 2, list_top + 180),
                },
                noRecords = new Sprite
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.Centre,
                    Position = new Vector2(2 + list_width / 2, list_top + 150),
                    Texture = skin.GetTexture(@"selection-norecords"),
                    Alpha = 0,
                },
                // Personal Best section, pinned below the scroll (doesn't scroll with the list).
                personalBest = new Container
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Position = new Vector2(2, pb_top),
                    Width = list_width,
                    AutoSizeAxes = Axes.Y,
                },
                // Drawn last so the dropdown's open menu stays above the scores / "no records" graphic.
                new LegacyStableDropdown<BeatmapLeaderboardScope>
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Position = new Vector2(8, 117),
                    Width = 309,
                    AccentColour = new Color4(31 / 255f, 184 / 255f, 255 / 255f, 1f),
                    HoverColour = new Color4(255 / 255f, 102 / 255f, 171 / 255f, 1f),
                    Items = Enum.GetValues<BeatmapLeaderboardScope>(),
                    ItemText = scope => SongSelectStrings.ScopeRanking(scope.GetLocalisableDescription()),
                    Current = { BindTarget = Scope },
                },
            };
        }

        // A full-screen overlay, but only the leaderboard's content (scores / dropdown) should capture
        // input. The empty right-hand side lets input through so the carousel can be dragged.
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            foreach (var child in InternalChildren)
            {
                if (child.ReceivePositionalInputAt(screenSpacePos))
                    return true;
            }

            return false;
        }

        protected override void Update()
        {
            base.Update();

            // When the mouse enters the leaderboard's real region (the same one that captures input) we
            // fire the scroll-to-selection once. The empty area next to it doesn't count (RPIAt returns
            // false there), so that stays for drag-scrolling only.
            bool over = HoverScrollRequested != null
                        && inputManager != null
                        && ReceivePositionalInputAt(inputManager.CurrentState.Mouse.Position);

            if (over && !wasLeaderboardHovered)
                HoverScrollRequested?.Invoke();

            wasLeaderboardHovered = over;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            inputManager = GetContainingInputManager()!;

            legacyActive = config.GetBindable<bool>(OsuSetting.ToriiLegacyFooterUseSkin);

            scores.BindTo(leaderboardManager.Scores);
            scores.BindValueChanged(_ => updateScores());

            // As in stable: when logged in we start on the Global ranking for maps that have an online
            // leaderboard; without login it stays Local.
            preferredScope = api.IsLoggedIn ? BeatmapLeaderboardScope.Global : BeatmapLeaderboardScope.Local;

            beatmap.BindValueChanged(_ =>
            {
                applyScopeForBeatmap();
                refetch();
            });
            ruleset.BindValueChanged(_ => refetch());
            Scope.BindValueChanged(scope =>
            {
                // Only user changes update the preference; forced Local ones (map without an online
                // leaderboard) don't clobber it.
                if (!updatingScopeProgrammatically)
                    preferredScope = scope.NewValue;

                refetch();
            });
            legacyActive.BindValueChanged(active =>
            {
                if (active.NewValue)
                {
                    applyScopeForBeatmap();
                    refetch();
                }
            }, true);

            updateScores();
        }

        // Maps without an online leaderboard (unranked / no online ID): force Local. Otherwise go back
        // to the user's preferred scope.
        private void applyScopeForBeatmap()
        {
            var target = hasOnlineLeaderboard() ? preferredScope : BeatmapLeaderboardScope.Local;

            if (Scope.Value == target)
                return;

            updatingScopeProgrammatically = true;
            Scope.Value = target;
            updatingScopeProgrammatically = false;
        }

        private bool hasOnlineLeaderboard()
        {
            var info = beatmap.Value?.BeatmapInfo;

            if (info == null || info.OnlineID <= 0)
                return false;

            switch (info.BeatmapSet?.Status ?? info.Status)
            {
                case BeatmapOnlineStatus.Ranked:
                case BeatmapOnlineStatus.Approved:
                case BeatmapOnlineStatus.Qualified:
                case BeatmapOnlineStatus.Loved:
                    return true;

                default:
                    return false;
            }
        }

        private void refetch()
        {
            // Only take over the leaderboard while the legacy UI is visible; otherwise leave it to the
            // modern leaderboard so the fetches don't clobber each other.
            if (!legacyActive.Value)
                return;

            lastFetchedCriteria = new LeaderboardCriteria(beatmap.Value?.BeatmapInfo, ruleset.Value, Scope.Value, null);
            reclaimedForeignFetch = false;
            leaderboardManager.FetchWithCriteria(lastFetchedCriteria);
        }

        private void updateScores()
        {
            if (IsDisposed)
                return;

            // Foreign fetch: another component asked the shared LeaderboardManager for something else
            // while the legacy UI is still active (e.g. a modern mode lookup). Those scores aren't
            // ours: don't display them, and reclaim our criteria ONCE (no loop: if the other one asks
            // again, it's their turn).
            if (legacyActive.Value && lastFetchedCriteria != null && leaderboardManager.CurrentCriteria != null
                && !leaderboardManager.CurrentCriteria.Equals(lastFetchedCriteria))
            {
                if (!reclaimedForeignFetch)
                {
                    reclaimedForeignFetch = true;
                    Scheduler.AddOnce(() => leaderboardManager.FetchWithCriteria(lastFetchedCriteria));
                }

                return;
            }

            var result = scores.Value;

            // null == a fetch is in flight: clear, show the spinner and wait for the results.
            if (result == null)
            {
                scoreContainer.Clear();
                personalBest.Clear();
                noRecords.FadeOut(80);
                loadingSpinner.Show();
                return;
            }

            loadingSpinner.Hide();
            scoreContainer.Clear();
            personalBest.Clear();
            scoreScroll.ScrollToStart(false);

            var top = result.TopScores;

            if (top.Count == 0)
            {
                noRecords.ClearTransforms();
                noRecords.ScaleTo(0.9f).Then().ScaleTo(1f, 600, Easing.OutQuint);
                noRecords.FadeInFromZero(300, Easing.OutQuint);
                return;
            }

            noRecords.FadeOut(80);

            // Up to 50 (stable's cap); anything past max_rows scrolls inside the list.
            var list = top.Take(50).ToList();
            int cascade = 0;

            for (int i = 0; i < list.Count; i++)
            {
                long? diff = i < list.Count - 1 ? list[i].TotalScore - list[i + 1].TotalScore : null;
                addAnimated(scoreContainer, new LegacyScoreRow(list[i], list[i].User.Username, diff) { Y = i * row_step }, cascade++);
            }

            // Personal Best section, pinned below the scroll (positions are relative to personalBest).
            addAnimated(personalBest, new StrokedLegacyText
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopCentre,
                Position = new Vector2(list_width / 2, 0),
                Text = BeatmapLeaderboardWedgeStrings.PersonalBest,
                Font = LegacyFonts.Get(20, FontWeight.Bold),
            }, cascade++);

            if (result.UserScore != null)
            {
                LocalisableString name = result.UserScore.Position is int pos
                    ? SongSelectStrings.RankOfTotal(pos, result.TotalScores)
                    : result.UserScore.User.Username;

                addAnimated(personalBest, new LegacyScoreRow(result.UserScore, name, null) { Y = 28 }, cascade++);
            }
            else
            {
                // The empty placeholder gets the same dark box, as if a score were there.
                addAnimated(personalBest, new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 50,
                    Y = 28,
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Masking = true,
                            CornerRadius = 4,
                            Child = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = ColourInfo.GradientHorizontal(new Color4(0f, 0f, 0f, 0.5f), new Color4(0f, 0f, 0f, 0.12f)),
                            },
                        },
                        new StrokedLegacyText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = SongSelectStrings.NoPersonalRecord,
                            Font = LegacyFonts.Get(20, FontWeight.Light),
                        },
                    },
                }, cascade++);
            }
        }

        /// <summary>
        /// Adds an element to <paramref name="target"/> with stable's cascading pop-in: it slides in from
        /// the left and fades in, staggered 50ms per element, with an OutBack bounce. The "wave" runs
        /// top to bottom.
        /// </summary>
        private void addAnimated(Container target, Drawable d, int cascadeIndex)
        {
            float targetX = d.X;
            target.Add(d);

            d.MoveToX(targetX - 30).FadeOut();
            d.Delay(cascadeIndex * 50).FadeIn(200, Easing.OutQuint).MoveToX(targetX, 400, Easing.OutBack);
        }

        private partial class LegacyScoreRow : OsuClickableContainer, IHasContextMenu, IHasCustomTooltip<ScoreInfo>
        {
            private readonly ScoreInfo score;
            private readonly LocalisableString displayName;
            private readonly long? scoreDiff;

            private static readonly OverlayColourProvider fallback_colour = new OverlayColourProvider(OverlayColourScheme.Blue);

            [Resolved]
            private ScoreManager scoreManager { get; set; } = null!;

            [Resolved]
            private IAPIProvider api { get; set; } = null!;

            [Resolved(CanBeNull = true)]
            private OverlayColourProvider? colourProvider { get; set; }

            [Resolved(CanBeNull = true)]
            private Bindable<IReadOnlyList<Mod>>? selectedMods { get; set; }

            [Resolved(CanBeNull = true)]
            private ISongSelect? songSelect { get; set; }

            [Resolved(CanBeNull = true)]
            private IDialogOverlay? dialogOverlay { get; set; }

            [Resolved(CanBeNull = true)]
            private OsuGame? game { get; set; }

            private Box hoverHighlight = null!;

            public LegacyScoreRow(ScoreInfo score, LocalisableString displayName, long? scoreDiff)
            {
                this.score = score;
                this.displayName = displayName;
                this.scoreDiff = scoreDiff;
            }

            [BackgroundDependencyLoader]
            private void load(ISkinSource skin)
            {
                RelativeSizeAxes = Axes.X;
                Height = 50;

                if (songSelect?.CanPresentScore == true)
                    Action = () => songSelect.PresentScore(score);

                string modString = string.Join(@",", score.Mods.Select(m => m.Acronym));

                var rightColumn = new FillFlowContainer
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -10,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 1),
                };

                if (modString.Length > 0)
                    rightColumn.Add(shadowed(modString, 14, FontWeight.Bold, Anchor.TopRight));

                rightColumn.Add(shadowed($"{score.Accuracy:0.00%}", 16, FontWeight.Bold, Anchor.TopRight));
                rightColumn.Add(shadowed(scoreDiff is long d ? $"+{d:#,0}" : @"-", 13, FontWeight.Regular, Anchor.TopRight, new Color4(0.82f, 0.86f, 0.9f, 1f)));

                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 4,
                        Children = new Drawable[]
                        {
                            // Lighter than before; stable's row is ~black 0.47, fading out to the right.
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = ColourInfo.GradientHorizontal(new Color4(0f, 0f, 0f, 0.5f), new Color4(0f, 0f, 0f, 0.12f)),
                            },
                            hoverHighlight = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4.White,
                                Alpha = 0,
                            },
                        },
                    },
                    // stable's order: avatar first (all the way left), then the grade badge, then the name.
                    new UpdateableAvatar(score.User, isInteractive: false)
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 6,
                        Size = new Vector2(44),
                        Masking = true,
                        CornerRadius = 4,
                    },
                    // FillMode only applies with RelativeSizeAxes = Both; on a fixed-size sprite it is a
                    // no-op and the non-square rank badges came out stretched horizontally. Wrap it in a
                    // fixed slot and let the sprite fit inside instead.
                    new Container
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 56,
                        Size = new Vector2(44),
                        Child = new Sprite
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            RelativeSizeAxes = Axes.Both,
                            FillMode = FillMode.Fit,
                            Texture = skin.GetTexture($@"ranking-{score.Rank}-small"),
                        },
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 114,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            shadowed(displayName, 25, FontWeight.Regular, Anchor.TopLeft),
                            shadowed(LocalisableString.Interpolate($"{BeatmapLeaderboardWedgeStrings.Score}: {score.TotalScore:#,0} ({score.MaxCombo:#,0}x)"), 17, FontWeight.Regular, Anchor.TopLeft),
                        },
                    },
                    rightColumn,
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverHighlight.FadeTo(0.1f, 100, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hoverHighlight.FadeOut(250, Easing.OutQuint);
                base.OnHoverLost(e);
            }

            ITooltip<ScoreInfo> IHasCustomTooltip<ScoreInfo>.GetCustomTooltip() => new BeatmapLeaderboardScore.LeaderboardScoreTooltip(colourProvider ?? fallback_colour);

            ScoreInfo IHasCustomTooltip<ScoreInfo>.TooltipContent => score;

            MenuItem[] IHasContextMenu.ContextMenuItems
            {
                get
                {
                    var items = new List<MenuItem>();

                    // System mods are never copied.
                    var copyableMods = score.Mods.Where(m => m.Type != ModType.System).ToArray();

                    if (copyableMods.Length > 0 && selectedMods != null)
                        items.Add(new OsuMenuItem(SongSelectStrings.UseTheseMods, MenuItemType.Highlighted, () => selectedMods.Value = copyableMods));

                    if (score.OnlineID > 0)
                        items.Add(new OsuMenuItem(CommonStrings.CopyLink, MenuItemType.Standard, () => game?.CopyToClipboard($@"{api.Endpoints.WebsiteUrl}/scores/{score.OnlineID}")));

                    if (score.Files.Count <= 0)
                        return items.ToArray();

                    if (items.Count > 0)
                        items.Add(new OsuMenuItemSpacer());

                    if (songSelect?.CanPresentScore == true)
                        items.Add(new OsuMenuItem(SongSelectStrings.WatchReplay, MenuItemType.Standard, () => songSelect.PresentScore(score, ScorePresentType.Gameplay)));

                    items.Add(new OsuMenuItem(CommonStrings.Export, MenuItemType.Standard, () => scoreManager.Export(score)));
                    items.Add(new OsuMenuItem(Resources.Localisation.Web.CommonStrings.ButtonsDelete, MenuItemType.Destructive, () => dialogOverlay?.Push(new LocalScoreDeleteDialog(score))));

                    return items.ToArray();
                }
            }

            // stable style: text with a thin dark outline so it reads over light or busy backgrounds
            // (stable outlines the text instead of applying a drop shadow).
            private static StrokedLegacyText shadowed(LocalisableString text, float size, FontWeight weight, Anchor anchor, Color4? colour = null) => new StrokedLegacyText
            {
                Anchor = anchor,
                Origin = anchor,
                Text = text,
                Font = LegacyFonts.Get(size, weight),
                Colour = colour ?? Color4.White,
            };
        }
    }
}
