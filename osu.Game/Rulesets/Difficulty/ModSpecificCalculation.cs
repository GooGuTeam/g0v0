// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.Difficulty
{
    /// <summary>
    /// Selects between the upstream and the mod-specific difficulty and performance algorithms of a <see cref="Ruleset"/>.
    /// </summary>
    /// <remarks>
    /// Rulesets opt in to mod-specific algorithms by overriding
    /// <see cref="Ruleset.CreateModSpecificDifficultyCalculator"/> and <see cref="Ruleset.CreateModSpecificPerformanceCalculator"/>.
    /// </remarks>
    public static class ModSpecificCalculation
    {
        /// <summary>
        /// Whether mod-specific algorithms (for example the Relax handling of the osu! ruleset) should be used.
        /// Updated by <see cref="OsuGameBase"/> from the corresponding setting.
        /// Scores which were already submitted are not recalculated.
        /// </summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// Creates a <see cref="DifficultyCalculator"/>, preferring the mod-specific implementation when <see cref="Enabled"/> is set.
        /// </summary>
        public static DifficultyCalculator CreateDifficultyCalculator(Ruleset ruleset, IWorkingBeatmap beatmap)
            => CreateDifficultyCalculator(ruleset, beatmap, Enabled);

        /// <summary>
        /// Creates a <see cref="DifficultyCalculator"/>, preferring the mod-specific implementation when <paramref name="useModSpecific"/> is set.
        /// </summary>
        public static DifficultyCalculator CreateDifficultyCalculator(Ruleset ruleset, IWorkingBeatmap beatmap, bool useModSpecific)
            => useModSpecific ? ruleset.CreateModSpecificDifficultyCalculator(beatmap) : ruleset.CreateDifficultyCalculator(beatmap);

        /// <summary>
        /// Creates a <see cref="PerformanceCalculator"/>, preferring the mod-specific implementation when <see cref="Enabled"/> is set.
        /// </summary>
        public static PerformanceCalculator? CreatePerformanceCalculator(Ruleset ruleset)
            => Enabled ? ruleset.CreateModSpecificPerformanceCalculator() : ruleset.CreatePerformanceCalculator();
    }
}
