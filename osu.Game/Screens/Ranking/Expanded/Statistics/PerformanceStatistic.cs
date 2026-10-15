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
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Scoring;
using osu.Game.Localisation;
using osu.Game.Rulesets.Mods;

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
        private OsuHoverContainer fullComboArea = null!;
        private TruncatingSpriteText fullComboText = null!;
        private V2StatisticChange personalBestChange = null!;

        private double? fullComboPP;
        private double? perfectPP;
        private bool showPerfectEstimate;

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
                AddInternal(fullComboArea = new OsuHoverContainer
                {
                    Name = "If FC performance",
                    Y = -18,
                    AutoSizeAxes = Axes.Both,
                    BypassAutoSizeAxes = Axes.Both,
                    IdleColour = Colour4.FromHex("#BDEFFF"),
                    HoverColour = Colour4.White,
                    Action = toggleEstimateCase,
                    Child = fullComboText = new TruncatingSpriteText
                    {
                        Font = OsuFont.Default.With(size: 12),
                        Colour = Colour4.FromHex("#BDEFFF"),
                        Shadow = false,
                    },
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

                // Both estimates are needed once the FC/SS estimate line can switch between the two cases.
                if (UseV2Style)
                {
                    if (FullComboScoreInfo.Create(score) is { } fullComboScore)
                        fullComboPP = (await calculator.CalculateAsync(fullComboScore, attributes.Value.DifficultyAttributes, token).ConfigureAwait(false)).Total;

                    if (PerfectScoreInfo.Create(score) is { } perfectScore)
                        perfectPP = (await calculator.CalculateAsync(perfectScore, attributes.Value.DifficultyAttributes, token).ConfigureAwait(false)).Total;
                }

                token.ThrowIfCancellationRequested();
                Schedule(() =>
                {
                    setPerformanceValue(pp);
                    if (!UseV2Style)
                        return;

                    if (attributes.Value.PerformanceAttributes?.Total is > 0 and var maxPP && double.IsFinite(maxPP))
                        maximumText.Text = $"/{Math.Round(maxPP, MidpointRounding.AwayFromZero):0}";

                    updateEstimateDisplay();
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

        /// <summary>
        /// Whether the score's mod setup would grant an "H" rank suffix (e.g. SSH) for a perfect play,
        /// mirroring how the rank is derived during gameplay.
        /// </summary>
        private static bool rankWouldCarryHidden(ScoreInfo scoreInfo)
        {
            var rank = ScoreRank.X;

            foreach (var mod in scoreInfo.Mods.OfType<IApplicableToScoreProcessor>())
                rank = mod.AdjustRank(rank, 1);

            return rank is ScoreRank.XH or ScoreRank.SH;
        }

        /// <summary>
        /// Whether the FC/SS estimate line can switch between the two cases. A play that is already a full
        /// combo has no meaningful FC case to switch away from, so only the perfect (SS) case is shown.
        /// </summary>
        private bool canSwitchEstimateCase
            => !score.IsPerfect() && fullComboPP is { } fc && perfectPP is { } perfect
               && double.IsFinite(fc) && double.IsFinite(perfect);

        private void toggleEstimateCase()
        {
            showPerfectEstimate = !showPerfectEstimate;
            updateEstimateText();
        }

        private void updateEstimateDisplay()
        {
            if (!canSwitchEstimateCase)
                showPerfectEstimate = true;

            fullComboArea.Enabled.Value = canSwitchEstimateCase;
            updateEstimateText();
        }

        private void updateEstimateText()
        {
            double? estimate = showPerfectEstimate ? perfectPP : fullComboPP;

            if (estimate is not { } pp || !double.IsFinite(pp))
            {
                fullComboText.Text = string.Empty;
                fullComboArea.TooltipText = string.Empty;
                return;
            }

            double rounded = Math.Round(pp, MidpointRounding.AwayFromZero);

            if (showPerfectEstimate)
            {
                // When the perfect case was switched to from FC, keep the switch hint visible.
                LocalisableString tooltip = canSwitchEstimateCase
                    ? LocalisableString.Format(@"{0} {1}", perfectTooltip(score), ResultsScreenStrings.ClickToFullComboTooltip)
                    : perfectTooltip(score);

                fullComboText.Text = rankWouldCarryHidden(score)
                    ? ResultsScreenStrings.IfPerfectHidden(rounded)
                    : ResultsScreenStrings.IfPerfect(rounded);
                fullComboArea.TooltipText = tooltip;
            }
            else
            {
                fullComboText.Text = ResultsScreenStrings.IfFullCombo(rounded);
                fullComboArea.TooltipText = ResultsScreenStrings.IfFullComboTooltip;
            }
        }

        private static LocalisableString perfectTooltip(ScoreInfo scoreInfo) => rankWouldCarryHidden(scoreInfo)
            ? ResultsScreenStrings.IfPerfectHiddenTooltip
            : ResultsScreenStrings.IfPerfectTooltip;

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
