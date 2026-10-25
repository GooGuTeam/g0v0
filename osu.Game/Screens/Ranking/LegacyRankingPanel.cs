// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.
// osu!stable's (results) ranking panel, laid out from stable's Ranking.cs (LoadScore /
// InitializeRankingPanel / LoadRanking / LoadMods / InitializeSpecifics) using stable's 480-space
// coordinates x1.6 into the 1366x768 legacy space. Includes stable's entrance animation (elements
// staggered 300ms, grade 2x->1x + additive ghost, white flash), the rotating background rays, the
// play's graph inside the ranking-graph frame, and the retry / watch replay buttons pinned to the
// right edge.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
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

        // stable's layout constants (480 space, skin v2+ "new layout").
        private const int textx1 = 80;
        private const int imgx1 = 40;
        private const int textx2 = 280;
        private const int imgx2 = 240;
        private const int row1 = 160;
        private const int row2 = 220;
        private const int row3 = 280;
        private const int row4 = 320;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private Player? player { get; set; }

        [Resolved]
        private ResultsScreen? resultsScreen { get; set; }

        // stable's entrance chain: every element (image + its text) enters staggered.
        private readonly List<(Drawable image, bool shrink, Drawable? text, double time, float targetScale)> entranceElements = new List<(Drawable, bool, Drawable?, double, float)>();
        private Sprite? grade;
        private Sprite? gradeGhost;
        private readonly List<Sprite> modSprites = new List<Sprite>();
        private Box? whiteFlash;
        private double gradeTime;

        /// <param name="score">The score to show.</param>
        /// <param name="animate">Whether to run stable's entrance animation (only on the first
        /// post-gameplay build; rebuilds for a score change show everything instantly).</param>
        public LegacyRankingPanel(ScoreInfo score, bool animate = false)
        {
            this.score = score;
            this.animate = animate;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            RelativeSizeAxes = Axes.Both;

            Texture? tex(string name) => skin.GetTexture(name)
                                         ?? skin.GetTexture($"{name}-0")
                                         ?? skins.DefaultClassicSkin.GetTexture(name)
                                         ?? skins.DefaultClassicSkin.GetTexture($"{name}-0");

            var children = new List<Drawable>();

            // Rotating background rays (stable: ranking-background-overlay, additive, one turn every
            // 20s, centred near the grade). Behind everything.
            var raysTex = tex(@"ranking-background-overlay");

            if (raysTex != null)
            {
                var rays = new Sprite
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.Centre,
                    Position = new Vector2(-180, 200) * 1.6f,
                    Texture = raysTex,
                    Blending = BlendingParameters.Additive,
                };
                children.Add(rays);
                rays.OnLoadComplete += d => d.Spin(20000, RotationDirection.Clockwise);
            }

            // The panel's large backing (stable: (0, 64) with the new layout).
            children.Add(new Sprite
            {
                Position = new Vector2(0, 64 * 1.6f),
                Texture = tex(@"ranking-panel"),
            });

            // The ranking-graph frame (stable: (160, 380)) + the HP graph inside it (stable draws
            // score.HpGraph at graphPos+(5,5), region 186x86; lazer doesn't persist health, so we
            // simulate it from the hit events: it rises with hits and drops on misses - the same shape as
            // the classic graph).
            var graphTex = tex(@"ranking-graph");

            children.Add(new Sprite
            {
                Position = new Vector2(160, 380) * 1.6f,
                Texture = graphTex,
            });

            if (score.HitEvents.Count > 0)
            {
                children.Add(new LegacyHealthGraph(score, animate)
                {
                    Position = new Vector2(165, 385) * 1.6f,
                });
            }

            // Total score in the skin's score font (stable: (220, 94), origin centre, 1.3x).
            var scoreText = new LegacySpriteText(LegacyFont.Score)
            {
                Origin = Anchor.Centre,
                Position = new Vector2(220, 94) * 1.6f,
                Scale = new Vector2(1.3f),
                Alpha = animate ? 0 : 1,
                Text = score.TotalScore <= 9999999 ? $"{score.TotalScore:0000000}" : $"{score.TotalScore:00000000}",
            };
            children.Add(scoreText);

            double t = 300;
            entranceElements.Add((scoreText, false, null, t, 1));

            // The six hit count slots, staggered like stable's AddRankingSprites.
            foreach (var (image, position, count) in hitRows())
            {
                t += 300;

                var img = new Sprite
                {
                    Origin = Anchor.Centre,
                    Position = position * 1.6f,
                    Texture = tex(image),
                    Alpha = animate ? 0 : 1,
                    Scale = animate ? new Vector2(2) : Vector2.One,
                };
                var txt = new LegacySpriteText(LegacyFont.Score)
                {
                    Position = new Vector2(position.X + 40, position.Y - 16) * 1.6f,
                    Scale = new Vector2(1.12f),
                    Alpha = animate ? 0 : 1,
                    Text = $"{count}x",
                };
                children.Add(img);
                children.Add(txt);
                entranceElements.Add((img, true, txt, t, 1));
            }

            // Labels + values for max combo / accuracy (stable's rows around row4; the images do NOT do
            // the 2x->1x shrink, they only fade - that's how stable does it).
            t += 300;

            var comboImg = new Sprite
            {
                Position = new Vector2(imgx1 - 35, row4 - 20) * 1.6f,
                Texture = tex(@"ranking-maxcombo"),
                Alpha = animate ? 0 : 1,
            };
            var comboTxt = new LegacySpriteText(LegacyFont.Score)
            {
                Position = new Vector2(textx1 - 65, row4 + 10) * 1.6f,
                Scale = new Vector2(1.12f),
                Alpha = animate ? 0 : 1,
                Text = $"{score.MaxCombo}x",
            };
            children.Add(comboImg);
            children.Add(comboTxt);
            entranceElements.Add((comboImg, false, comboTxt, t, 1));

            t += 300;

            var accImg = new Sprite
            {
                Position = new Vector2(imgx2 - 58, row4 - 20) * 1.6f,
                Texture = tex(@"ranking-accuracy"),
                Alpha = animate ? 0 : 1,
            };
            var accTxt = new LegacySpriteText(LegacyFont.Score)
            {
                Position = new Vector2(textx2 - 86, row4 + 10) * 1.6f,
                Scale = new Vector2(1.12f),
                Alpha = animate ? 0 : 1,
                Text = $"{score.Accuracy * 100:0.00}%",
            };
            children.Add(accImg);
            children.Add(accTxt);
            entranceElements.Add((accImg, false, accTxt, t, 1));

            // The grade (stable LoadRanking: field TopRight, origin centre, (120, 200); enters 2x->1x with
            // a fade AFTER all the elements, with an additive ghost that grows and fades for B+ ranks).
            gradeTime = t + 300;

            var gradeTex = tex($@"ranking-{score.Rank}") ?? tex(@"ranking-D");

            grade = new Sprite
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-120, 200) * 1.6f,
                Texture = gradeTex,
                Alpha = animate ? 0 : 1,
                Scale = animate ? new Vector2(2) : Vector2.One,
            };
            children.Add(grade);

            if (animate && score.Rank >= ScoreRank.B)
            {
                gradeGhost = new Sprite
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.Centre,
                    Position = new Vector2(-120, 200) * 1.6f,
                    Texture = gradeTex,
                    Blending = BlendingParameters.Additive,
                    Alpha = 0,
                };
                children.Add(gradeGhost);
            }

            // Mod icons (stable LoadMods: field TopRight origin centre, y=260, the first one 40 from the
            // right edge, +20 per icon towards the left - heavy overlap, so the first ends up furthest
            // right and the following ones draw ON TOP). They enter 2x->1x staggered 500ms.
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
                children.Add(modSprite);
                modSprites.Add(modSprite);
                modIndex++;
            }

            // Full combo banner (stable: for perfect plays, at (260, 430), in front). Scaled so it fits the
            // ranking-graph frame (the default texture is wider than the frame and came out gigantic,
            // covering everything).
            if (isPerfect)
            {
                gradeTime += 300;

                var perfectTex = tex(@"ranking-perfect");
                float perfectScale = 1;

                if (perfectTex != null && graphTex != null)
                    perfectScale = Math.Min(1, graphTex.DisplayWidth * 0.95f / perfectTex.DisplayWidth);

                var perfect = new Sprite
                {
                    Origin = Anchor.Centre,
                    Position = new Vector2(260, 430) * 1.6f,
                    Texture = perfectTex,
                    Alpha = animate ? 0 : 1,
                    Scale = new Vector2(animate ? perfectScale * 2 : perfectScale),
                };
                children.Add(perfect);
                entranceElements.Add((perfect, true, null, gradeTime, perfectScale));

                gradeTime += 300;
            }

            // stable's retry / watch replay buttons: pinned to the right edge (origin centre-right), retry
            // at y=360 and replay at y=420 (or 360 when there's no retry). 70% alpha idle, 100% on hover -
            // exactly like stable.
            bool showRetry = player != null && resultsScreen?.AllowRetry == true;

            if (showRetry)
            {
                children.Add(new LegacyPanelButton(tex(@"pause-retry"), () => player?.Restart())
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.CentreRight,
                    Y = 360 * 1.6f,
                });
            }

            if (resultsScreen?.AllowWatchingReplay != false)
            {
                children.Add(new LegacyWatchReplayButton(score, tex(@"pause-replay"))
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.CentreRight,
                    Y = (showRetry ? 420 : 360) * 1.6f,
                });
            }

            // stable's white flash: an additive screen flash that accompanies the grade landing.
            if (animate)
            {
                whiteFlash = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                    Blending = BlendingParameters.Additive,
                    Alpha = 0,
                };
                children.Add(whiteFlash);
            }

            InternalChildren = children.ToArray();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (!animate)
                return;

            // stable's entrance chain (RankingElement.cs): the image fades in + shrinks 2x->1x over 300ms
            // In; its text enters 200ms later sliding 40 units in from the left with a fade, 300ms Out.
            foreach (var (image, shrink, text, time, targetScale) in entranceElements)
            {
                image.Delay(time).FadeIn(300, Easing.In);

                if (shrink)
                    image.Delay(time).ScaleTo(targetScale, 300, Easing.In);

                if (text != null)
                {
                    float finalX = text.X;
                    text.X = finalX - 40 * 1.6f;
                    text.Delay(time + 200).MoveToX(finalX, 300, Easing.Out);
                    text.Delay(time + 200).FadeIn(300, Easing.Out);
                }
            }

            // Grade: 1000ms In after all the elements; the additive ghost 1s later.
            grade?.Delay(gradeTime).FadeIn(1000, Easing.In);
            grade?.Delay(gradeTime).ScaleTo(1, 1000, Easing.In);

            if (gradeGhost != null)
            {
                gradeGhost.Delay(gradeTime + 1000).FadeTo(1).Then().FadeOut(2400, Easing.Out);
                gradeGhost.Delay(gradeTime + 1000).ScaleTo(1.05f, 2400, Easing.Out);
            }

            // White flash: 0.5 -> 0 as the grade lands.
            if (whiteFlash != null)
                whiteFlash.Delay(gradeTime + 1000).FadeTo(0.5f).Then().FadeOut(1400);

            // Mods: staggered 500ms, 2x->1x shrink + fade over 400ms Out.
            for (int i = 0; i < modSprites.Count; i++)
            {
                modSprites[i].Delay(300 + i * 500).FadeIn(400, Easing.Out);
                modSprites[i].Delay(300 + i * 500).ScaleTo(1, 400, Easing.Out);
            }
        }

        /// <summary>
        /// lazer mod names that don't match stable's texture.
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

        /// <summary>
        /// The six hit count slots, mapped per ruleset like stable's LoadScore switch. For osu!, the
        /// geki/katu slots show lazer's slider tick hits / slider tail misses (stable has no equivalent
        /// for lazer scores), reusing the geki/katu textures.
        /// </summary>
        private IEnumerable<(string image, Vector2 position, int count)> hitRows()
        {
            switch (score.Ruleset.ShortName)
            {
                default:
                    yield return (@"hit300", new Vector2(imgx1, row1), stat(HitResult.Great));
                    yield return (@"hit100", new Vector2(imgx1, row2), stat(HitResult.Ok));
                    yield return (@"hit50", new Vector2(imgx1, row3), stat(HitResult.Meh));
                    yield return (@"hit300g", new Vector2(imgx2, row1), stat(HitResult.LargeTickHit));
                    yield return (@"hit100k", new Vector2(imgx2, row2), Math.Max(0, maxStat(HitResult.SliderTailHit) - stat(HitResult.SliderTailHit)));
                    yield return (@"hit0", new Vector2(imgx2, row3), stat(HitResult.Miss));
                    break;

                case @"taiko":
                    yield return (@"taiko-hit300", new Vector2(imgx1, row1), stat(HitResult.Great));
                    yield return (@"taiko-hit100", new Vector2(imgx1, row2), stat(HitResult.Ok));
                    yield return (@"taiko-hit0", new Vector2(imgx1, row3), stat(HitResult.Miss));
                    yield return (@"taiko-hit300g", new Vector2(imgx2, row1), stat(HitResult.LargeBonus));
                    yield return (@"taiko-hit100k", new Vector2(imgx2, row2), stat(HitResult.SmallBonus));
                    break;

                case @"fruits":
                    yield return (@"fruit-orange", new Vector2(imgx1, row1), stat(HitResult.Great));
                    yield return (@"fruit-drop", new Vector2(imgx1, row2), stat(HitResult.LargeTickHit));
                    yield return (@"fruit-drop", new Vector2(imgx1, row3), stat(HitResult.SmallTickHit));
                    yield return (@"hit0", new Vector2(imgx2, row1), stat(HitResult.Miss));
                    break;

                case @"mania":
                    yield return (@"mania-hit300", new Vector2(imgx1, row1), stat(HitResult.Great));
                    yield return (@"mania-hit200", new Vector2(imgx1, row2), stat(HitResult.Good));
                    yield return (@"mania-hit50", new Vector2(imgx1, row3), stat(HitResult.Meh));
                    yield return (@"mania-hit300g", new Vector2(imgx2, row1), stat(HitResult.Perfect));
                    yield return (@"mania-hit100", new Vector2(imgx2, row2), stat(HitResult.Ok));
                    yield return (@"mania-hit0", new Vector2(imgx2, row3), stat(HitResult.Miss));
                    break;
            }
        }


        /// <summary>
        /// stable-style sprite button: 70% alpha idle, 100% on hover over 200ms.
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
        /// stable's "watch replay", with the same state machine as the stock
        /// <see cref="ReplayDownloadButton"/>: plays the replay if it's local, downloads it first if it's
        /// online. Also keeps the save / export replay hotkeys working (the stock bar that handled them is
        /// hidden in legacy mode). Hides itself only when the score has no replay anywhere, as in stable.
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
