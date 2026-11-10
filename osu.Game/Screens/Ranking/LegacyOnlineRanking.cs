// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.
// Originally ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Scoring;
using osu.Game.Screens.Select;
using osu.Game.Skinning;
using osu.Game.Skinning.Select;
using osu.Game.Users;
using osuTK;
using osuTK.Graphics;
using Realms;

namespace osu.Game.Screens.Ranking
{
    /// <summary>
    /// Stable's online ranking page below the results (<c>RankingDialog</c>), laid out from its source in 640 x 480 units scaled by 1.6:
    /// the local rank and the replay and favourite options in the LOCAL band, the user panel and the ranking chart once the score has
    /// been submitted in the ONLINE band, and the beatmap's play count and rating in the BEATMAP band. Stable only shows it for the
    /// local user's own plays.
    /// </summary>
    /// <remarks>
    /// Stable draws the bands from its own <c>ranking-dialog-left</c>, <c>-middle</c> and <c>-right</c>, which skins can't replace and the
    /// bundled classic skin doesn't have, so they are drawn here in their colours. Lazer has no ranking charts or beatmap ratings, so the
    /// chart is the overall ranking alone, with its "to next rank" left empty, and the rating says it isn't available.
    /// </remarks>
    public partial class LegacyOnlineRanking : CompositeDrawable
    {
        /// <summary>
        /// The height of stable's dialog textures, 709 px (443 units).
        /// </summary>
        public const float DIALOG_HEIGHT = 709;

        private const float unit = 1.6f;

        // RankingDialog's colours.
        private static readonly Color4 gold = new Color4(255, 203, 33, 255);
        private static readonly Color4 colour_increase = new Color4(103, 157, 17, 255);
        private static readonly Color4 colour_decrease = new Color4(203, 35, 18, 255);
        private static readonly Color4 colour_new = new Color4(31, 175, 231, 255);
        private static readonly Color4 row_colour = new Color4(40, 40, 40, 255);
        private static readonly Color4 checkbox_colour = new Color4(236, 117, 140, 255);

        // the chart's columns: their offsets from the table's left edge and widths, in units.
        private static readonly float[] column_x = { 0, 100, 170, 240, 310, 420, 530 };
        private static readonly float[] column_width = { 100, 70, 70, 70, 110, 110, 70 };

        private const float header_y = 170;
        private const float row_y = 180;
        private const float cell_height = 23;

        private readonly ScoreInfo score;

        /// <summary>
        /// Whether the page shows: only for the local user's own plays.
        /// </summary>
        public bool IsShown { get; private set; }

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        [Resolved]
        private UserStatisticsWatcher? statisticsWatcher { get; set; }

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        private readonly IBindable<ScoreBasedUserStatisticsUpdate?> latestUpdate = new Bindable<ScoreBasedUserStatisticsUpdate?>();

        private bool onlineRetrieval;
        private bool statisticsWaitExpired;
        private bool chartShown;

        private OsuSpriteText localRankText = null!;
        private OsuSpriteText? loadingText;
        private Container chart = null!;
        private Sprite? modeIcon;
        private ScoreBasedUserStatisticsUpdate? pendingUpdate;
        private bool bestFetched;
        private int? bestPosition;
        private bool thisIsBest;
        private BestScore? onlineBest;
        private bool ppCalculated;
        private double? playPp;
        private CancellationTokenSource? cancellation;
        private FillFlowContainer beatmapInfo = null!;

        public LegacyOnlineRanking(ScoreInfo score)
        {
            this.score = score;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            RelativeSizeAxes = Axes.X;

            if (api.LocalUser.Value == null || score.UserID != api.LocalUser.Value.OnlineID)
            {
                Height = 0;
                return;
            }

            IsShown = true;
            Height = DIALOG_HEIGHT;

            // OnlineRetrival: a submitted score on a ranked, approved, qualified or loved beatmap.
            onlineRetrieval = api.IsLoggedIn && score.BeatmapInfo?.Status >= BeatmapOnlineStatus.Ranked && score.BeatmapInfo.OnlineID > 0;

            var children = new List<Drawable>
            {
                new DialogBackground(),
                // the local rank, centred 410 units left of the right edge.
                (localRankText = new OsuSpriteText
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.Centre,
                    Position = new Vector2(-410, 20) * unit,
                    Font = LegacyFonts.Get(22 * unit),
                    Colour = gold,
                    Alpha = 0,
                }),
                new LegacyCheckbox(LegacyRankingDialogStrings.SaveReplay, onExportReplay)
                {
                    Anchor = Anchor.TopRight,
                    Position = new Vector2(-160, 10) * unit,
                },
            };

