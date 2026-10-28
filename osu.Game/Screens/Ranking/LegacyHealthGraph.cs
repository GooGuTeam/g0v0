// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.
// The legacy results screen's HP graph: the play's REAL health across the map, computed by replaying the
// hit events against the ruleset's actual HealthProcessor (calibrated drain included) - headless, never
// added to the tree. stable-style presentation: green/red line with the split at half health, a 4s
// reveal, clustered miss/50/100 crosses ("Nx" in a pill) pinned on the line, and a multi-line tooltip
// with hit error + unstable rate like stable's hint. If the beatmap can't be converted it falls back to
// a cheap simulation so the graph never disappears.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking
{
    public partial class LegacyHealthGraph : CompositeDrawable, IHasCustomTooltip<LegacyHealthGraph.TooltipData?>
    {
        private const int samples_count = 90;
        private const int marker_columns = 24;

        // Inner margins: at hp=1.0 the line floats a few px inside the frame instead of sticking to the
        // edge (the "top glue" that looked broken), and at hp=0 it doesn't touch the floor.
        private const float top_inset = 8;
        private const float bottom_inset = 6;

        // Stroke half-widths. The per-segment boxes the line used to be built from were 3.2 and 7 px tall.
        private const float line_radius = 1.6f;
        private const float shadow_radius = 3.5f;

        private static readonly Vector2 region = new Vector2(186, 86) * 1.6f;

        private static readonly Color4 marker_miss = new Color4(231, 68, 58, 255);
        private static readonly Color4 marker_meh = new Color4(255, 163, 66, 255);
        private static readonly Color4 marker_ok = new Color4(109, 222, 73, 255);

        private readonly ScoreInfo score;
        private readonly bool animate;

        [Resolved]
        private Player? player { get; set; }

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        private Container? revealMask;
        private CancellationTokenSource? cancellation;

        public TooltipData? TooltipContent { get; private set; }

        public ITooltip<TooltipData?> GetCustomTooltip() => new LegacyHintTooltip();

        public LegacyHealthGraph(ScoreInfo score, bool animate)
        {
            this.score = score;
            this.animate = animate;
            Size = region;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // The tooltip content is cheap to build, so it is built synchronously here.
            double? unstableRate = score.HitEvents.CalculateUnstableRate()?.Result;
            var offsets = score.HitEvents.Where(e => e.Result.IsBasic() && e.Result.IsHit()).Select(e => e.TimeOffset).ToList();

            if (unstableRate != null && offsets.Count > 0)
            {
                TooltipContent = new TooltipData(
                    offsets.Where(o => o < 0).DefaultIfEmpty(0).Average(),
                    offsets.Where(o => o > 0).DefaultIfEmpty(0).Average(),
                    unstableRate.Value);
            }

            // Health is computed off-thread (beatmap conversion can be expensive);
            // the post-mods gameplay beatmap is captured HERE (update thread).
            var token = (cancellation = new CancellationTokenSource()).Token;
            var gameplayBeatmap = player?.GameplayState?.Beatmap;

            Task.Run(() => computeData(gameplayBeatmap, token), token).ContinueWith(t => Schedule(() =>
            {
                if (token.IsCancellationRequested || IsDisposed)
                    return;

                var data = t.IsCompletedSuccessfully ? t.GetResultSafely() : null;

                if (data != null)
                    buildContent(data);
            }));
        }

        #region computing

        private GraphData? computeData(IBeatmap? playable, CancellationToken token)
        {
            try
            {
                if (score.HitEvents.Count == 0)
                    return null;

                playable ??= beatmapManager.GetWorkingBeatmap(score.BeatmapInfo).GetPlayableBeatmap(score.Ruleset, score.Mods);

                if (playable.HitObjects.Count == 0)
                    return fallbackData();

                double drainStart = playable.HitObjects[0].StartTime;
                double gameplayEnd = playable.HitObjects[^1].GetEndTime();

                if (gameplayEnd <= drainStart)
                    return fallbackData();

                // The real processor, as Player sets it up (Classic-type mods bring their own).
                var processor = score.Mods.OfType<IApplicableHealthProcessor>().FirstOrDefault()?.CreateHealthProcessor(drainStart)
                                ?? score.Ruleset.CreateInstance().CreateHealthProcessor(drainStart);

                // Without this the first 0 HP triggers HasFailed and ApplyResult ignores the rest: the
                // graph would be flat after the first near-death.
                processor.Failed += () => false;
                processor.ApplyBeatmap(playable);

                double drainRate = (processor as DrainingHealthProcessor)?.DrainRate ?? 0;

                // The same drain-free periods DrainingHealthProcessor sets up (breaks: from the end of the
                // last object before the break starts to the start of the next one).
                var noDrain = new PeriodTracker(playable.Breaks.Select(b => new Period(
                    playable.HitObjects.Select(h => h.GetEndTime()).Where(t => t <= b.StartTime).DefaultIfEmpty(double.MinValue).Last(),
                    playable.HitObjects.Select(h => h.StartTime).Where(t => t >= b.EndTime).DefaultIfEmpty(double.MaxValue).First())));

                double[] samples = new double[samples_count];
                Array.Fill(samples, double.MaxValue);

                double cursor = drainStart;

                void record(double time)
                {
                    int i = (int)Math.Clamp((time - drainStart) / (gameplayEnd - drainStart) * (samples_count - 1), 0, samples_count - 1);
                    // Minimum per bucket: preserves the miss V shapes that plain sampling would swallow.
                    samples[i] = Math.Min(samples[i], processor.Health.Value);
                }

                void drainTo(double target)
                {
                    target = Math.Clamp(target, drainStart, gameplayEnd);

                    while (cursor < target)
                    {
                        double next = Math.Min(cursor + 50, target);

                        if (drainRate > 0 && !noDrain.IsInAny((cursor + next) / 2))
                            processor.Health.Value -= drainRate * (next - cursor);

                        record(next);
                        cursor = next;
                    }
                }

                record(drainStart);

                // Replay the judgements IN LIST ORDER (the real application order: the processor's
                // end-of-combo bonuses depend on it, don't reorder).
                foreach (var e in score.HitEvents)
                {
                    token.ThrowIfCancellationRequested();

#pragma warning disable CS0618
                    if (e.Result == HitResult.LegacyComboIncrease)
                        continue;
#pragma warning restore CS0618

                    double eventTime = e.HitObject.GetEndTime() + e.TimeOffset;

                    drainTo(eventTime);
                    processor.ApplyResult(new JudgementResult(e.HitObject, e.HitObject.Judgement) { Type = e.Result });
                    record(Math.Clamp(eventTime, drainStart, gameplayEnd));
                }

                drainTo(gameplayEnd);
                fillEmpty(samples);

                return new GraphData(samples, buildMarkers(drainStart, gameplayEnd));
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch
            {
                // Corrupt beatmap / failed conversion: a cheap simulation beats an empty frame.
                return fallbackData();
            }
        }

        /// <summary>
        /// Cheap simulation (the previous implementation) for when the beatmap isn't available: rises
        /// with hits, drops on misses.
        /// </summary>
        private GraphData? fallbackData()
        {
            var basics = score.HitEvents.Where(e => e.Result.IsBasic()).OrderBy(e => e.HitObject.StartTime).ToList();

            if (basics.Count < 2)
                return null;

            double start = basics[0].HitObject.StartTime;
            double end = basics[^1].HitObject.StartTime;

            if (end <= start)
                return null;

            int n = basics.Count;
            double[] times = new double[n];
            double[] values = new double[n];
            double hp = 0.85;

            for (int i = 0; i < n; i++)
            {
                hp = Math.Clamp(hp + hpDelta(basics[i].Result), 0, 1);
                times[i] = basics[i].HitObject.StartTime;
                values[i] = hp;
            }

            double[] samples = new double[samples_count];
            int pointer = 0;

            for (int s = 0; s < samples_count; s++)
            {
                double t = start + (end - start) * s / (samples_count - 1);

                while (pointer < n - 1 && times[pointer + 1] <= t)
                    pointer++;

                samples[s] = values[pointer];
            }

            return new GraphData(samples, buildMarkers(start, end));
        }

        private List<Marker> buildMarkers(double domainStart, double domainEnd)
        {
            int[] missCount = new int[marker_columns];
            int[] mehCount = new int[marker_columns];
            int[] okCount = new int[marker_columns];
            double[] missTime = new double[marker_columns];
            double[] mehTime = new double[marker_columns];
            double[] okTime = new double[marker_columns];

            foreach (var e in score.HitEvents)
            {
                if (!e.Result.IsBasic())
                    continue;

                double t = e.HitObject.StartTime;
                int m = (int)Math.Clamp((t - domainStart) / (domainEnd - domainStart) * (marker_columns - 1), 0, marker_columns - 1);

                if (!e.Result.IsHit())
                {
                    missCount[m]++;
                    missTime[m] += t;
                }
                else if (e.Result == HitResult.Meh)
                {
                    mehCount[m]++;
                    mehTime[m] += t;
                }
                else if (e.Result == HitResult.Ok || e.Result == HitResult.Good)
                {
                    okCount[m]++;
                    okTime[m] += t;
                }
            }

            var markers = new List<Marker>();

            for (int m = 0; m < marker_columns; m++)
            {
                int count;
                double meanTime;
                int type;

                if (missCount[m] > 0)
                {
                    count = missCount[m];
                    meanTime = missTime[m] / count;
                    type = 0;
                }
                else if (mehCount[m] > 0)
                {
                    count = mehCount[m];
                    meanTime = mehTime[m] / count;
                    type = 1;
                }
                else if (okCount[m] > 0)
                {
                    count = okCount[m];
                    meanTime = okTime[m] / count;
                    type = 2;
                }
                else
                    continue;

                float xNorm = (float)Math.Clamp((meanTime - domainStart) / (domainEnd - domainStart), 0, 1);
                markers.Add(new Marker(xNorm, count, type));
            }

            return markers;
        }

        private static void fillEmpty(double[] samples)
        {
            double last = 1;

            for (int i = 0; i < samples.Length; i++)
            {
                if (samples[i] == double.MaxValue)
                    samples[i] = last;
                else
                    last = samples[i];
            }
        }

        private static double hpDelta(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                case HitResult.Great:
                    return 0.012;

                case HitResult.Good:
                    return 0.008;

                case HitResult.Ok:
                    return 0.005;

                case HitResult.Meh:
                    return 0.002;

                default:
                    return result.IsHit() ? 0.008 : -0.2;
            }
        }

        #endregion

        #region drawing

        private void buildContent(GraphData data)
        {
            // Short smoothing for stable's gentle slopes.
            double[] smooth = new double[samples_count];

            for (int s = 0; s < samples_count; s++)
            {
                int lo = Math.Max(0, s - 1);
                int hi = Math.Min(samples_count - 1, s + 1);
                double sum = 0;

                for (int k = lo; k <= hi; k++)
                    sum += data.Samples[k];

                smooth[s] = sum / (hi - lo + 1);
            }

            Vector2 pointAt(int s) => new Vector2(region.X * s / (samples_count - 1), lineY(smooth[s]));

            var content = new List<Drawable>(data.Markers.Count * 3 + 4);

            // The shadow is a single path for the whole line: a path composites its own segments with max
            // blending, so it stays flat where the per-segment boxes used to double up into darker joints.
            var shadowVertices = new List<Vector2>(samples_count);

            for (int s = 0; s < samples_count; s++)
                shadowVertices.Add(pointAt(s));

            content.Add(createPath(shadow_radius, Color4.Black, shadowVertices, 0.45f));

            // The line itself is split into runs of consecutive segments that sit on the same side of half
            // health, one path per run. A segment takes the colour of its END sample - exactly what the
            // per-segment boxes did - so where the colour flips does not move.
            int runStart = 0;
            bool runHigh = smooth[1] > 0.5;

            for (int s = 2; s < samples_count; s++)
            {
                bool isHigh = smooth[s] > 0.5;

                if (isHigh == runHigh)
                    continue;

                addRun(content, pointAt, runStart, s - 1, runHigh);
                runStart = s - 1;
                runHigh = isHigh;
            }

            addRun(content, pointAt, runStart, samples_count - 1, runHigh);

            foreach (var marker in data.Markers)
            {
                var colour = marker.Type switch
                {
                    0 => marker_miss,
                    1 => marker_meh,
                    _ => marker_ok,
                };

                float mx = Math.Clamp(marker.XNorm * region.X, 6, region.X - 6);
                int si = Math.Clamp((int)(marker.XNorm * (samples_count - 1)), 0, samples_count - 1);
                float my = Math.Clamp(lineY(smooth[si]), 7, region.Y - 7);

                // Black outline plus a coloured cross, pinned onto the line.
                content.Add(new SpriteIcon
                {
                    Origin = Anchor.Centre,
                    Position = new Vector2(mx, my),
                    Size = new Vector2(12),
                    Icon = FontAwesome.Solid.Times,
                    Colour = Color4.Black,
                });
                content.Add(new SpriteIcon
                {
                    Origin = Anchor.Centre,
                    Position = new Vector2(mx, my),
                    Size = new Vector2(8.5f),
                    Icon = FontAwesome.Solid.Times,
                    Colour = colour,
                });

                if (marker.Count > 1)
                {
                    // Dark pill with the cluster colour as border; flips sideways/upwards when it would
                    // land outside the frame.
                    string label = $"{marker.Count}x";
                    float pillWidth = 12 + 7 * label.Length;
                    bool flipX = mx + 7.5f + pillWidth > region.X - 2;
                    bool flipY = my - 20 < 2;

                    content.Add(new Container
                    {
                        AutoSizeAxes = Axes.Both,
                        Origin = flipY ? (flipX ? Anchor.TopRight : Anchor.TopLeft) : (flipX ? Anchor.BottomRight : Anchor.BottomLeft),
                        Position = new Vector2(flipX ? mx - 7.5f : mx + 7.5f, flipY ? my + 5 : my - 5),
                        Masking = true,
                        CornerRadius = 7,
                        BorderThickness = 1.4f,
                        BorderColour = colour,
                        EdgeEffect = new EdgeEffectParameters
                        {
                            Type = EdgeEffectType.Shadow,
                            Colour = Color4.Black.Opacity(0.5f),
                            Radius = 3,
                        },
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = new Color4(15, 15, 15, 255),
                                Alpha = 0.8f,
                            },
                            new OsuSpriteText
                            {
                                Text = label,
                                Font = LegacyFonts.Get(12.8f, FontWeight.Bold),
                                Margin = new MarginPadding { Horizontal = 4.5f, Vertical = 1 },
                                Shadow = false,
                            },
                        },
                    });
                }
            }

            InternalChild = revealMask = new Container
            {
                Masking = true,
                Size = new Vector2(animate ? 0 : region.X, region.Y),
                Children = content,
            };

            // The reveal starts HERE (the content arrives async; LoadComplete may have run a while ago).
            if (animate)
                revealMask.Delay(300).ResizeWidthTo(region.X, 4000);
        }

        private static void addRun(ICollection<Drawable> target, Func<int, Vector2> pointAt, int from, int to, bool high)
        {
            var vertices = new List<Vector2>(to - from + 1);

            for (int s = from; s <= to; s++)
                vertices.Add(pointAt(s));

            target.Add(createPath(line_radius, high ? Color4.YellowGreen : Color4.Red, vertices));
        }

        private static Path createPath(float radius, Color4 colour, List<Vector2> vertices, float alpha = 1)
        {
            // A path auto-sizes to its vertices *including* the stroke radius and shifts its geometry by that
            // same amount. Pushing every vertex at least one radius in from the origin makes that shift a fixed
            // +radius on both axes, which the -radius position cancels - otherwise the vertical shift would
            // depend on where the topmost vertex happens to be.
            var path = new Path
            {
                Position = new Vector2(-radius, -radius),
                PathRadius = radius,
                Colour = colour,
                Alpha = alpha,
            };

            foreach (var vertex in vertices)
                path.AddVertex(vertex + new Vector2(radius, radius));

            return path;
        }

        private static float lineY(double health) => top_inset + (float)(1 - health) * (region.Y - top_inset - bottom_inset);

        #endregion

        protected override void Dispose(bool isDisposing)
        {
            cancellation?.Cancel();
            base.Dispose(isDisposing);
        }

        private sealed record GraphData(double[] Samples, List<Marker> Markers);

        private readonly record struct Marker(float XNorm, int Count, int Type);

        public sealed record TooltipData(double NegAvg, double PosAvg, double UnstableRate);

        /// <summary>
        /// stable's boxed hint: "Accuracy:" + average error + unstable rate, three lines in a little dark
        /// box. The game's TooltipContainer clamps it to the screen on its own.
        /// </summary>
        public partial class LegacyHintTooltip : VisibilityContainer, ITooltip<TooltipData?>
        {
            private OsuSpriteText errorText = null!;
            private OsuSpriteText urText = null!;

            [BackgroundDependencyLoader]
            private void load()
            {
                AutoSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 3;
                BorderThickness = 1;
                BorderColour = Color4.White.Opacity(0.3f);

                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black,
                        Alpha = 0.8f,
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding { Horizontal = 7, Vertical = 5 },
                        Spacing = new Vector2(0, 1),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = LocalisableString.Interpolate($"{RankingsStrings.StatAccuracy}:"),
                                Font = LegacyFonts.Get(12.8f, FontWeight.Bold),
                            },
                            errorText = new OsuSpriteText
                            {
                                Font = LegacyFonts.Get(12.8f),
                                Colour = new Color4(255, 220, 120, 255),
                            },
                            urText = new OsuSpriteText
                            {
                                Font = LegacyFonts.Get(12.8f),
                                Colour = new Color4(255, 220, 120, 255),
                            },
                        },
                    },
                };
            }

            public void SetContent(TooltipData? content)
            {
                if (content == null)
                    return;

                errorText.Text = RankingStatisticsStrings.AverageHitErrorLine(content.NegAvg, content.PosAvg);
                urText.Text = LocalisableString.Interpolate($"{RankingStatisticsStrings.UnstableRateTitle}: {content.UnstableRate:0.00}");
            }

            public void Move(Vector2 pos) => Position = pos;

            protected override void PopIn() => this.FadeIn(150, Easing.OutQuint);

            protected override void PopOut() => this.FadeOut(150, Easing.OutQuint);
        }
    }
}
