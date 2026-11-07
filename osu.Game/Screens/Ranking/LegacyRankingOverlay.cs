// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
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
    /// torii: reemplaza la vista de "detalles" del results con el panel de ranking de osu!stable.
    /// va montado encima del <see cref="ResultsScreen"/>: trackea la visibilidad del
    /// <see cref="StatisticsPanel"/> (el estado al que se entra clickeando el score panel expandido)
    /// y muestra el panel legacy en su lugar, escondiendo el contenido stock de abajo. tambien
    /// re-estiliza el chrome del screen para que quede como stable: la barra de abajo stock se esconde
    /// (sin reservar altura) y el back button global pasa a ser el legacy skineado.
    /// </summary>
    public partial class LegacyRankingOverlay : CompositeDrawable
    {
        private readonly ResultsScreen screen;
        private readonly StatisticsPanel statisticsPanel;
        private readonly Drawable scrollContent;
        private readonly Drawable stockBottomPanel;
        private readonly bool autoShowDetails;

        private BlockingLayer panelLayer = null!;
        private DrawSizePreservingFillContainer panelSpace = null!;
        private Container panelContainer = null!;
        private bool toolbarHidden;

        // el score para el que ya esta armado el panel: show() puede correr dos veces para el
        // mismo score (pre-armado en load + el sync del statistics panel), y rebuildear al pedo
        // duplica los computes async de los extras (pp, heatmap).
        private ScoreInfo? builtScore;

        // la animacion de entrada de stable corre solo en el PRIMER build post-gameplay; los
        // rebuilds por cambio de score muestran todo al instante.
        private bool pendingEntrance;

        // true cuando el modo directo post-gameplay quedo activo de verdad (autoShowDetails y
        // habia score al armar): el chrome stock no se ve nunca y back sale del screen.
        private bool directModeActive;

        private OsuScrollContainer? currentScroll;
        private Drawable? detailsHint;
        private bool hintVisible = true;

        // la "pagina 1" (el ranking panel) matchea la altura REAL del viewport en cada frame:
        // el espacio DrawSizePreserving puede ser mas alto que 768 (ventanas 16:10, etc) y con
        // altura fija se asomaba el banner de la pagina de abajo sin scrollear.
        private Container? panelPage;
        private LegacyRankingPanel? currentRankingPanel;
        private LegacyOnlineRanking? currentOnlineRanking;

        // los scores ajenos no tienen pagina de abajo (las bandas LOCAL/ONLINE/BEATMAP hablan
        // del usuario local): sin below-fold el hint no va.
        private bool hasBelowFold;

        [Resolved]
        private Online.API.IAPIProvider apiProvider { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        private OsuGame? game;
        private Bindable<LegacySongSelectAspectRatio> aspectRatio = null!;

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
        private void load(OsuGame? game, OsuConfigManager config)
        {
            this.game = game;
            aspectRatio = config.GetBindable<LegacySongSelectAspectRatio>(OsuSetting.ToriiLegacySongSelectAspectRatio);

            RelativeSizeAxes = Axes.Both;

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    // la vista de ranking legacy, toggleada con el estado del statistics panel.
                    panelLayer = new BlockingLayer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                        ClickAction = () =>
                        {
                            if (currentRankingPanel?.IsAnimating == true)
                                currentRankingPanel.FinishAnimation();
                        },
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4.Black,
                                Alpha = 0.8f,
                            },
                            // stable scales its results screen by height and lets the width follow the window (see LegacyRankingLayout).
                            panelSpace = new DrawSizePreservingFillContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                TargetDrawSize = new Vector2(LegacyRankingLayout.MIN_WIDTH, LegacyRankingLayout.HEIGHT),
                                Strategy = DrawSizePreservationStrategy.Minimum,
                                Child = panelContainer = new Container { RelativeSizeAxes = Axes.Both },
                            },
                        },
                    },
                    new DrawSizePreservingFillContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        TargetDrawSize = new Vector2(LegacyRankingLayout.MIN_WIDTH, LegacyRankingLayout.HEIGHT),
                        Strategy = DrawSizePreservationStrategy.Minimum,
                        Child = new LegacyBackButton
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            // mismo two-stage que Esc: OnBackButton cierra la vista de detalles si
                            // corresponde (modo browse); en el modo stable directo devuelve false
                            // y salimos de una (nunca se ve el carousel de scores stock).
                            Action = () =>
                            {
                                if (!screen.OnBackButton())
                                    screen.Exit();
                            },
                        },
                    },
                },
            };

            // post-gameplay: el panel legacy queda armado y VISIBLE aca mismo, todavia en el load
            // thread del screen (scrollContent es parte del mismo subtree en carga, mutarlo es
            // legal) — el primer frame del results ya es el ranking de stable, sin ningun flash
            // del chrome de lazer.
            if (autoShowDetails)
            {
                var score = screen.SelectedScore.Value ?? screen.Score;

                if (score != null)
                {
                    directModeActive = true;
                    rebuildPanel(score);
                    panelLayer.Alpha = 1;
                    scrollContent.Alpha = 0;
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            // la animacion de entrada del screen vuelve a fadear la barra stock, que en modo legacy no se ve.
            if (stockBottomPanel.Alpha > 0)
                stockBottomPanel.Alpha = 0;

            // pagina 1 del scroll = altura real del viewport (ver comentario del campo).
            if (panelPage != null && currentScroll != null && Math.Abs(panelPage.Height - currentScroll.DrawHeight) > 0.1f)
                panelPage.Height = currentScroll.DrawHeight;

            if (panelPage != null && currentOnlineRanking != null)
                currentOnlineRanking.Y = panelPage.Height;

            // stable scrolls the online ranking up until its top meets the header (Ranking: ScrollToPosition(HeightScaled * 0.875)), so
            // the page is as tall as the view below the header and the rest of its dialog is cut off.
            if (currentOnlineRanking?.IsShown == true && currentScroll != null)
                currentOnlineRanking.Height = Math.Max(0, currentScroll.DrawHeight - HeaderStrip.HEIGHT);

            // si el screen sale con la vista de detalles abierta, dejamos la toolbar en paz:
            // OsuGame la re-muestra solo en los cambios de screen normales, y el song select
            // legacy la esconde por su cuenta. re-mostrarla aca corria una carrera contra el
            // screen de destino y la dejaba trabada visible.
            if (toolbarHidden && !screen.IsCurrentScreen())
                toolbarHidden = false;

            // el hint de scroll desaparece apenas se scrollea (como el "Online Ranking" de
            // stable, que se esconde con ScrollPosition.Y > 20) y vuelve al subir del todo.
            if (detailsHint != null && currentScroll != null && hasBelowFold)
            {
                bool shouldShow = currentScroll.Current <= 32;

                if (shouldShow != hintVisible)
                {
                    hintVisible = shouldShow;
                    detailsHint.FadeTo(shouldShow ? 1 : 0, 200);
                }
            }
        }

        /// <summary>
        /// back en el modo stable directo: si la vista esta scrolleada en los extras, primero
        /// vuelve arriba (como el Esc de stable cierra el dialog de online ranking); devuelve
        /// false cuando ya esta arriba, y ahi el screen sale de una.
        /// </summary>
        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key == osuTK.Input.Key.Space || e.Key == osuTK.Input.Key.Enter || e.Key == osuTK.Input.Key.KeypadEnter)
            {
                if (currentRankingPanel?.IsAnimating == true)
                {
                    currentRankingPanel.FinishAnimation();
                    return true;
                }
            }

            return base.OnKeyDown(e);
        }

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

            aspectRatio.BindValueChanged(_ =>
            {
                if (builtScore == null)
                    return;

                var score = builtScore;
                builtScore = null;
                rebuildPanel(score);
            });

            // look de stable: sin el back button rosa de lazer, y la barra stock de abajo ni
            // reserva altura de layout (su fila de grid es auto-size) ni dibuja. va ACA y no en
            // load(): tocar el estado del footer global desde el loader thread async es una
            // mutacion ilegal (InvalidThreadForMutationException). LoadComplete corre siempre en
            // el update thread.
            screen.SetFooterBackButtonVisible(false);
            stockBottomPanel.Height = 0;

            if (directModeActive)
            {
                // post-gameplay: el panel legacy ya esta visible desde el primer frame (lo dejo
                // armado load()). aca solo sincronizamos el estado del screen (detach del panel
                // stock, dim del fondo) para que SelectedScore y compania queden como si se
                // hubiera abierto la vista de detalles — todo tapado por el panel legacy.
                // en este modo NUNCA volvemos al chrome stock: el bind ignora el hide, y el
                // screen (LegacyStableDirectMode) hace que back salga directo y Select no togglee.
                statisticsPanel.State.BindValueChanged(v =>
                {
                    if (v.NewValue == Visibility.Visible)
                        show();
                });

                screen.LegacyStableDirectMode = true;

                // el sync va con una demora corta: el detach del score panel stock asume que el
                // panel ya se asento (el delay original de 500ms protegia ese orden, no era solo
                // estetico). como todo pasa tapado por el panel legacy, la demora no se ve.
                Scheduler.AddDelayed(() =>
                {
                    if (statisticsPanel.State.Value != Visibility.Visible && (screen.SelectedScore.Value ?? screen.Score) != null)
                        statisticsPanel.ToggleVisibility();
                }, 250);

                // stable toca el aplauso al entrar al ranking. el flair stock (accuracy circle,
                // que era quien lo disparaba al final de su animacion) se suprime en este modo,
                // asi que lo tocamos nosotros. Schedule: PlayApplause exige IsCurrentScreen.
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

            // cambio de score (flechas izq/der) con la vista de detalles abierta.
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
            // esconder el contenido stock (statistics/panels) entero asi el panel legacy queda
            // solo sobre el fondo del beatmap. la toolbar tambien se va, como stable.
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

            // Stable lays out against the viewport, never a width inferred from skin textures.
            // A user-selected skin ratio constrains that viewport explicitly.
            float? designWidth = aspectRatio.Value switch
            {
                LegacySongSelectAspectRatio.Widescreen => LegacyRankingLayout.HEIGHT * 16f / 9,
                LegacySongSelectAspectRatio.WidescreenTall => LegacyRankingLayout.HEIGHT * 16f / 10,
                _ => null,
            };
            panelSpace.TargetDrawSize = new Vector2(designWidth ?? LegacyRankingLayout.MIN_WIDTH, LegacyRankingLayout.HEIGHT);

            // primero el skin actual, con el classic bundleado de fallback (para el score font y
            // las texturas ranking-* que el skin no traiga). la vista scrollea como la de stable:
            // el ranking panel es la primera "pagina", scrollear para abajo revela la seccion de
            // extras (pp, heatmap de accuracy, tags del mapa). el header (barra negra + titulo +
            // ranking-title) queda FIJO por encima del scroll, como los ViewOffsetImmune de stable.
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
                            // stable draws the RankingDialog under the results page (pScrollableArea.Draw: its children, then the
                            // content), so a skin's ranking-panel reaching down the page lies over the dialog. the dialog sits below the
                            // first page, kept there in Update.
                            Child = new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Children = new Drawable[]
                                {
                                    // la pagina de abajo estilo stable (el RankingDialog con las
                                    // bandas LOCAL / ONLINE / BEATMAP); vacia para scores ajenos.
                                    currentOnlineRanking = new LegacyOnlineRanking(score) { Y = 768 },
                                    panelPage = new Container
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 768,
                                        Child = createDesignArea(designWidth, currentRankingPanel = new LegacyRankingPanel(score, animate)),
                                    },
                                },
                            },
                        },
                        new HeaderStrip(score, designWidth, currentScroll),
                        // Ranking.InitializeSpecifics: a skinnable 200 x 30 BlueViolet pButton at (W / 2 - 100, H - 26), its text 14 * 30 / 18
                        // units tall.
                        detailsHint = new LegacyDialogButton(@"▼ Online Ranking ▼", Color4.BlueViolet, () => currentScroll?.ScrollToEnd())
                        {
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.TopCentre,
                            Y = -26 * 1.6f,
                            ButtonSize = new Vector2(200, 30) * 1.6f,
                            LabelSize = 14 * 30 / 18f * 1.6f,
                        },
                    },
                },
            };

            hasBelowFold = score.UserID == apiProvider.LocalUser.Value?.OnlineID;
            detailsHint.Alpha = hasBelowFold ? 1 : 0;
            hintVisible = hasBelowFold;
        }

        /// <summary>
        /// The area a skin's art was drawn for: its design width, centred, or the whole width when it fits any.
        /// </summary>
        private static Container createDesignArea(float? designWidth, Drawable content) => new Container
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            RelativeSizeAxes = designWidth == null ? Axes.Both : Axes.Y,
            Width = designWidth ?? 1,
            Child = content,
        };

        /// <summary>
        /// mientras esta visible, se traga el input de puntero asi los clicks no llegan al
        /// contenido stock (escondido) de abajo. los botones flotantes y el back button son
        /// hermanos puestos encima, asi que siguen andando.
        /// </summary>
        private partial class BlockingLayer : Container
        {
            public Action? ClickAction { get; set; }

            public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
                => Alpha > 0.5f && base.ReceivePositionalInputAt(screenSpacePos);

            protected override bool OnClick(ClickEvent e)
            {
                ClickAction?.Invoke();
                return true;
            }
            protected override bool OnHover(HoverEvent e) => true;
            protected override bool OnMouseDown(MouseDownEvent e) => true;
            protected override bool OnScroll(ScrollEvent e) => true;
        }

        /// <summary>
        /// el header fijo de stable (ViewOffsetImmune): barra negra de 60 unidades al 80% con
        /// el titulo del mapa / creador / jugador, y el ranking-title colgando arriba a la
        /// derecha. no scrollea con el panel.
        /// </summary>
        private partial class HeaderStrip : CompositeDrawable
        {
            public const float HEIGHT = 60 * 1.6f;

            private readonly ScoreInfo score;
            private readonly float? designWidth;
            private readonly OsuScrollContainer scroll;
            private Sprite title = null!;
            private bool newLayout;

            [Resolved]
            private SkinManager skins { get; set; } = null!;

            public HeaderStrip(ScoreInfo score, float? designWidth, OsuScrollContainer scroll)
            {
                this.score = score;
                this.designWidth = designWidth;
                this.scroll = scroll;
            }

            protected override void Update()
            {
                base.Update();
                // Ranking.Initialize: only the new layout's title is ViewOffsetImmune.
                title.Y = newLayout ? 0 : -(float)scroll.Current;
            }

            [BackgroundDependencyLoader]
            private void load(ISkinSource skin)
            {
                RelativeSizeAxes = Axes.X;
                Height = HEIGHT;
                newLayout = (skin.GetConfig<SkinConfiguration.LegacySetting, decimal>(SkinConfiguration.LegacySetting.Version)?.Value ?? SkinConfiguration.LATEST_VERSION) > 1;

                InternalChildren = new Drawable[]
                {
                    // the bar spans the window; the text and title sit in the skin's design area.
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black,
                        Alpha = 0.8f,
                    },
                    createDesignArea(designWidth, new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Position = new Vector2(0, 0),
                                Font = LegacyFonts.Get(22 * 1.6f),
                                Text = $"{score.BeatmapInfo?.Metadata.Artist} - {score.BeatmapInfo?.Metadata.Title} [{score.BeatmapInfo?.DifficultyName}]",
                            },
                            new OsuSpriteText
                            {
                                Position = new Vector2(1.6f, 20 * 1.6f),
                                Font = LegacyFonts.Get(16 * 1.6f),
                                Text = $"Beatmap by {score.BeatmapInfo?.Metadata.Author.Username}",
                            },
                            new OsuSpriteText
                            {
                                Position = new Vector2(1.6f, 34 * 1.6f),
                                Font = LegacyFonts.Get(16 * 1.6f),
                                Text = $"Played by {score.User.Username} on {score.Date.ToLocalTime():g}.",
                            },
                            title = new Sprite
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                X = -20 * 1.6f,
                                Texture = skin.GetStableTexture(@"ranking-title") ?? skins.DefaultClassicSkin.GetStableTexture(@"ranking-title"),
                            },
                        },
                    }),
                };
            }
        }
    }
}
