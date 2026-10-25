// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking.Statistics;
using osu.Game.Screens.Select;
using osu.Game.Skinning;
using osu.Game.Skinning.Select;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking
{
    /// <summary>
    /// Replaces the results screen's "details" view with osu!stable's ranking panel. Mounted on top of
    /// the <see cref="ResultsScreen"/>: it tracks the visibility of the <see cref="StatisticsPanel"/> (the
    /// state entered by clicking the expanded score panel) and shows the legacy panel instead, hiding the
    /// stock content underneath. It also restyles the screen's chrome to match stable: the stock bottom
    /// bar (grey backing, with reserved height) becomes a floating transparent bar with the same buttons,
    /// and the global back button becomes the legacy skinned one.
    /// </summary>
    public partial class LegacyRankingOverlay : CompositeDrawable
    {
        private readonly ResultsScreen screen;
        private readonly StatisticsPanel statisticsPanel;
        private readonly Drawable scrollContent;
        private readonly Drawable stockBottomPanel;
        private readonly bool autoShowDetails;

        private BlockingLayer panelLayer = null!;
        private Container panelContainer = null!;
        private bool toolbarHidden;

        // The score the panel is already built for: show() can run twice for the same score
        // (pre-built in load + the statistics panel sync), and rebuilding for nothing duplicates the
        // async extra computes (pp, heatmap).
        private ScoreInfo? builtScore;

        // stable's entrance animation only runs on the FIRST build after gameplay; rebuilds for a score
        // change show everything instantly.
        private bool pendingEntrance;

        // True when the post-gameplay direct mode really became active (autoShowDetails and there was a
        // score at build time): the stock chrome is never seen and back leaves the screen.
        private bool directModeActive;

        private OsuScrollContainer? currentScroll;
        private Drawable? detailsHint;
        private bool hintVisible = true;

        // "Page 1" (the ranking panel) matches the viewport's REAL height every frame: the
        // DrawSizePreserving space can be taller than 768 (16:10 windows, etc) and with a fixed height the
        // next page's banner peeked in without scrolling.
        private Container? panelPage;

        // The floating button bar (render/note/collection/favourite) gets in the way of the stable panel at
        // the very top: it only shows once the view is scrolled (or when the panel is closed).
        private Container floatingBar = null!;
        private bool floatingBarVisible = true;

        // Other players' scores have no page below (the LOCAL/ONLINE/BEATMAP bands are about the local
        // user): without a below-fold the hint is pointless and the floating bar stays put.
        private bool hasBelowFold;

        [Resolved]
        private Online.API.IAPIProvider apiProvider { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        private OsuGame? game;

        public LegacyRankingOverlay(ResultsScreen screen, StatisticsPanel statisticsPanel, Drawable scrollContent, Drawable stockBottomPanel, bool autoShowDetails)
        {
            this.screen = screen;
            this.statisticsPanel = statisticsPanel;
            this.scrollContent = scrollContent;
            this.stockBottomPanel = stockBottomPanel;
            this.autoShowDetails = autoShowDetails;

            pendingEntrance = autoShowDetails;
        }

        [BackgroundDependencyLoader(true)]
        private void load(OsuGame? game)
        {
            this.game = game;

            RelativeSizeAxes = Axes.Both;

            var buttons = new FillFlowContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Spacing = new Vector2(5),
                Direction = FillDirection.Horizontal,
            };

            // The collection/favourite buttons, without the stock bar's grey backing.
            // retry and watch-replay do NOT go here: they live IN the legacy panel (under the grade), as in stable.
            if (screen.Score?.BeatmapInfo != null)
                buttons.Add(new CollectionButton(screen.Score.BeatmapInfo));

            if (screen.Score?.BeatmapInfo?.BeatmapSet != null && screen.Score.BeatmapInfo.BeatmapSet.OnlineID > 0)
                buttons.Add(new FavouriteButton(screen.Score.BeatmapInfo.BeatmapSet));

            // The floating bar's collections button opens a popover: this overlay lives OUTSIDE the
            // screen's PopoverContainer (it's added to the screen with AddInternal because of its depth),
            // so it needs its own at the root. Full-screen, like the screen's, so the popover positions
            // itself as it would in the stock layout (ShowPopover throws if there's no PopoverContainer above).
            InternalChild = new PopoverContainer
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    // The legacy ranking view, toggled with the statistics panel state.
                    panelLayer = new BlockingLayer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4.Black,
                                Alpha = 0.8f,
                            },
                            new DrawSizePreservingFillContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                TargetDrawSize = new Vector2(1366, 768),
                                Strategy = DrawSizePreservationStrategy.Minimum,
                                Child = panelContainer = new Container { RelativeSizeAxes = Axes.Both },
                            },
                        },
                    },
                    // Centred floating buttons (visibility handled in Update) + the skinned back button.
                    floatingBar = new Container
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = TwoLayerButton.SIZE_EXTENDED.Y,
                        Child = buttons,
                    },
                    new DrawSizePreservingFillContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        TargetDrawSize = new Vector2(1366, 768),
                        Strategy = DrawSizePreservationStrategy.Minimum,
                        Child = new LegacyBackButton
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            // The same two-stage as Esc: OnBackButton closes the details view when
                            // applicable (browse mode); in stable direct mode it returns false and we leave
                            // straight away (the stock score carousel is never seen).
                            Action = () =>
                            {
                                if (!screen.OnBackButton())
                                    screen.Exit();
                            },
                        },
                    },
                },
            };

            // Post-gameplay: the legacy panel is built and VISIBLE right here, still on the screen's load
            // thread (scrollContent is part of the same subtree being loaded, mutating it is legal) - the
            // results screen's first frame is already stable's ranking, with no flash of lazer's chrome.
            if (autoShowDetails)
            {
                var score = screen.SelectedScore.Value ?? screen.Score;

                if (score != null)
                {
                    directModeActive = true;
                    rebuildPanel(score);
                    panelLayer.Alpha = 1;
                    scrollContent.Alpha = 0;

                    // Start with the panel in view: the floating bar waits for the scroll.
                    floatingBar.Alpha = 0;
                    floatingBarVisible = false;
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            // The screen's entrance animation fades the stock bar back in; its children (the buttons we
            // recreate here) would otherwise draw at zero height.
            if (stockBottomPanel.Alpha > 0)
                stockBottomPanel.Alpha = 0;

            // Page 1 of the scroll = the viewport's real height (see the field's comment).
            if (panelPage != null && currentScroll != null && Math.Abs(panelPage.Height - currentScroll.DrawHeight) > 0.1f)
                panelPage.Height = currentScroll.DrawHeight;

            // If the screen exits with the details view open, leave the toolbar alone: OsuGame re-shows it
            // only on normal screen changes, and the legacy song select hides it on its own. Re-showing it
            // here raced against the destination screen and left it stuck visible.
            if (toolbarHidden && !screen.IsCurrentScreen())
                toolbarHidden = false;

            // The scroll hint disappears as soon as the view is scrolled (like stable's "Online Ranking",
            // hidden once ScrollPosition.Y > 20) and comes back at the very top.
            if (detailsHint != null && currentScroll != null && hasBelowFold)
            {
                bool shouldShow = currentScroll.Current <= 32;

                if (shouldShow != hintVisible)
                {
                    hintVisible = shouldShow;
                    detailsHint.FadeTo(shouldShow ? 1 : 0, 200);
                }
            }

            // The floating button bar covered the hint and made the stable panel look dirty: with the panel
            // in view it only appears once scrolled to the details (complementing the hint); with the panel
            // closed (browse mode over the stock carousel) or without a page below (someone else's score)
            // it stays put.
            bool barShouldShow = panelLayer.Alpha < 0.5f || !hasBelowFold || (currentScroll != null && currentScroll.Current > 300);

            if (barShouldShow != floatingBarVisible)
            {
                floatingBarVisible = barShouldShow;
                floatingBar.FadeTo(barShouldShow ? 1 : 0, 200);
            }
        }

        /// <summary>
        /// Back in stable direct mode: if the view is scrolled into the extras, go back to the top first
        /// (like stable's Esc closing the online ranking dialog); returns false once at the top, and then
        /// the screen leaves straight away.
        /// </summary>
        public bool HandleBackScroll()
        {
            if (currentScroll != null && currentScroll.Current > 32)
            {
                currentScroll.ScrollToStart();
                return true;
            }

            return false;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // stable's look: no pink lazer back button, and the stock bottom bar neither reserves layout
            // height (its grid row is auto-sized) nor draws. This goes HERE and not in load(): touching the
            // global footer state from the async loader thread is an illegal mutation
            // (InvalidThreadForMutationException). LoadComplete always runs on the update thread.
            screen.SetFooterBackButtonVisible(false);
            stockBottomPanel.Height = 0;

            if (directModeActive)
            {
                // Post-gameplay: the legacy panel has been visible since the first frame (built in load()).
                // Here we only sync the screen's state (detach the stock panel, dim the background) so
                // SelectedScore and friends behave as if the details view had been opened - all covered by
                // the legacy panel. In this mode we NEVER go back to the stock chrome: the bind ignores the
                // hide, and the screen (LegacyStableDirectMode) makes back leave directly and Select not toggle.
                statisticsPanel.State.BindValueChanged(v =>
                {
                    if (v.NewValue == Visibility.Visible)
                        show();
                });

                screen.LegacyStableDirectMode = true;

                // The sync runs with a short delay: detaching the stock score panel assumes the panel has
                // already settled (the original 500ms delay protected that ordering, it wasn't just
                // cosmetic). Since everything is covered by the legacy panel, the delay isn't visible.
                Scheduler.AddDelayed(() =>
                {
                    if (statisticsPanel.State.Value != Visibility.Visible && (screen.SelectedScore.Value ?? screen.Score) != null)
                        statisticsPanel.ToggleVisibility();
                }, 250);

                // stable plays the applause on entering the ranking. The stock flair (accuracy circle, which
                // was what fired it at the end of its animation) is suppressed in this mode, so we fire it
                // ourselves. Schedule: PlayApplause requires IsCurrentScreen.
                var score = screen.SelectedScore.Value ?? screen.Score;
                if (score != null)
                    Schedule(() => screen.PlayApplause(score.Rank));
            }
            else
            {
                statisticsPanel.State.BindValueChanged(v =>
                {
                    if (v.NewValue == Visibility.Visible)
                        show();
                    else
                        hide();
                }, true);
            }

            // Score change (left/right arrows) with the details view open.
            screen.SelectedScore.BindValueChanged(_ =>
            {
                if (directModeActive || statisticsPanel.State.Value == Visibility.Visible)
                    show();
            });
        }

        private void show()
        {
            var score = screen.SelectedScore.Value ?? screen.Score;

            if (score == null)
                return;

            rebuildPanel(score);

            panelLayer.FadeIn(250, Easing.OutQuint);
            // Hide the entire stock content (statistics/panels) so the legacy panel is left alone over the
            // beatmap background. The toolbar goes too, as in stable.
            scrollContent.FadeOut(120);

            if (game != null)
            {
                game.Toolbar.Hide();
                toolbarHidden = true;
            }
        }

        private void hide()
        {
            panelLayer.FadeOut(200, Easing.OutQuint);
            scrollContent.FadeIn(200);

            if (toolbarHidden)
            {
                game?.Toolbar.Show();
                toolbarHidden = false;
            }
        }

        private void rebuildPanel(ScoreInfo score)
        {
            if (score == builtScore)
                return;

            builtScore = score;

            bool animate = pendingEntrance;
            pendingEntrance = false;

            // Current skin first, with the bundled classic one as fallback (for the score font and the
            // ranking-* textures the skin doesn't ship). The view scrolls like stable's: the ranking panel
            // is the first "page", scrolling down reveals the extras section (pp, accuracy heatmap, beatmap
            // tags). The header (black bar + title + ranking-title) stays FIXED above the scroll, like
            // stable's ViewOffsetImmune elements.
            panelContainer.Child = new SkinProvidingContainer(skins.DefaultClassicSkin)
            {
                RelativeSizeAxes = Axes.Both,
                Child = new SkinProvidingContainer(skins.CurrentSkin.Value)
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        currentScroll = new OsuScrollContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            ScrollbarVisible = false,
                            Child = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Children = new Drawable[]
                                {
                                    panelPage = new Container
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 768,
                                        Child = new LegacyRankingPanel(score, animate),
                                    },
                                    // The stable-style page below (the RankingDialog with the LOCAL / ONLINE /
                                    // BEATMAP bands); empty for someone else's scores.
                                    new LegacyOnlineRanking(score),
                                },
                            },
                        },
                        new HeaderStrip(score),
                        detailsHint = new ScrollHintButton(@"v  Online Ranking  v")
                        {
                            Action = () => currentScroll?.ScrollToEnd(),
                        },
                    },
                },
            };

            hasBelowFold = score.UserID == apiProvider.LocalUser.Value?.OnlineID;
            detailsHint.Alpha = hasBelowFold ? 1 : 0;
            hintVisible = hasBelowFold;
        }

        /// <summary>
        /// While visible, it swallows pointer input so clicks never reach the (hidden) stock content
        /// underneath. The floating buttons and the back button are siblings placed on top, so they keep
        /// working.
        /// </summary>
        private partial class BlockingLayer : Container
        {
            public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
                => Alpha > 0.5f && base.ReceivePositionalInputAt(screenSpacePos);

            protected override bool OnClick(ClickEvent e) => true;
            protected override bool OnHover(HoverEvent e) => true;
            protected override bool OnMouseDown(MouseDownEvent e) => true;
            protected override bool OnScroll(ScrollEvent e) => true;
        }

        /// <summary>
        /// stable's fixed header (ViewOffsetImmune): a 60-unit black bar at 80% with the map title /
        /// creator / player, and the ranking-title hanging in the top right. Doesn't scroll with the panel.
        /// </summary>
        private partial class HeaderStrip : CompositeDrawable
        {
            private readonly ScoreInfo score;

            [Resolved]
            private SkinManager skins { get; set; } = null!;

            public HeaderStrip(ScoreInfo score)
            {
                this.score = score;
            }

            [BackgroundDependencyLoader]
            private void load(ISkinSource skin)
            {
                RelativeSizeAxes = Axes.X;
                Height = 60 * 1.6f;

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black,
                        Alpha = 0.8f,
                    },
                    new OsuSpriteText
                    {
                        Position = new Vector2(0, 0),
                        Font = LegacyFonts.Get(22 * 1.6f),
                        Shadow = true,
                        Text = $"{score.BeatmapInfo?.Metadata.Artist} - {score.BeatmapInfo?.Metadata.Title} [{score.BeatmapInfo?.DifficultyName}]",
                    },
                    new OsuSpriteText
                    {
                        Position = new Vector2(1.6f, 20 * 1.6f),
                        Font = LegacyFonts.Get(16 * 1.6f),
                        Shadow = true,
                        Text = $"Beatmap by {score.BeatmapInfo?.Metadata.Author.Username}",
                    },
                    new OsuSpriteText
                    {
                        Position = new Vector2(1.6f, 34 * 1.6f),
                        Font = LegacyFonts.Get(16 * 1.6f),
                        Shadow = true,
                        Text = $"Played by {score.User.Username} on {score.Date.ToLocalTime():g}.",
                    },
                    new Sprite
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        X = -20 * 1.6f,
                        Texture = skin.GetTexture(@"ranking-title") ?? skins.DefaultClassicSkin.GetTexture(@"ranking-title"),
                    },
                };
            }
        }

        /// <summary>
        /// stable's scroll hint button ("v Online Ranking v", centred at the bottom): stable's REAL pButton
        /// (button-left/middle/right textures, three-slice, BlueViolet tint), inviting you to scroll to the
        /// section below, hidden while the view isn't at the top.
        /// </summary>
        private partial class ScrollHintButton : OsuClickableContainer
        {
            private readonly string text;

            [Resolved]
            private SkinManager skins { get; set; } = null!;

            public ScrollHintButton(string text)
            {
                this.text = text;

                Anchor = Anchor.BottomCentre;
                Origin = Anchor.BottomCentre;
                Size = new Vector2(200, 30) * 1.6f;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                var left = skins.DefaultClassicSkin.GetTexture(@"button-left");
                var middle = skins.DefaultClassicSkin.GetTexture(@"button-middle");
                var right = skins.DefaultClassicSkin.GetTexture(@"button-right");

                var tint = Color4.BlueViolet;

                // Three-slice like stable's pButton: caps at natural size + stretched middle, all scaled
                // from the texture's native height (71) to the button's.
                float scale = Height / 71f;
                float texSpaceWidth = Width / scale;
                float capWidth = left?.DisplayWidth ?? 16;

                Children = new Drawable[]
                {
                    new Container
                    {
                        Size = new Vector2(texSpaceWidth, 71),
                        Scale = new Vector2(scale),
                        Colour = tint,
                        Children = new Drawable[]
                        {
                            new Sprite { Texture = left },
                            new Sprite
                            {
                                X = capWidth,
                                Size = new Vector2(texSpaceWidth - capWidth * 2, 71),
                                Texture = middle,
                            },
                            new Sprite
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Texture = right,
                            },
                        },
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Font = LegacyFonts.Get(15 * 1.6f),
                        Shadow = true,
                        Text = text,
                    },
                };
            }
        }
    }
}
