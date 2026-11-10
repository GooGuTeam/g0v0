// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Skinning;

namespace osu.Game.Recording.Export
{
    /// <summary>
    /// Starts offline replay video exports, one at a time, and shows their progress as notifications.
    /// Everything the export needs from the game is captured here, on the update thread, before its worker starts.
    /// </summary>
    public partial class ReplayVideoExporter : Component
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private IRulesetConfigCache rulesetConfigCache { get; set; } = null!;

        [Resolved]
        private INotificationOverlay notifications { get; set; } = null!;

        private ReplayExportNotification? activeExport;

        /// <summary>
        /// Whether an export is running.
        /// </summary>
        public bool IsRunning => activeExport?.IsRunning == true;

        /// <summary>
        /// Exports a score's replay. The replay must be available locally.
        /// </summary>
        public void Export(IScoreInfo score) => start(() =>
        {
            var replay = scoreManager.GetScore(score);
            if (replay?.Replay == null)
                return null;

            return ReplayExportRequest.FromScore(beatmapManager.GetWorkingBeatmap(replay.ScoreInfo.BeatmapInfo), replay);
        });

        /// <summary>
        /// Exports the ruleset's autoplay on a beatmap with the given mods.
        /// </summary>
        public void ExportAutoplay(BeatmapInfo beatmap, RulesetInfo ruleset, IEnumerable<Mod> mods) => start(() =>
            ReplayExportRequest.FromAutoplay(beatmapManager.GetWorkingBeatmap(beatmap), ruleset, mods));

        /// <summary>
        /// Cancels the running export, if any.
        /// </summary>
        public void Cancel()
        {
            if (IsRunning)
                activeExport!.State = ProgressNotificationState.Cancelled;
        }

        private void start(Func<ReplayExportRequest?> createRequest)
        {
            if (IsRunning)
                return;

            try
            {
                var request = createRequest();

                if (request == null)
                {
                    notifications.Post(new SimpleNotification { Text = ResultsScreenStrings.ReplayUnavailable });
                    return;
                }

                var options = RecordingConfiguration.Snapshot(config);
                var backend = config.Get<RecordingBackend>(OsuSetting.RecordingBackend);
                string ffmpegPath = config.Get<string>(OsuSetting.RecordingFfmpegPath);
                var timeline = new ReplayExportTimeline(request.Duration, options.FrameRate);

                string directory = storage.GetFullPath(@"exports");
                Directory.CreateDirectory(directory);
                string destination = Path.Combine(directory, $@"replay-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.mp4");

                var skin = skinManager.CurrentSkinInfo.Value.PerformRead(s => s.Detach());
                var settings = config.GetCurrentConfigurationForLogging();
                var ruleset = request.Score.ScoreInfo.Ruleset.CreateInstance();
                var rulesetConfig = rulesetConfigCache.GetConfigFor(ruleset);
                if (rulesetConfig != null && rulesetConfig is not IRulesetConfigSnapshotSource)
                    throw new NotSupportedException(@"The selected ruleset cannot snapshot its configuration for export.");

                var rulesetSettings = (rulesetConfig as IRulesetConfigSnapshotSource)?.CreateSnapshot();
                var rulesetType = ruleset.GetType();
                var shaderCache = host.CacheStorage.GetStorageForDirectory(@"shaders");

                var notification = activeExport = new ReplayExportNotification(timeline, destination, host);
                notifications.Post(notification);

                _ = notification.RunAsync(token =>
                {
                    token.ThrowIfCancellationRequested();
                    return Task.FromResult<IReplayExportSession>(new OfflineReplayExportSession(
                        request, skin, skinManager, storage, settings, rulesetType, rulesetSettings, destination, options, backend, ffmpegPath, shaderCache, token));
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, @"Unable to prepare replay video export.");
                notifications.Post(new SimpleNotification { Text = VideoRecordingStrings.Failed(ex.Message) });
            }
        }
    }
}
