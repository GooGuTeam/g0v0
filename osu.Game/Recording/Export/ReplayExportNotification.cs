// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Localisation;
using osu.Game.Screens.Play;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Recording.Export
{
    /// <summary>
    /// Keeps export progress on the client update thread, outside the private rendering scene.
    /// </summary>
    public partial class ReplayExportNotification : ProgressNotification
    {
        private readonly ReplayExportTimeline timeline;
        private readonly Stopwatch elapsed = new Stopwatch();
        private bool started;
        private volatile bool running;
        public bool IsRunning => running;
        private long lastProgressMilliseconds;

        public ReplayExportNotification(ReplayExportTimeline timeline, string destination, GameHost host)
        {
            this.timeline = timeline;
            Text = VideoRecordingStrings.ExportPreparing;
            CompletionText = VideoRecordingStrings.Saved(destination);
            CompletionClickAction = () => host.PresentFileExternally(destination);
        }

        public async Task RunAsync(Func<CancellationToken, Task<IReplayExportSession>> createSession)
        {
            if (started)
                throw new InvalidOperationException(@"Export notification has already been started.");
            started = running = true;
            State = ProgressNotificationState.Active;
            elapsed.Start();
            try
            {
                await new ReplayExportJob(timeline).RunAsync(createSession, new ExportProgress(this), CancellationToken).ConfigureAwait(false);
                Schedule(() =>
                {
                    Progress = 1;
                    State = ProgressNotificationState.Completed;
                });
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
                Schedule(() =>
                {
                    State = ProgressNotificationState.Cancelled;
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"[ReplayExport] Replay video export failed: {ex.Message}");
                Schedule(() =>
                {
                    Text = VideoRecordingStrings.Failed(ex.Message);
                    State = ProgressNotificationState.Cancelled;
                });
            }
            finally
            {
                elapsed.Stop();
                running = false;
            }
        }

        private void report(double progress)
        {
            long now = elapsed.ElapsedMilliseconds;
            if (progress < 1 && now - lastProgressMilliseconds < 100)
                return;
            lastProgressMilliseconds = now;
            long frame = timeline.FrameIndex;
            double speed = elapsed.Elapsed.TotalSeconds > 0 ? timeline.TimeMilliseconds / 1000 / elapsed.Elapsed.TotalSeconds : 0;
            Schedule(() =>
            {
                if (!Ongoing)
                    return;

                Progress = (float)progress;
                Text = progress >= 0.99
                    ? VideoRecordingStrings.ExportFinalising
                    : VideoRecordingStrings.ExportProgress(frame, timeline.TotalFrames, speed);
            });
        }

        private sealed class ExportProgress : IProgress<double>
        {
            private readonly ReplayExportNotification owner;

            public ExportProgress(ReplayExportNotification owner) => this.owner = owner;

            public void Report(double value) => owner.report(value);
        }
    }
}
