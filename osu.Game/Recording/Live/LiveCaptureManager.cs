// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Video;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Recording.Live
{
    /// <summary>
    /// Records or streams the game window and audio, one capture at a time, with the encoder selected in the recording settings
    /// (<see cref="MediaFoundationLiveCapture"/> on Windows by default, <see cref="FfmpegLiveCapture"/> elsewhere).
    /// </summary>
    public partial class LiveCaptureManager : Component
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private INotificationOverlay notifications { get; set; } = null!;

        [Resolved]
        private IDialogOverlay dialogs { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private readonly SemaphoreSlim operations = new SemaphoreSlim(1);
        private volatile ILiveCapture? capture;
        private volatile bool busy;
        private volatile bool streaming;
        private volatile bool startupCancelled;
        private volatile bool isDisposed;
        private CancellationTokenSource? startup;

        public bool IsCapturing => capture != null || busy;

        public bool IsRecording => IsCapturing && !streaming;

        public bool IsStreaming => IsCapturing && streaming;

        public void ToggleStreaming()
        {
            if (IsStreaming)
                _ = StopAsync();
            else if (IsCapturing)
                notify(VideoRecordingStrings.CaptureBusy);
            else
            {
                bool viaFfmpeg;

                try { viaFfmpeg = config.Get<RecordingBackend>(OsuSetting.RecordingBackend).Resolve() == RecordingBackend.Ffmpeg; }
                catch (Exception ex)
                {
                    notify(VideoRecordingStrings.Failed(ex.Message));
                    return;
                }

                dialogs.Push(new LiveStreamingDialog(address => { _ = startAsync(address); }, viaFfmpeg));
            }
        }

        public Task ToggleAsync()
        {
            if (IsRecording)
                return StopAsync();

            if (IsCapturing)
            {
                notify(VideoRecordingStrings.CaptureBusy);
                return Task.CompletedTask;
            }

            return startAsync(null);
        }

        /// <param name="destination">The publishing address, or <c>null</c> to record into a file.</param>
        private async Task startAsync(string? destination)
        {
            if (isDisposed)
                return;

            bool stream = destination != null;
            VideoRecordingOptions options;
            RecordingBackend backend;
            string ffmpegPath;

            try
            {
                options = RecordingConfiguration.Snapshot(config);
                backend = config.Get<RecordingBackend>(OsuSetting.RecordingBackend).Resolve();
                ffmpegPath = config.Get<string>(OsuSetting.RecordingFfmpegPath);
            }
            catch (Exception ex)
            {
                notify(VideoRecordingStrings.Failed(ex.Message));
                return;
            }

            await operations.WaitAsync().ConfigureAwait(false);

            string? path = null;

            try
            {
                if (isDisposed)
                    return;

                if (capture != null)
                {
                    notify(VideoRecordingStrings.CaptureBusy);
                    return;
                }

                busy = true;
                streaming = stream;
                startupCancelled = false;

                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(stream ? 30 : 20));
                startup = timeout;

                if (!stream)
                {
                    string directory = storage.GetFullPath(@"recordings");
                    Directory.CreateDirectory(directory);
                    path = Path.Combine(directory, $@"recording-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.mp4");
                }

                var started = backend == RecordingBackend.MediaFoundation
                    ? stream
                        ? await MediaFoundationLiveCapture.StreamAsync(host, audio, destination!, options, timeout.Token).ConfigureAwait(false)
                        : await MediaFoundationLiveCapture.RecordAsync(host, audio, path!, options, timeout.Token).ConfigureAwait(false)
                    : stream
                        ? await FfmpegLiveCapture.StreamAsync(host, audio, destination!, options, ffmpegPath, timeout.Token).ConfigureAwait(false)
                        : await FfmpegLiveCapture.RecordAsync(host, audio, path!, options, ffmpegPath, timeout.Token).ConfigureAwait(false);

                capture = started;

                await started.Started.WaitAsync(timeout.Token).ConfigureAwait(false);
                _ = observeAsync(started, path);
                notify(stream ? VideoRecordingStrings.StreamStarted : VideoRecordingStrings.Started);
            }
            catch (OperationCanceledException) when (startupCancelled)
            {
                await stopInternalAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Publishing addresses carry stream keys, so stream failures are reported without details.
                Logger.Log($@"Unable to start {(stream ? @"streaming" : @"recording")}: {(stream ? ex.GetType().Name : ex.Message)}", level: LogLevel.Important);
                await stopInternalAsync().ConfigureAwait(false);
                notify(stream ? VideoRecordingStrings.StreamFailed : VideoRecordingStrings.Failed(ex.Message));
            }
            finally
            {
                startup = null;
                busy = false;
                operations.Release();
            }
        }

        public async Task StopAsync()
        {
            startupCancelled = true;

            try { startup?.Cancel(); }
            catch (ObjectDisposedException) { }

            await operations.WaitAsync().ConfigureAwait(false);

            try
            {
                busy = true;
                await stopInternalAsync().ConfigureAwait(false);
            }
            finally
            {
                busy = false;
                operations.Release();
            }
        }

        private async Task stopInternalAsync()
        {
            var current = capture;
            capture = null;
            streaming = false;

            try
            {
                if (current != null)
                    await current.StopAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Reported by observeAsync.
            }
        }

        private async Task observeAsync(ILiveCapture current, string? path)
        {
            bool stream = path == null;

            try
            {
                await current.Completion.ConfigureAwait(false);
                notify(stream ? VideoRecordingStrings.StreamStopped : VideoRecordingStrings.Saved(path!));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                notify(stream ? VideoRecordingStrings.StreamFailed : VideoRecordingStrings.Failed(ex.Message));
            }
            finally
            {
                await operations.WaitAsync().ConfigureAwait(false);

                try
                {
                    if (ReferenceEquals(capture, current))
                    {
                        busy = true;
                        await stopInternalAsync().ConfigureAwait(false);
                    }
                }
                finally
                {
                    busy = false;
                    operations.Release();
                }
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposed)
                return;

            isDisposed = true;
            startupCancelled = true;

            try { startup?.Cancel(); }
            catch (ObjectDisposedException) { }

            _ = StopAsync();
        }

        private void notify(LocalisableString text)
        {
            if (!isDisposed)
                Schedule(() => notifications.Post(new SimpleNotification { Text = text }));
        }
    }
}
