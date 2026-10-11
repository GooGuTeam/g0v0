// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Scoring;
using osu.Game.Localisation;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Screens.Ranking.Expanded.Statistics
{
    public partial class PerformanceStatistic : StatisticDisplay, IHasTooltip
    {
        public LocalisableString TooltipText { get; private set; }

        public readonly Bindable<double?> Performance = new Bindable<double?>();
        public readonly Bindable<ScoreInfo?> ComparisonScore = new Bindable<ScoreInfo?>();

        private readonly ScoreInfo score;
        private readonly Bindable<int> performance = new Bindable<int>();
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private RollingCounter<int> counter = null!;
        private FillFlowContainer v2Content = null!;
        private OsuSpriteText maximumText = null!;
        private TruncatingSpriteText fullComboText = null!;
        private V2StatisticChange personalBestChange = null!;

        public void SetComparisonDebugPreview(bool enabled)
        {
            if (UseV2Style)
                personalBestChange.DebugPreview = enabled;
        }

        public PerformanceStatistic(ScoreInfo score)
            : base(BeatmapsetsStrings.ShowScoreboardHeaderspp)
        {
            this.score = score;
        }

        [BackgroundDependencyLoader]
        private void load(BeatmapDifficultyCache difficultyCache)
        {
            if (UseV2Style)
            {
                AddInternal(fullComboText = new TruncatingSpriteText
                {
                    Name = "If FC performance",
                    Y = -18,
                    Font = OsuFont.Default.With(size: 12),
                    Colour = Colour4.FromHex("#BDEFFF"),
                    Shadow = false,
                    BypassAutoSizeAxes = Axes.Both,
                });
                v2Content.Add(personalBestChange = new V2StatisticChange
                {
                    Name = "Personal best PP change",
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Margin = new MarginPadding { Left = 3 },
                    Suffix = "pp",
                });
                ComparisonScore.BindValueChanged(_ => updateComparison());
                Performance.BindValueChanged(_ => updateComparison());
            }

            if (score.PP.HasValue)
                setPerformanceValue(score.PP.Value);

            if (UseV2Style || !score.PP.HasValue)
                _ = calculatePerformanceAsync(difficultyCache, cancellationTokenSource.Token);
        }

        private async Task calculatePerformanceAsync(BeatmapDifficultyCache difficultyCache, CancellationToken token)
        {
            try
            {
                var attributes = await difficultyCache.GetDifficultyAsync(score.BeatmapInfo!, score.Ruleset, score.Mods, token).ConfigureAwait(false);
                var calculator = score.Ruleset.CreateInstance().CreatePerformanceCalculator();

                if (attributes?.DifficultyAttributes == null || calculator == null)
                    return;

                double pp = score.PP ?? (await calculator.CalculateAsync(score, attributes.Value.DifficultyAttributes, token).ConfigureAwait(false)).Total;

                double? fullComboPP = null;
                if (UseV2Style && FullComboScoreInfo.Create(score) is { } fullComboScore)
                    fullComboPP = (await calculator.CalculateAsync(fullComboScore, attributes.Value.DifficultyAttributes, token).ConfigureAwait(false)).Total;

                token.ThrowIfCancellationRequested();
                Schedule(() =>
                {
                    setPerformanceValue(pp);
                    if (!UseV2Style)
                        return;

                    if (attributes.Value.PerformanceAttributes?.Total is > 0 and var maxPP && double.IsFinite(maxPP))
                        maximumText.Text = $"/{Math.Round(maxPP, MidpointRounding.AwayFromZero):0}";

                    if (fullComboPP is { } fc && double.IsFinite(fc))
                        fullComboText.Text = ResultsScreenStrings.IfFullCombo(Math.Round(fc, MidpointRounding.AwayFromZero));
                });
            }
            catch (OperationCanceledException)
            {
                // Leaving the results screen cancels any outstanding calculation.
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Could not calculate results performance.");
            }
        }

        private void setPerformanceValue(double pp)
        {
            if (!double.IsFinite(pp))
                return;

            Performance.Value = pp;
            performance.Value = (int)Math.Round(pp, MidpointRounding.AwayFromZero);

            if (!score.BeatmapInfo!.Status.GrantsPerformancePoints())
            {
                Alpha = 0.5f;
                TooltipText = ResultsScreenStrings.NoPPForUnrankedBeatmaps;
            }
            else if (hasUnrankedMods(score))
            {
                Alpha = 0.5f;
                TooltipText = ResultsScreenStrings.NoPPForUnrankedMods;
            }
            else if (score.Rank == ScoreRank.F)
            {
                Alpha = 0.5f;
                TooltipText = ResultsScreenStrings.NoPPForFailedScores;
            }
            else
            {
                Alpha = 1f;
                TooltipText = default;
            }
        }

        private void updateComparison()
        {
            personalBestChange.Difference.Value = Performance.Value - ComparisonScore.Value?.PP;
            personalBestChange.Pending.Value = ComparisonScore.Value?.PP != null && Performance.Value == null;
        }

        private static bool hasUnrankedMods(ScoreInfo scoreInfo)
        {
            IEnumerable<Mod> modsToCheck = scoreInfo.Mods;

            if (scoreInfo.IsLegacyScore)
                modsToCheck = modsToCheck.Where(m => m is not ModClassic);

            return modsToCheck.Any(m => !m.Ranked);
        }

        public override void Appear()
        {
            base.Appear();
            counter.Current.BindTo(performance);
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellationTokenSource.Cancel();
            base.Dispose(isDisposing);
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();
            if (UseV2Style)
                fullComboText.MaxWidth = Math.Max(1, DrawWidth);
        }

        protected override Drawable CreateContent()
        {
            counter = new StatisticCounter
            {
                UseV2Style = UseV2Style,
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
            };

            if (!UseV2Style)
                return counter;

            return v2Content = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Children = new Drawable[]
                {
                    counter,
                    maximumText = new OsuSpriteText
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Font = OsuFont.Numeric.With(size: 10),
                        Colour = ColourInfo.GradientVertical(Colour4.White, Colour4.FromHex("#AFE8FF")),
                        Shadow = false,
                    },
                },
            };
        }
    }
}
