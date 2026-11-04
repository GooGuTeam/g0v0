// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Recording.Export
{
    /// <summary>
    /// Stands in for the game's <see cref="BeatmapDifficultyCache"/> in the export scene, serving the export's one beatmap, ruleset and mods
    /// from values computed synchronously before export, instead of wall-clock-dependent background calculation.
    /// </summary>
    /// <remarks>
    /// The PP counter and beatmap attribute displays use it unchanged. Bindable difficulties are filled by <see cref="Publish"/>, which the export
    /// calls after its first scene update, so that they change once as in the game (where calculation completes later) before any frame is drawn.
    /// It is never loaded into the scene; dispose it after the scene.
    /// </remarks>
    public partial class ReplayExportDifficultyCache : BeatmapDifficultyCache
    {
        private readonly List<TimedDifficultyAttributes> attributes;
        private readonly StarDifficulty difficulty;
        private readonly List<Bindable<StarDifficulty>> issued = new List<Bindable<StarDifficulty>>();
        private bool published;

        public ReplayExportDifficultyCache(WorkingBeatmap beatmap, Ruleset ruleset, Mod[] mods, ScoreProcessor scoreProcessor)
        {
            var calculator = ruleset.CreateDifficultyCalculator(beatmap);
            attributes = calculator.CalculateTimed(mods);
            var difficultyAttributes = calculator.Calculate(mods);
            var perfectScore = new ScoreInfo(beatmap.BeatmapInfo, ruleset.RulesetInfo)
            {
                Passed = true,
                Accuracy = 1,
                Mods = mods,
                MaxCombo = scoreProcessor.MaximumCombo,
                Combo = scoreProcessor.MaximumCombo,
                TotalScore = scoreProcessor.MaximumTotalScore,
                Statistics = scoreProcessor.MaximumStatistics,
                MaximumStatistics = scoreProcessor.MaximumStatistics,
            };
            var performance = ruleset.CreatePerformanceCalculator()?.Calculate(perfectScore, difficultyAttributes) ?? new PerformanceAttributes();
            difficulty = new StarDifficulty(difficultyAttributes, performance);
        }

        /// <summary>
        /// Gives every bindable difficulty handed out so far, and from now on, its value.
        /// </summary>
        public void Publish()
        {
            published = true;

            foreach (var bindable in issued)
                bindable.Value = difficulty;
        }

        public override IBindable<StarDifficulty> GetBindableDifficulty(IBeatmapInfo beatmapInfo, CancellationToken cancellationToken = default, int computationDelay = 0)
        {
            var bindable = new Bindable<StarDifficulty>();

            if (published)
                bindable.Value = difficulty;
            else
                issued.Add(bindable);

            return bindable;
        }

        public override Task<StarDifficulty?> GetDifficultyAsync(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo = null, IEnumerable<Mod>? mods = null,
                                                                 CancellationToken cancellationToken = default, int computationDelay = 0)
            => Task.FromResult<StarDifficulty?>(difficulty);

        public override Task<List<TimedDifficultyAttributes>> GetTimedDifficultyAttributesAsync(IWorkingBeatmap beatmap, Ruleset ruleset, Mod[] mods, CancellationToken cancellationToken = default)
            => Task.FromResult(attributes);
    }
}
