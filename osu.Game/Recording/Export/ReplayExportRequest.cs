// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Scoring;

namespace osu.Game.Recording.Export
{
    /// <summary>
    /// A replay to export and the beatmap it plays, validated and with its export time range worked out.
    /// </summary>
    public sealed class ReplayExportRequest
    {
        /// <summary>
        /// Lead-in and lead-out around the beatmap's objects and the replay's frames, in beatmap time.
        /// </summary>
        private const double padding = 2000;

        public WorkingBeatmap Beatmap { get; }

        public Score Score { get; }

        /// <summary>
        /// The beatmap time the export starts at.
        /// </summary>
        public double StartTime { get; }

        /// <summary>
        /// The beatmap time the export ends at.
        /// </summary>
        public double EndTime { get; }

        /// <summary>
        /// The constant playback rate of the replay's mods.
        /// </summary>
        public double Rate { get; }

        /// <summary>
        /// The output video's length.
        /// </summary>
        public TimeSpan Duration => TimeSpan.FromMilliseconds((EndTime - StartTime) / Rate);

        private ReplayExportRequest(WorkingBeatmap beatmap, Score score)
        {
            if (score.Replay == null)
                throw new ArgumentException(@"The score has no replay.", nameof(score));
            if (beatmap is DummyWorkingBeatmap || beatmap.Beatmap.HitObjects.Count == 0)
                throw new InvalidOperationException(@"The replay's beatmap is unavailable.");
            if (score.ScoreInfo.Mods.Any(m => m is IApplicableToRate && m is not ModRateAdjust))
                throw new NotSupportedException(@"Variable-rate mods are not supported by offline export yet.");

            Beatmap = beatmap;
            Score = score;
            Rate = score.ScoreInfo.Mods.OfType<IApplicableToRate>().Aggregate(1.0, (value, mod) => mod.ApplyToRate(0, value));
            StartTime = Math.Min(0, beatmap.Beatmap.HitObjects[0].StartTime - padding);
            EndTime = Math.Max(beatmap.Beatmap.GetLastObjectTime(), score.Replay.Frames.Count == 0 ? 0 : score.Replay.Frames.Max(f => f.Time)) + padding;
        }

        private ReplayExportRequest(ReplayExportRequest other, double startTime)
        {
            Beatmap = other.Beatmap;
            Score = other.Score;
            Rate = other.Rate;
            StartTime = startTime;
            EndTime = other.EndTime;
        }

        /// <summary>
        /// The same export starting later or earlier, so that tests and benchmarks can render a part of the replay.
        /// </summary>
        internal ReplayExportRequest StartingAt(double startTime) => new ReplayExportRequest(this, startTime);

        /// <summary>
        /// Exports a score's recorded replay.
        /// </summary>
        public static ReplayExportRequest FromScore(WorkingBeatmap beatmap, Score score) => new ReplayExportRequest(beatmap, score);

        /// <summary>
        /// Exports what the ruleset's autoplay does on a beatmap with the given mods, as in the game's autoplay
        /// (<see cref="ModExtensions.CreateScoreFromReplayData"/> on the playable beatmap, with the autoplay mod among the score's mods).
        /// </summary>
        /// <param name="beatmap">The beatmap to play.</param>
        /// <param name="ruleset">The ruleset to play it in.</param>
        /// <param name="mods">The mods to play with. The ruleset's autoplay mod is added when no mod creates replay data, replacing incompatible mods.</param>
        public static ReplayExportRequest FromAutoplay(WorkingBeatmap beatmap, RulesetInfo ruleset, IEnumerable<Mod> mods)
        {
            var instance = ruleset.CreateInstance();
            var exportMods = mods.Select(m => m.DeepClone()).ToList();

            if (!exportMods.OfType<ICreateReplayData>().Any())
            {
                var autoplay = instance.GetAutoplayMod() ?? throw new NotSupportedException(@"The ruleset has no autoplay mod.");
                exportMods = exportMods.Where(m => !m.IncompatibleMods.Any(t => t.IsInstanceOfType(autoplay)) && !autoplay.IncompatibleMods.Any(t => t.IsInstanceOfType(m)))
                                       .Append(autoplay).ToList();
            }

            var replayMod = exportMods.OfType<ICreateReplayData>().First();
            var playable = beatmap.GetPlayableBeatmap(ruleset, exportMods);
            var score = replayMod.CreateScoreFromReplayData(playable, exportMods);

            score.ScoreInfo.BeatmapInfo = beatmap.BeatmapInfo;
            score.ScoreInfo.Ruleset = ruleset;
            score.ScoreInfo.Mods = exportMods.ToArray();

            return new ReplayExportRequest(beatmap, score);
        }
    }
}
