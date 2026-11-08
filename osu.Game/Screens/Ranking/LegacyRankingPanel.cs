// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.
// el panel de ranking (results) de osu!stable, layouteado desde el Ranking.cs de stable
// (LoadScore / InitializeRankingPanel / LoadRanking / LoadMods / InitializeSpecifics) con
// las coords del espacio 480 de stable x1.6 al espacio legacy de 768 de alto (ver LegacyRankingLayout). incluye la
// animacion de entrada de stable (elementos staggereados 300ms, grade 2x->1x + ghost
// aditivo, white flash), los rayos rotativos de fondo, el graph de la play adentro del
// marco ranking-graph, y los botones retry / watch replay pegados al borde derecho.

using System;
using System.Collections.Generic;
using System.Text;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Graphics.Containers;
using osu.Game.Input.Bindings;
using osu.Game.Online;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking
{
    public partial class LegacyRankingPanel : CompositeDrawable
    {
        private readonly ScoreInfo score;
        private readonly bool animate;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private Player? player { get; set; }

        [Resolved]
        private ResultsScreen? resultsScreen { get; set; }

        // stable's entrance (RankingElement): each image fades in and scales over 300ms, its text slides in 200ms later.
        private readonly List<EntranceElement> entranceElements = new List<EntranceElement>();
        private Sprite? grade;
        private Sprite? gradeGhost;
        private readonly List<Sprite> modSprites = new List<Sprite>();
        private Box? whiteFlash;
        private double gradeTime;
        private LegacySpriteText? scoreText;
        private string totalScoreString = string.Empty;
        private double scoreRevealStartTime;
        private bool animationFinished;
        private double animationEndTime;

        public bool IsAnimating => animate && !animationFinished;

        /// <param name="score">el score a mostrar.</param>
        /// <param name="animate">si corre la animacion de entrada de stable (solo el primer
        /// build post-gameplay; los rebuilds por cambio de score muestran todo al instante).</param>
        public LegacyRankingPanel(ScoreInfo score, bool animate = false)
        {
            this.score = score;
            this.animate = animate;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            RelativeSizeAxes = Axes.Both;

            // stable's TextureManager.LoadFirst: the plain name, then the first animation frame.
            Texture? tex(string name) => skin.GetStableTexture(name)
                                         ?? skin.GetStableTexture($"{name}-0")
                                         ?? skins.DefaultClassicSkin.GetStableTexture(name)
                                         ?? skins.DefaultClassicSkin.GetStableTexture($"{name}-0");

            // SkinManager.UseNewLayout: skins of version 1 keep stable's original layout.
            bool newLayout = (skin.GetConfig<SkinConfiguration.LegacySetting, decimal>(SkinConfiguration.LegacySetting.Version)?.Value ?? SkinConfiguration.LATEST_VERSION) > 1;

            // stable draws by depth; these layers follow its depths from back to front.
            var back = new List<Drawable>(); // ranking-panel (0.2)
            var texts = new List<Drawable>(); // ranking-graph, the HP graph, score and counts (0.8)
            var labels = new List<Drawable>(); // ranking-maxcombo and ranking-accuracy (0.81)
            var images = new List<Drawable>(); // hit counts and the grade (0.9)
            var overlays = new List<Drawable>(); // catch overlays and the grade's ghost (0.91)
            var front = new List<Drawable>(); // mods (0.92), buttons (0.94) and ranking-perfect (0.99)

            // rayos rotativos de fondo (stable: ranking-background-overlay, aditivo, girando
            // una vuelta cada 20s, centrado cerca del grade). detras de todo.
            var raysTex = tex(@"ranking-background-overlay");

            if (raysTex != null)
            {
                var rays = new Sprite
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.Centre,
                    Position = new Vector2(-180, newLayout ? 200 : 170) * 1.6f,
                    Texture = raysTex,
                    Blending = BlendingParameters.Additive,
                };
                back.Add(rays);
                rays.OnLoadComplete += d => d.Spin(20000, RotationDirection.Clockwise);
            }

            // InitializeRankingPanel.
            back.Add(new Sprite
            {
                Position = new Vector2(0, newLayout ? 64 : 46) * 1.6f,
                Texture = tex(@"ranking-panel"),
            });

            var graphPosition = new Vector2(160, newLayout ? 380 : 360);

            texts.Add(new Sprite
            {
                Position = graphPosition * 1.6f,
                Texture = tex(@"ranking-graph"),
            });

            // stable draws score.HpGraph in a 186 x 86 region inside the frame. lazer no persiste la vida, asi que la simulamos
            // desde los hit events: sube con los aciertos y se desploma con los miss — la misma forma que el graph clasico.
            if (score.HitEvents.Count > 0)
            {
                texts.Add(new LegacyHealthGraph(score, animate)
                {
                    Position = (graphPosition + new Vector2(newLayout ? 5 : 10)) * 1.6f,
                });
            }

            // LoadScore: the total score rolls its digits from the start, centred at (220, 94).
            totalScoreString = score.TotalScore <= 9999999 ? $"{score.TotalScore:0000000}" : $"{score.TotalScore:00000000}";

            scoreText = new LegacySpriteText(LegacyFont.Score)
            {
                Origin = Anchor.Centre,
                Position = new Vector2(220, 94) * 1.6f,
                Scale = new Vector2(newLayout ? 1.3f : 1.05f),
                Text = animate ? randomDigits(0) : totalScoreString,
                FixedWidth = true,
            };
            texts.Add(scoreText);

            // AddRankingSprites: elements start 300ms apart, 300ms after the screen (startTime) and 300ms after that (their own start).
            double t = 600;
            float textYOffset = newLayout ? -16 : -25;

            foreach (var row in hitRows())
            {
                var image = new Sprite
                {
                    Origin = Anchor.Centre,
                    Position = row.Position * 1.6f,
                    Texture = tex(row.Image),
                    Rotation = MathHelper.RadiansToDegrees(row.Rotation),
                    Colour = row.Colour ?? Color4.White,
                };

                LegacySpriteText? text = null;

                if (row.Text != null)
                {
                    text = new LegacySpriteText(LegacyFont.Score)
                    {
                        Position = new Vector2(row.Position.X + 40, row.Position.Y + textYOffset) * 1.6f,
                        Scale = new Vector2(1.12f),
                        Text = row.Text,
                    };
                    texts.Add(text);
                }

                (row.IsOverlay ? overlays : images).Add(image);
                addEntrance(image, row.StartScale, row.EndScale, text, t);

                if (!row.IsOverlay)
                    t += 300;
            }

            float labelY = 320 - (newLayout ? 20 : 8);

            t = addLabel(@"ranking-maxcombo", new Vector2(5, labelY), $"{score.MaxCombo}x", new Vector2(15, 330), t);
            t = addLabel(@"ranking-accuracy", new Vector2(182, labelY), $"{score.Accuracy * 100:0.00}%", new Vector2(194, 330), t);

            double addLabel(string name, Vector2 position, string value, Vector2 valuePosition, double time)
            {
                var image = new Sprite
                {
                    Position = position * 1.6f,
                    Texture = tex(name),
                };
                var text = new LegacySpriteText(LegacyFont.Score)
                {
                    Position = valuePosition * 1.6f,
                    Scale = new Vector2(1.12f),
                    Text = value,
                };

                labels.Add(image);
                texts.Add(text);
                addEntrance(image, 1, 1, text, time);
                return time + 300;
            }

            if (isPerfect)
            {
                // starts 300ms after its slot, as the grade does.
                var perfect = new Sprite
                {
                    Origin = Anchor.Centre,
                    Position = new Vector2(newLayout ? 260 : 200, 430) * 1.6f,
                    Texture = tex(@"ranking-perfect"),
                };
                front.Add(perfect);
                addEntrance(perfect, 1.1f, 1, null, t + 300);
                t += 300;
            }

            // LoadRanking: the grade scales 2 -> 1 and fades in over 1000ms once every element has started; a B or better also
            // gets an additive ghost that grows and fades out once it lands.
            gradeTime = t;

            var gradeTex = tex($@"ranking-{score.Rank}") ?? tex(@"ranking-D");
            var gradePosition = new Vector2(-120, newLayout ? 200 : 170) * 1.6f;

            grade = new Sprite
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Position = gradePosition,
                Texture = gradeTex,
                Alpha = animate ? 0 : 1,
                Scale = animate ? new Vector2(2) : Vector2.One,
            };
            images.Add(grade);

            if (animate && score.Rank >= ScoreRank.B)
            {
                gradeGhost = new Sprite
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.Centre,
                    Position = gradePosition,
                    Texture = gradeTex,
                    Blending = BlendingParameters.Additive,
                    Alpha = 0,
                };
                overlays.Add(gradeGhost);
            }

            // LoadMods: field TopRight, y = 260, the first 40 units from the right edge and each next one 20 further left, drawn
            // over the previous ones.
            int modIndex = 0;

            foreach (var mod in score.Mods)
            {
                string name = mod.GetType().Name;
                int idx = name.IndexOf(@"Mod", StringComparison.Ordinal);
                if (idx < 0)
                    continue;

                var modTex = tex($@"selection-mod-{mapModTextureName(name[(idx + 3)..].ToLowerInvariant())}");
                if (modTex == null)
                    continue;

                var modSprite = new Sprite
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.Centre,
                    Position = new Vector2(-(40 + modIndex * 20) * 1.6f, 260 * 1.6f),
                    Texture = modTex,
                    Alpha = animate ? 0 : 1,
                    Scale = animate ? new Vector2(2) : Vector2.One,
                };
                front.Add(modSprite);
                modSprites.Add(modSprite);
                modIndex++;
            }

            // InitializeSpecifics: retry and replay on the right edge from y = 360, 60 units apart, at 70% until hovered. skins of
            // version 1 use their ranking-retry and ranking-replay first.
            float buttonY = 360;
            bool showRetry = player != null && resultsScreen?.AllowRetry == true;

            if (showRetry)
            {
                front.Add(new LegacyPanelButton((newLayout ? null : tex(@"ranking-retry")) ?? tex(@"pause-retry"), () => player?.Restart())
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.CentreRight,
                    Y = buttonY * 1.6f,
                });
                buttonY += 60;
            }

            if (resultsScreen?.AllowWatchingReplay != false)
            {
                front.Add(new LegacyWatchReplayButton(score, (newLayout ? null : tex(@"ranking-replay")) ?? tex(@"pause-replay"))
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.CentreRight,
                    Y = buttonY * 1.6f,
                });
            }

            // white flash de stable: pantallazo aditivo que acompaña la llegada del grade. it covers the whole window, even when the
            // panel is narrower than it.
            if (animate)
            {
                whiteFlash = new Box
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    RelativeSizeAxes = Axes.Y,
                    Width = 10000,
                    Colour = Color4.White,
                    Blending = BlendingParameters.Additive,
                    Alpha = 0,
                };
                front.Add(whiteFlash);
            }

            var children = new List<Drawable>();
            children.AddRange(back);
            children.AddRange(texts);
            children.AddRange(labels);
            children.AddRange(images);
            children.AddRange(overlays);
            children.AddRange(front);
            InternalChildren = children.ToArray();
        }

        private void addEntrance(Drawable image, float startScale, float endScale, Drawable? text, double time)
        {
            image.Alpha = animate ? 0 : 1;
            image.Scale = new Vector2(animate ? startScale : endScale);

            if (text != null)
                text.Alpha = animate ? 0 : 1;

            entranceElements.Add(new EntranceElement(image, endScale, text, time));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (!animate)
                return;

            scoreRevealStartTime = Time.Current + 300;
            animationEndTime = Math.Max(scoreRevealStartTime + totalScoreString.Length * 500,
                Time.Current + Math.Max(gradeTime + 3400, Math.Max(0, modSprites.Count - 1) * 500 + 400));
            foreach (var element in entranceElements)
                animationEndTime = Math.Max(animationEndTime, Time.Current + element.Time + 500);

            // RankingElement: the image fades in and scales over 300ms (In); its text slides 40 units in from the left and fades
            // in 200ms later, over 300ms (Out).
            foreach (var element in entranceElements)
            {
                element.Image.Delay(element.Time).FadeIn(300, Easing.In);
                element.Image.Delay(element.Time).ScaleTo(element.EndScale, 300, Easing.In);

                if (element.Text != null)
                {
                    float finalX = element.Text.X;
                    element.Text.X = finalX - 40 * 1.6f;
                    element.Text.Delay(element.Time + 200).MoveToX(finalX, 300, Easing.Out);
                    element.Text.Delay(element.Time + 200).FadeIn(300, Easing.Out);
                }
            }

            grade?.Delay(gradeTime).FadeIn(1000, Easing.In);
            grade?.Delay(gradeTime).ScaleTo(1, 1000, Easing.In);

            if (gradeGhost != null)
            {
                gradeGhost.Delay(gradeTime + 1000).FadeTo(1).Then().FadeOut(2400, Easing.Out);
                gradeGhost.Delay(gradeTime + 1000).ScaleTo(1.05f, 2400, Easing.Out);
            }

            // s_white: 0.5 -> 0 over 1400ms once the grade lands.
            whiteFlash?.Delay(gradeTime + 1000).FadeTo(0.5f).Then().FadeOut(1400);

            // LoadMods: from the screen's start, 500ms apart, 2 -> 1 and fading in over 400ms (Out).
            for (int i = 0; i < modSprites.Count; i++)
            {
                modSprites[i].Delay(i * 500).FadeIn(400, Easing.Out);
                modSprites[i].Delay(i * 500).ScaleTo(1, 400, Easing.Out);
            }
        }

        public void FinishAnimation()
        {
            if (!animate || animationFinished)
                return;

            animationFinished = true;

            foreach (var element in entranceElements)
            {
                element.Image.FinishTransforms(true);
                element.Image.Alpha = 1;
                element.Image.Scale = new Vector2(element.EndScale);

                if (element.Text != null)
                {
                    element.Text.FinishTransforms(true);
                    element.Text.Alpha = 1;
                }
            }

            if (grade != null)
            {
                grade.FinishTransforms(true);
                grade.Alpha = 1;
                grade.Scale = Vector2.One;
            }

            if (gradeGhost != null)
            {
                gradeGhost.FinishTransforms(true);
                gradeGhost.Alpha = 0;
            }

            if (whiteFlash != null)
            {
                whiteFlash.FinishTransforms(true);
                whiteFlash.Alpha = 0;
            }

            foreach (var mod in modSprites)
            {
                mod.FinishTransforms(true);
                mod.Alpha = 1;
                mod.Scale = Vector2.One;
            }

            if (scoreText != null)
                scoreText.Text = totalScoreString;
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (IsAnimating)
            {
                FinishAnimation();
                return true;
            }

            return base.OnClick(e);
        }

        protected override void Update()
        {
            base.Update();

            if (IsAnimating && Time.Current >= animationEndTime)
                FinishAnimation();

            // Ranking.Update: one more digit settles every 500ms from the screen's start; the rest roll.
            if (IsAnimating && scoreText != null)
            {
                int settled = (int)((Time.Current - scoreRevealStartTime) / 500);
                scoreText.Text = settled >= totalScoreString.Length ? totalScoreString : randomDigits(settled);
            }
        }

        private string randomDigits(int settled)
        {
            var sb = new StringBuilder(totalScoreString.Length);

            for (int i = 0; i < totalScoreString.Length; ++i)
                sb.Append(i < settled ? totalScoreString[i] : (char)('0' + RNG.Next(0, 9)));

            return sb.ToString();
        }

        /// <summary>
        /// los nombres de mod de lazer que no coinciden con la textura de stable.
        /// </summary>
        private static string mapModTextureName(string name) => name switch
        {
            @"autopilot" => @"relax2",
            @"targetpractice" => @"target",
            @"daycore" => @"halftime",
            _ => name,
        };

        private bool isPerfect => stat(HitResult.Miss) == 0 && stat(HitResult.LargeTickMiss) == 0 && stat(HitResult.ComboBreak) == 0;

        private int stat(HitResult result) => score.Statistics.GetValueOrDefault(result);

        private int maxStat(HitResult result) => score.MaximumStatistics.GetValueOrDefault(result);

        private readonly record struct EntranceElement(Drawable Image, float EndScale, Drawable? Text, double Time);

        /// <param name="Image">the texture, or its first animation frame.</param>
        /// <param name="Position">its centre, in stable's 640 x 480 units.</param>
        /// <param name="Text">the count beside it, or <c>null</c> for none.</param>
        /// <param name="StartScale">the scale it enters at.</param>
        /// <param name="EndScale">the scale it settles at.</param>
        /// <param name="Rotation">its rotation, in radians.</param>
        /// <param name="Colour">its tint.</param>
        /// <param name="IsOverlay">drawn over the other images, starting with the next one instead of taking a slot of its own.</param>
        private readonly record struct HitRow(string Image, Vector2 Position, string? Text, float StartScale = 1, float EndScale = 0.5f, float Rotation = 0, Color4? Colour = null, bool IsOverlay = false);

        /// <summary>
        /// los seis slots de hit-counts, mapeados por ruleset como el switch de LoadScore de stable.
        /// para osu!, los slots de geki/katu muestran los slider tick hits / slider tail misses de
        /// lazer (stable no tiene equivalente para scores de lazer), reusando las texturas geki/katu.
        /// </summary>
        private IEnumerable<HitRow> hitRows()
        {
            var left1 = new Vector2(40, 160);
            var left2 = new Vector2(40, 220);
            var left3 = new Vector2(40, 280);
            var right1 = new Vector2(240, 160);
            var right2 = new Vector2(240, 220);
            var right3 = new Vector2(240, 280);

            switch (score.Ruleset.ShortName)
            {
                default:
                    yield return new HitRow(@"hit300", left1, $"{stat(HitResult.Great)}x");
                    yield return new HitRow(@"hit100", left2, $"{stat(HitResult.Ok)}x");
                    yield return new HitRow(@"hit50", left3, $"{stat(HitResult.Meh)}x");
                    yield return new HitRow(@"hit300g", right1, $"{stat(HitResult.LargeTickHit)}x");
                    yield return new HitRow(@"hit100k", right2, $"{Math.Max(0, maxStat(HitResult.SliderTailHit) - stat(HitResult.SliderTailHit))}x");
                    yield return new HitRow(@"hit0", right3, $"{stat(HitResult.Miss)}x");
                    break;

                case @"taiko":
                    yield return new HitRow(@"taiko-hit300", left1, $"{stat(HitResult.Great)}x");
                    yield return new HitRow(@"taiko-hit100", left2, $"{stat(HitResult.Ok)}x");
                    yield return new HitRow(@"taiko-hit0", left3, $"{stat(HitResult.Miss)}x");
                    yield return new HitRow(@"taiko-hit300g", right1, $"{stat(HitResult.LargeBonus)}x");
                    yield return new HitRow(@"taiko-hit100k", right2, $"{stat(HitResult.SmallBonus)}x");
                    break;

                case @"fruits":
                    // fruits tinted as stable tints them, the misses as a grey fruit under a small miss mark.
                    yield return new HitRow(@"fruit-orange-overlay", left1, null, IsOverlay: true);
                    yield return new HitRow(@"fruit-orange", left1, $"{stat(HitResult.Great)}x", Colour: Color4.Orange);
                    yield return new HitRow(@"fruit-drop", left2, $"{stat(HitResult.LargeTickHit)}x", EndScale: 0.6f, Colour: Color4.YellowGreen);
                    yield return new HitRow(@"fruit-drop", left3, $"{stat(HitResult.SmallTickHit)}x", EndScale: 0.4f, Rotation: 4, Colour: Color4.LightBlue);
                    yield return new HitRow(@"fruit-orange-overlay", right1, null, EndScale: 0.4f, Rotation: 0.8f, IsOverlay: true);
                    yield return new HitRow(@"hit0", right1 - new Vector2(20), null, StartScale: 0.7f, EndScale: 0.2f, IsOverlay: true);
                    yield return new HitRow(@"fruit-orange", right1, $"{stat(HitResult.Miss)}x", EndScale: 0.4f, Rotation: 0.8f, Colour: Color4.LightGray);
                    break;

                case @"mania":
                    yield return new HitRow(@"mania-hit300", left1, $"{stat(HitResult.Great)}x");
                    yield return new HitRow(@"mania-hit200", left2, $"{stat(HitResult.Good)}x");
                    yield return new HitRow(@"mania-hit50", left3, $"{stat(HitResult.Meh)}x");
                    yield return new HitRow(@"mania-hit300g", right1, $"{stat(HitResult.Perfect)}x");
                    yield return new HitRow(@"mania-hit100", right2, $"{stat(HitResult.Ok)}x");
                    yield return new HitRow(@"mania-hit0", right3, $"{stat(HitResult.Miss)}x");
                    break;
            }
        }

        /// <summary>
        /// boton de sprite estilo stable: idle al 70% de alpha, hover al 100% en 200ms.
        /// </summary>
        private partial class LegacyPanelButton : OsuClickableContainer
        {
            public LegacyPanelButton(Texture? texture, Action? action)
            {
                AutoSizeAxes = Axes.Both;
                Alpha = 0.7f;
                Action = action;

                Child = new Sprite { Texture = texture };
            }

            protected override bool OnHover(HoverEvent e)
            {
                this.FadeTo(1, 200);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                this.FadeTo(0.7f, 200);
                base.OnHoverLost(e);
            }
        }

        /// <summary>
        /// el "watch replay" de stable, con la misma maquina de estados que el
        /// <see cref="ReplayDownloadButton"/> stock: si el replay esta local lo reproduce,
        /// si esta online lo descarga primero. tambien conserva los hotkeys de guardar /
        /// exportar replay (la barra stock que los manejaba queda escondida en modo legacy).
        /// se esconde solo si el score no tiene replay por ningun lado, como stable.
        /// </summary>
        private partial class LegacyWatchReplayButton : LegacyPanelButton, IKeyBindingHandler<GlobalAction>
        {
            private readonly ScoreInfo score;
            private readonly Bindable<DownloadState> state = new Bindable<DownloadState>();

            [Resolved]
            private OsuGame? game { get; set; }

            [Resolved]
            private ScoreManager scoreManager { get; set; } = null!;

            [Resolved]
            private ScoreModelDownloader scoreDownloader { get; set; } = null!;

            public LegacyWatchReplayButton(ScoreInfo score, Texture? texture)
                : base(texture, null)
            {
                this.score = score;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                AddInternal(new ScoreDownloadTracker(score)
                {
                    State = { BindTarget = state },
                });

                Action = onClick;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                state.BindValueChanged(_ => updateAvailability(), true);
            }

            private bool replayAvailable => state.Value == DownloadState.LocallyAvailable || score.HasOnlineReplay;

            private void updateAvailability()
            {
                Enabled.Value = replayAvailable;
                this.FadeTo(replayAvailable ? (IsHovered ? 1 : 0.7f) : 0, 120);
            }

            private void onClick()
            {
                switch (state.Value)
                {
                    case DownloadState.LocallyAvailable:
                        game?.PresentScore(score, ScorePresentType.Gameplay);
                        break;

                    case DownloadState.NotDownloaded:
                        if (score.HasOnlineReplay)
                            scoreDownloader.Download(score);
                        break;
                }
            }

            public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
            {
                if (e.Repeat)
                    return false;

                switch (e.Action)
                {
                    case GlobalAction.SaveReplay:
                        TriggerClick();
                        return true;

                    case GlobalAction.ExportReplay:
                        if (state.Value == DownloadState.LocallyAvailable)
                            scoreManager.Export(score);
                        else if (score.HasOnlineReplay)
                        {
                            scoreDownloader.Download(score);
                            state.BindValueChanged(exportWhenReady);
                        }

                        return true;
                }

                return false;
            }

            public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
            {
            }

            private void exportWhenReady(ValueChangedEvent<DownloadState> s)
            {
                if (s.NewValue != DownloadState.LocallyAvailable)
                    return;

                scoreManager.Export(score);
                state.ValueChanged -= exportWhenReady;
            }
        }
    }
}
