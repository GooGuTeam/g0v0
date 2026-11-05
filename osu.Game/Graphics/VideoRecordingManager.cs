// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Veldrid;
using osu.Framework.Graphics.Video;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Graphics
{
    public partial class VideoRecordingManager : Component
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
        private volatile DesktopVideoRecording? recording;
        private volatile RtmpVideoPublisher? publisher;
        private volatile bool busy;
        private volatile bool startingStream;
        private CancellationTokenSource? startup;

        public bool IsRecording => recording != null || busy;
        public bool IsStreaming => publisher != null || startingStream;

        public void ToggleStreaming()
        {
            if (IsStreaming)
                _ = StopAsync();
            else if (IsRecording)
                notify(VideoRecordingStrings.CaptureBusy);
            else
                dialogs.Push(new LiveStreamingDialog(address => { _ = startStreamingAsync(address); }));
        }

        public async Task ToggleAsync()
        {
            if (IsRecording)
            {
                await StopAsync().ConfigureAwait(false);
                return;
            }
            VideoRecordingOptions options;
            try { options = RecordingConfiguration.Snapshot(config); }
            catch (Exception ex)
            {
                notify(VideoRecordingStrings.Failed(ex.Message));
                return;
            }
            await operations.WaitAsync().ConfigureAwait(false);
            try
            {
                if (recording != null) return;
                busy = true;
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                startup = timeout;
                string directory = storage.GetFullPath(@"recordings");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, $@"recording-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.mp4");
                recording = await DesktopVideoRecording.StartAsync(host, audio, path, cancellationToken: timeout.Token, options: options).ConfigureAwait(false);
                _ = observeAsync(recording, null, path);
                await recording.Started.WaitAsync(timeout.Token).ConfigureAwait(false);
                notify(VideoRecordingStrings.Started);
            }
            catch (Exception ex)
            {
                await stopInternalAsync().ConfigureAwait(false);
                notify(VideoRecordingStrings.Failed(ex.Message));
            }
            finally
            {
                startup = null;
                busy = false;
                operations.Release();
            }
        }

        private async Task startStreamingAsync(string destination)
        {
            VideoRecordingOptions options;
            try { options = RecordingConfiguration.Snapshot(config); }
            catch (Exception ex)
            {
                notify(VideoRecordingStrings.Failed(ex.Message));
                return;
            }
            await operations.WaitAsync().ConfigureAwait(false);
            try
            {
                if (recording != null)
                {
                    notify(VideoRecordingStrings.CaptureBusy);
                    return;
                }
                busy = startingStream = true;
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                startup = timeout;
                publisher = new RtmpVideoPublisher(destination, () => recording?.RequestKeyFrame());
                recording = await DesktopVideoRecording.StartStreamingAsync(host, audio, publisher.Enqueue, timeout.Token, options).ConfigureAwait(false);
                _ = observeAsync(recording, publisher, null);
                await publisher.Started.WaitAsync(timeout.Token).ConfigureAwait(false);
                notify(VideoRecordingStrings.StreamStarted);
            }
            catch (Exception)
            {
                await stopInternalAsync().ConfigureAwait(false);
                notify(VideoRecordingStrings.StreamFailed);
            }
            finally
            {
                startup = null;
                busy = startingStream = false;
                operations.Release();
            }
        }

        public async Task StopAsync()
        {
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
            var current = recording;
            var output = publisher;
            recording = null;
            publisher = null;
            try
            {
                if (current != null)
                    await current.StopAsync().ConfigureAwait(false);
            }
            catch (Exception) { }
            finally
            {
                if (output != null)
                {
                    try { await output.DisposeAsync().ConfigureAwait(false); }
                    catch (Exception) { }
                }
            }
        }

        private async Task observeAsync(DesktopVideoRecording current, RtmpVideoPublisher? output, string? path)
        {
            try
            {
                if (output == null)
                {
                    await current.Completion.ConfigureAwait(false);
                    notify(VideoRecordingStrings.Saved(path!));
                }
                else
                {
                    Task first = await Task.WhenAny(current.Completion, output.Completion).ConfigureAwait(false);
                    await first.ConfigureAwait(false);
                    notify(VideoRecordingStrings.StreamStopped);
                }
            }
            catch (Exception ex)
            {
                notify(output == null ? VideoRecordingStrings.Failed(ex.Message) : VideoRecordingStrings.StreamFailed);
            }
            finally
            {
                await operations.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (ReferenceEquals(recording, current))
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

        private void notify(LocalisableString text) => Schedule(() => notifications.Post(new SimpleNotification { Text = text }));
    }
}