            if (score.BeatmapInfo?.BeatmapSet?.OnlineID > 0)
            {
                children.Add(new LegacyCheckbox(LegacyRankingDialogStrings.AddOnlineFavourite, onToggleFavourite)
                {
                    Anchor = Anchor.TopRight,
                    Position = new Vector2(-160, 30) * unit,
                });
            }

            if (onlineRetrieval)
            {
                children.Add(new LegacyFooterUser
                {
                    Anchor = Anchor.TopRight,
                    Position = new Vector2(-510, 60) * unit,
                });

                // pulses until the submission's results arrive.
                children.Add(loadingText = new OsuSpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.Centre,
                    Y = 180 * unit,
                    Font = LegacyFonts.Get(22 * unit),
                    Text = LegacyRankingDialogStrings.LoadingResults,
                    Alpha = 0,
                });

                var modeTexture = skin.GetStableTexture($@"mode-{score.Ruleset.ShortName}") ?? skins.DefaultClassicSkin.GetStableTexture($@"mode-{score.Ruleset.ShortName}");

                if (modeTexture != null)
                {
                    children.Add(modeIcon = new Sprite
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.Centre,
                        Position = new Vector2(-100, 230) * unit,
                        Texture = modeTexture,
                        Scale = new Vector2(0.8f),
                        Blending = BlendingParameters.Additive,
                        Alpha = 0,
                    });
                }
            }

            // the chart sits centred: 3 units right of where a 640 unit wide screen would start.
            children.Add(chart = new Container
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopLeft,
                Position = new Vector2(3 - 320, 0) * unit,
                AutoSizeAxes = Axes.Both,
            });

            // the beatmap's title and play count, centred on each other from (170, 301).
            children.Add(beatmapInfo = new FillFlowContainer
            {
                Position = new Vector2(170, 301) * unit,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Alpha = 0,
            });

            children.Add(new RatingDisplay
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
            });

            InternalChildren = children.ToArray();

            fetchLocalRank();

            if (onlineRetrieval)
            {
                fetchBest();
                calculatePp();
                fetchBeatmapInfo();
            }

            if (statisticsWatcher != null)
            {
                latestUpdate.BindTo(statisticsWatcher.LatestUpdate);
                latestUpdate.BindValueChanged(update =>
                {
                    if (update.NewValue != null && update.NewValue.Score.MatchesOnlineID(score))
                        Schedule(() => ShowStatisticsUpdate(update.NewValue));
                }, true);
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Historical scores and failed submissions may never produce a matching watcher update.
            Scheduler.AddDelayed(() =>
            {
                statisticsWaitExpired = true;
                tryShowChart();
            }, 10000);

            if (loadingText != null && !chartShown)
            {
                // fades and pops in over 200ms, settles over a second, then breathes between 1 and 1.1.
                loadingText.FadeIn(200, Easing.Out);
                loadingText.ScaleTo(0).ScaleTo(1.2f, 200, Easing.Out).Then().ScaleTo(1, 1000, Easing.Out)
                           .Then().ScaleTo(1.1f, 1200, Easing.Out).Then().ScaleTo(1, 1200, Easing.In).Loop(1200);
            }
        }

        #region chart

        /// <summary>
        /// Shows the ranking chart once this score's submission has updated the user's statistics (<c>displayOnlineRanking</c>).
        /// </summary>
        public void ShowStatisticsUpdate(ScoreBasedUserStatisticsUpdate update)
        {
            pendingUpdate = update;
            tryShowChart();
        }

        private void tryShowChart()
        {
            // the beatmap row waits for the user's best on the beatmap and this play's pp, as stable's waits for the whole response.
            if (!onlineRetrieval || chartShown || (!statisticsWaitExpired && (pendingUpdate == null || !bestFetched || !ppCalculated)))
                return;

            chartShown = true;

            if (loadingText != null)
            {
                loadingText.ClearTransforms();
                loadingText.Scale = Vector2.One;
                loadingText.FadeOut(500);
            }

            modeIcon?.FadeTo(0.1f, 2000);
            modeIcon?.RotateTo(MathHelper.RadiansToDegrees(0.1f), 5000, Easing.Out);

            LocalisableString[] headers =
            {
                LegacyRankingDialogStrings.Overall,
                LegacyRankingDialogStrings.Accuracy,
                LegacyRankingDialogStrings.MaxCombo,
                LegacyRankingDialogStrings.RankedScore,
                LegacyRankingDialogStrings.TotalScore,
                LegacyRankingDialogStrings.Performance,
            };

            for (int i = 0; i < headers.Length; i++)
                chart.Add(new Cell(column_width[i + 1] - 1, null, headers[i], 9, bold: true) { Position = new Vector2(column_x[i + 1], header_y) * unit });

            // the server's charts, in its order: this beatmap's ranking, then the overall one.
            addChartRow(1, LegacyRankingDialogStrings.BeatmapRanking, true, beatmapStats());
            if (pendingUpdate != null)
                addChartRow(2, LegacyRankingDialogStrings.OverallRanking, false, overallStats(pendingUpdate));

            // each cell fades in to 90% along a diagonal sweep, from 200ms + (x + y - 150) / 4 to 200ms + (x + y - 150) * 2, x being its
            // position on screen.
            float tableLeft = DrawWidth / unit / 2 - 317;

            foreach (var cell in chart.OfType<Cell>())
            {
                float sweep = tableLeft + cell.X / unit + cell.Y / unit - 150;
                double start = 200 + sweep / 4;
                cell.Alpha = 0;
                cell.Delay(start).FadeTo(0.9f, Math.Max(0, 200 + sweep * 2 - start));
            }
        }

        /// <summary>
        /// A chart value before and after this score. A <c>null</c> after means the column has no data.
        /// </summary>
        private readonly record struct ChartValue(double? After, double? Before);

        // rank, accuracy, max combo, ranked score, total score and pp.
        private static readonly string[] value_formats = { @"#{0:#,0}", @"{0:0.00}%", @"{0:#,0}x", @"{0:#,0}", @"{0:#,0}", @"{0:#,0}pp" };

        private void addChartRow(int index, LocalisableString name, bool beatmapChart, ChartValue[] values)
        {
            // rows alternate (40, 40, 40) and (20, 20, 20), 24 units apart.
            var rowColour = index % 2 == 0 ? new Color4(20, 20, 20, 255) : new Color4(40, 40, 40, 255);
            float y = row_y + (index - 1) * 24;

            var row = new List<Cell>
            {
                new Cell(column_width[0] - 1, rowColour, name, 11, bold: true, colour: Color4.Orange) { Position = new Vector2(column_x[0], y) * unit },
            };

            for (int i = 0; i < values.Length; i++)
                row.AddRange(valueCells(new Vector2(column_x[i + 1], y), column_width[i + 1], rowColour, values[i], value_formats[i], i == 0, beatmapChart, i == 5));

            foreach (var cell in row)
            {
                cell.Row = row;
                chart.Add(cell);
            }
        }

        /// <summary>
        /// A chart value's cell, as stable shows it: a value without a previous one is "new!" in blue, a change shows in green or red with
        /// its size (+ meaning better), and a beatmap score that didn't improve on the user's best shows in grey beside that best.
        /// </summary>
        private static IEnumerable<Cell> valueCells(Vector2 position, float width, Color4 rowColour, ChartValue value, string format, bool lowerIsBetter, bool beatmapChart,
                                                    bool isPp)
        {
            if (value.After == null)
            {
                yield return new Cell(width - 1, rowColour, "-\n ", 11, colour: Color4.Gray) { Position = position * unit };

                yield break;
            }

            double after = value.After.Value;
            double before = value.Before ?? 0;
            bool hasBefore = value.Before != null && (isPp || before > 0);
            bool hasAfter = isPp || after > 0;
            double delta = Math.Round(after - before, 2);
            bool improved = after > before == !lowerIsBetter;

            if (!hasBefore && hasAfter)
            {
                yield return new Cell(width - 1, delta == 0 ? rowColour : colour_new, string.Format(format, after), 11, shadow: delta != 0,
                    secondLine: LegacyRankingDialogStrings.NewMark) { Position = position * unit };
            }
            else if (improved || !beatmapChart)
            {
                string change = delta != 0 ? $"({(improved ? "+" : "-")}{string.Format(format, Math.Abs(delta))})" : @"-";
                yield return new Cell(width - 1, delta == 0 ? rowColour : improved ? colour_increase : colour_decrease, $"{string.Format(format, after)}\n{change}", 11)
                    { Position = position * unit };
            }
            else
            {
                yield return new Cell(width - 1, rowColour, $"{(hasAfter ? string.Format(format, after) : "-")}\n\t", 11, colour: Color4.Gray) { Position = position * unit };
                yield return new Cell(width - 5, null, LegacyRankingDialogStrings.YourBest(hasBefore ? string.Format(format, before) : "-"), 8)
                    { Position = (position + new Vector2(2, 12)) * unit };
            }
        }

        /// <summary>
        /// The beatmap chart: this play against the user's best on the beatmap before it. When this play is the new best, the previous best
        /// is the best of the user's earlier local scores; its rank before is unknown, so the rank shows unchanged.
        /// </summary>
        private ChartValue[] beatmapStats()
        {
            var previous = thisIsBest ? previousLocalBest() : onlineBest;
            double? rank = thisIsBest ? bestPosition : null;

            return new[]
            {
                new ChartValue(rank, rank),
                new ChartValue(score.Accuracy * 100, previous?.Accuracy * 100),
                new ChartValue(score.MaxCombo, previous?.MaxCombo),
                new ChartValue(score.TotalScore, previous?.TotalScore),
                new ChartValue(score.TotalScore, previous?.TotalScore),
                new ChartValue(playPp, previous?.PP),
            };
        }

        private static ChartValue[] overallStats(ScoreBasedUserStatisticsUpdate update)
        {
            var before = update.Before;
            var after = update.After;

            return new[]
            {
                new ChartValue(after.GlobalRank, before.GlobalRank),
                new ChartValue(after.Accuracy, before.Accuracy),
                new ChartValue(after.MaxCombo, before.MaxCombo),
                new ChartValue(after.RankedScore, before.RankedScore),
                new ChartValue(after.TotalScore, before.TotalScore),
                new ChartValue((double?)after.PP, (double?)before.PP),
            };
        }

        private BestScore? previousLocalBest() => realm.Run(r =>
        {
            int beatmapOnlineId = score.BeatmapInfo?.OnlineID ?? 0;

            return r.All<ScoreInfo>()
                    .Filter(@"BeatmapInfo.OnlineID == $0 AND DeletePending == false", beatmapOnlineId)
                    .ToList()
                    .Where(s => s.ID != score.ID && s.UserID == score.UserID && s.Ruleset.ShortName == score.Ruleset.ShortName && s.Date < score.Date)
                    .OrderByDescending(s => s.TotalScore)
                    .Select(s => (BestScore?)new BestScore(s.Accuracy, s.MaxCombo, s.TotalScore, s.PP))
                    .FirstOrDefault();
        });

        /// <summary>
        /// The user's best score on the beatmap, as the beatmap chart compares against it.
        /// </summary>
        private readonly record struct BestScore(double Accuracy, int MaxCombo, long TotalScore, double? PP);

        #endregion

        #region data

        private void fetchLocalRank()
        {
            var beatmapId = score.BeatmapInfo?.ID;

            if (beatmapId == null)
                return;

            // this score's place among the beatmap's local scores, as ScoreManager.InsertScore finds it.
            int rank = realm.Run(r =>
            {
                var localScores = r.All<ScoreInfo>()
                                   .Filter(@"BeatmapInfo.ID == $0 AND DeletePending == false", beatmapId.Value)
                                   .ToList()
                                   .Where(s => s.Ruleset.ShortName == score.Ruleset.ShortName)
                                   .OrderByDescending(s => s.TotalScore)
                                   .ToList();

                return localScores.FindIndex(s => s.ID == score.ID) + 1;
            });

            if (rank >= 1)
            {
                localRankText.Text = LegacyRankingDialogStrings.LocalRanking(rank);
                localRankText.FadeIn(400);
            }
        }

        private void fetchBest()
        {
            var req = new GetScoresRequest(score.BeatmapInfo!, score.Ruleset);

            req.Success += response => Schedule(() =>
            {
                var best = response.UserScore;

                if (best != null)
                {
                    bestPosition = best.Position;
                    thisIsBest = best.Score.MatchesOnlineID(score);
                    onlineBest = thisIsBest ? null : new BestScore(best.Score.Accuracy, best.Score.MaxCombo, best.Score.TotalScore, best.Score.PP);
                }

                bestFetched = true;
                tryShowChart();
            });
            req.Failure += _ => Schedule(() =>
            {
                bestFetched = true;
                tryShowChart();
            });

            api.Queue(req);
        }

        /// <summary>
        /// This play's pp for the beatmap chart: fresh scores don't carry the server's yet.
        /// </summary>
        private void calculatePp()
        {
            var calculator = ModSpecificCalculation.CreatePerformanceCalculator(score.Ruleset.CreateInstance());

            if (score.PP != null || calculator == null || score.BeatmapInfo == null)
            {
                playPp = score.PP;
                ppCalculated = true;
                return;
            }

            var token = (cancellation = new CancellationTokenSource()).Token;

            Task.Run(async () =>
            {
                double? pp = null;

                try
                {
                    var stars = await difficultyCache.GetDifficultyAsync(score.BeatmapInfo, score.Ruleset, score.Mods, token).ConfigureAwait(false);

                    if (stars?.DifficultyAttributes != null)
                        pp = (await calculator.CalculateAsync(score, stars.Value.DifficultyAttributes, token).ConfigureAwait(false)).Total;
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    Logger.Error(e, @"Failed to calculate the pp of a score for its online ranking");
                }

                Schedule(() =>
                {
                    playPp = pp;
                    ppCalculated = true;
                    tryShowChart();
                });
            }, token);
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellation?.Cancel();
            base.Dispose(isDisposing);
        }

        private void fetchBeatmapInfo()
        {
            var req = new GetBeatmapRequest(score.BeatmapInfo!);

            req.Success += beatmap => Schedule(() =>
            {
                if (beatmap.PlayCount <= 0)
                    return;

                var metadata = score.BeatmapInfo!.Metadata;
                string since = beatmap.BeatmapSet?.Ranked?.ToString(@"MMM, yyyy") ?? @"unknown";
                int passRate = (int)(beatmap.PassCount * 100f / beatmap.PlayCount);

                beatmapInfo.Children = new Drawable[]
                {
                    infoLine($"{metadata.Artist} - {metadata.Title} [{score.BeatmapInfo.DifficultyName}] by {metadata.Author.Username}"),
                    infoLine(LocalisableString.Interpolate($"{LegacyRankingDialogStrings.PlaysSince(beatmap.PlayCount, since)} | {LegacyRankingDialogStrings.PassRate(passRate)}")),
                };
                beatmapInfo.FadeIn(200);
            });

            api.Queue(req);

            static OsuSpriteText infoLine(LocalisableString text) => new OsuSpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Font = LegacyFonts.Get(14 * unit),
                Colour = Color4.White.Opacity(200 / 255f),
                Text = text,
            };
        }

        #endregion

        #region actions

        private bool replayExported;

        private void onExportReplay(bool ticked)
        {
            // stable exports when the page closes with the box ticked; this exports once when it's ticked.
            if (!ticked || replayExported)
                return;

            replayExported = true;
            scoreManager.Export(score);
        }

        private void onToggleFavourite(bool ticked)
        {
            int setId = score.BeatmapInfo?.BeatmapSet?.OnlineID ?? 0;

            if (setId <= 0 || !api.IsLoggedIn)
                return;

            api.Queue(new PostBeatmapFavouriteRequest(setId, ticked ? BeatmapFavouriteAction.Favourite : BeatmapFavouriteAction.UnFavourite));
        }

        #endregion

        /// <summary>
        /// One of the chart's cells: a fixed-width text over its background, centred, one or two lines. Hovering any cell of a row brings
        /// the whole row to full opacity.
        /// </summary>
        private partial class Cell : CompositeDrawable
        {
            public List<Cell>? Row { get; set; }

            private readonly Box? background;
            private readonly OsuSpriteText[] lines = { new OsuSpriteText(), new OsuSpriteText() };
            private readonly bool bold;
            private readonly Color4 textColour;

            public Cell(float width, Color4? backgroundColour, LocalisableString text, float size, bool bold = false, Color4? colour = null, bool shadow = false,
                        LocalisableString? secondLine = null)
            {
                this.bold = bold;
                textColour = colour ?? Color4.White;

                Size = new Vector2(width, backgroundColour == null ? 10 : cell_height) * unit;

                if (backgroundColour != null)
                    AddInternal(background = new Box { RelativeSizeAxes = Axes.Both, Colour = backgroundColour.Value });

                foreach (var line in lines)
                {
                    line.Anchor = Anchor.TopCentre;
                    line.Origin = Anchor.TopCentre;
                    AddInternal(line);
                }

                Set(text, size, backgroundColour, shadow, secondLine);
            }

            /// <param name="text">the text: one line, or two split by a line break.</param>
            /// <param name="size">its size, in units.</param>
            /// <param name="backgroundColour">the cell's colour, or <c>null</c> to keep it.</param>
            /// <param name="shadow">whether the text has a shadow.</param>
            /// <param name="secondLine">a second line to show below a one-line <paramref name="text"/>.</param>
            public void Set(LocalisableString text, float size, Color4? backgroundColour, bool shadow, LocalisableString? secondLine = null)
            {
                if (background != null && backgroundColour != null)
                    background.Colour = backgroundColour.Value;

                // stable pads one-line cells to two lines with a tab.
                string[] split = text.ToString().Replace('\t', ' ').Split('\n');

                for (int i = 0; i < lines.Length; i++)
                {
                    if (i == 1 && secondLine != null)
                        lines[i].Text = secondLine.Value;
                    else
                        lines[i].Text = i == 0 && split.Length == 1 ? text : i < split.Length ? split[i] : string.Empty;

                    lines[i].Font = LegacyFonts.Get(size * unit, bold ? FontWeight.Bold : FontWeight.Regular);
                    lines[i].Colour = textColour;
                    lines[i].Shadow = shadow;
                    lines[i].Y = i * 11.5f * unit;
                }
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (Row == null)
                    return false;

                foreach (var cell in Row)
                    cell.FadeTo(1, 100, Easing.Out);

                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                if (Row == null)
                    return;

                foreach (var cell in Row)
                    cell.FadeTo(0.9f, 100, Easing.In);
            }
        }

        /// <summary>
        /// The beatmap rating below the chart: its text centred at y = 355, ten stars 32 units apart below it, dimmed while there is no
        /// rating to show.
        /// </summary>
        private partial class RatingDisplay : CompositeDrawable
        {
            [BackgroundDependencyLoader]
            private void load()
            {
                AutoSizeAxes = Axes.Both;

                var children = new List<Drawable>
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.Centre,
                        Y = 355 * unit,
                        Font = LegacyFonts.Get(16 * unit),
                        Colour = Color4.OrangeRed,
                        Text = LegacyRankingDialogStrings.RatingNotAvailable,
                    },
                };

                for (int i = 0; i < 10; i++)
                {
                    children.Add(new SpriteIcon
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.Centre,
                        Position = new Vector2(-144 + 32 * i, 380) * unit,
                        Size = new Vector2(24 * unit),
                        Icon = FontAwesome.Solid.Star,
                        Colour = new Color4(0, 0, 0, 50),
                    });
                }

                InternalChildren = children.ToArray();
            }
        }

        /// <summary>
        /// The LOCAL, ONLINE and BEATMAP bands of stable's <c>ranking-dialog</c> textures, at their 86% opacity: each a lit title strip
        /// over a darker body, with the LOCAL band giving way to a grey corner behind the options on the right.
        /// </summary>
        private partial class DialogBackground : CompositeDrawable
        {
            [BackgroundDependencyLoader]
            private void load()
            {
                RelativeSizeAxes = Axes.X;
                Height = DIALOG_HEIGHT;
                Alpha = 220 / 255f;

                InternalChildren = new Drawable[]
                {
                    band(0, 205, @"LOCAL", new Color4(97, 78, 13, 255), new Color4(125, 101, 16, 255), new Color4(52, 41, 7, 255), new Color4(64, 51, 8, 255)),
                    band(205, 477, @"ONLINE", new Color4(99, 31, 82, 255), new Color4(125, 41, 101, 255), new Color4(52, 17, 41, 255), new Color4(64, 21, 51, 255)),
                    band(477, DIALOG_HEIGHT, @"BEATMAP", new Color4(22, 68, 100, 255), new Color4(31, 85, 125, 255), new Color4(15, 36, 51, 255), new Color4(18, 44, 63, 255)),
                    // the grey corner of ranking-dialog-right, shaded where it meets the gold band.
                    new Container
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Size = new Vector2(280, 205),
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = new Color4(41, 41, 41, 255),
                            },
                            new Box
                            {
                                RelativeSizeAxes = Axes.Y,
                                Width = 30,
                                Colour = ColourInfo.GradientHorizontal(new Color4(27, 28, 30, 255), new Color4(41, 41, 41, 255)),
                            },
                        },
                    },
                };
            }

            private static Container band(float top, float bottom, string label, Color4 stripTop, Color4 stripBottom, Color4 bodyTop, Color4 body)
            {
                const float strip_height = 53;

                return new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Y = top,
                    Height = bottom - top,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = strip_height,
                            Colour = ColourInfo.GradientVertical(stripTop, stripBottom),
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Y = strip_height,
                            Height = 25,
                            Colour = ColourInfo.GradientVertical(bodyTop, body),
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Y = strip_height + 25,
                            Height = bottom - top - strip_height - 25,
                            Colour = body,
                        },
                        // the band's name, faint and thin, its foot on the strip's lower edge and its top cut by the strip.
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = strip_height,
                            Masking = true,
                            Child = new OsuSpriteText
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                X = 2,
                                Y = 14,
                                Font = OsuFont.Torus.With(size: 72, weight: FontWeight.Light),
                                Colour = Color4.White.Opacity(0.3f),
                                Text = label,
                            },
                        },
                    },
                };
            }
        }

        /// <summary>
        /// Stable's <c>pCheckbox</c> at 0.7: a pink ring that fills when ticked, its label 20 units to the right.
        /// </summary>
        private partial class LegacyCheckbox : OsuClickableContainer
        {
            private readonly LocalisableString label;
            private readonly Action<bool> onToggle;

            private CircularContainer circle = null!;
            private Box fill = null!;
            private bool ticked;

            private Sample? checkOn;
            private Sample? checkOff;

            public LegacyCheckbox(LocalisableString label, Action<bool> onToggle)
            {
                this.label = label;
                this.onToggle = onToggle;
            }

            [BackgroundDependencyLoader]
            private void load(AudioManager audio)
            {
                AutoSizeAxes = Axes.Both;

                checkOn = audio.Samples.Get(@"UI/check-on");
                checkOff = audio.Samples.Get(@"UI/check-off");

                Children = new Drawable[]
                {
                    circle = new CircularContainer
                    {
                        Origin = Anchor.Centre,
                        Position = new Vector2(8) * unit,
                        Size = new Vector2(16 * 0.7f),
                        Masking = true,
                        BorderThickness = 2,
                        BorderColour = checkbox_colour,
                        Child = fill = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = checkbox_colour,
                            Alpha = 0,
                            AlwaysPresent = true,
                        },
                    },
                    new OsuSpriteText
                    {
                        Origin = Anchor.CentreLeft,
                        Position = new Vector2(8 + 20 * 0.7f, 8) * unit,
                        Font = LegacyFonts.Get(15 * 0.7f * unit),
                        Text = label,
                    },
                };

                Action = () =>
                {
                    ticked = !ticked;
                    fill.Alpha = ticked ? 1 : 0;

                    if (ticked)
                        circle.ScaleTo(1.2f).ScaleTo(1, 350, Easing.Out);

                    (ticked ? checkOn : checkOff)?.Play();
                    onToggle(ticked);
                };
            }
        }
    }
}
