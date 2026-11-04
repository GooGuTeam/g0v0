// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Audio;
using osu.Framework.Graphics.Veldrid;
using osu.Framework.Graphics.Video;
using osu.Framework.Platform;

namespace osu.Game.Recording.Live
{
    /// <summary>
    /// Windows capture through the framework's GPU capture and Media Foundation hardware H.264 encoder (<see cref="DesktopVideoRecording"/>),
    /// published with <see cref="RtmpVideoPublisher"/> when streaming.
    /// </summary>
    public sealed class MediaFoundationLiveCapture : ILiveCapture
    {
        private readonly DesktopVideoRecording recording;
        private readonly RtmpVideoPublisher? publisher;

        public Task Started { get; }

        public Task Completion { get; }

        private MediaFoundationLiveCapture(DesktopVideoRecording recording, RtmpVideoPublisher? publisher)
        {
            this.recording = recording;
            this.publisher = publisher;

            Started = publisher == null ? recording.Started : Task.WhenAll(recording.Started, publisher.Started);
            Completion = publisher == null ? recording.Completion : completeAsync(recording, publisher);
        }

        public static async Task<ILiveCapture> RecordAsync(GameHost host, AudioManager audio, string path, VideoRecordingOptions options, CancellationToken cancellationToken)
        {
            var recording = await DesktopVideoRecording.StartAsync(host, audio, path, cancellationToken, options).ConfigureAwait(false);
            return new MediaFoundationLiveCapture(recording, null);
        }

        public static async Task<ILiveCapture> StreamAsync(GameHost host, AudioManager audio, string destination, VideoRecordingOptions options, CancellationToken cancellationToken)
        {
            DesktopVideoRecording? recording = null;
            var publisher = new RtmpVideoPublisher(destination, () => recording?.RequestKeyFrame());

            try
            {
                recording = await DesktopVideoRecording.StartStreamingAsync(host, audio, publisher.Enqueue, cancellationToken, options).ConfigureAwait(false);
                return new MediaFoundationLiveCapture(recording, publisher);
            }
            catch
            {
                await publisher.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        public async Task StopAsync()
        {
            try
            {
                await recording.StopAsync().ConfigureAwait(false);
            }
            finally
            {
                if (publisher != null)
                    await publisher.DisposeAsync().ConfigureAwait(false);
            }
        }

        private static async Task completeAsync(DesktopVideoRecording recording, RtmpVideoPublisher publisher)
        {
            // Either side ending ends the stream; a failure of the first to end is the stream's failure.
            var first = await Task.WhenAny(recording.Completion, publisher.Completion).ConfigureAwait(false);
            await first.ConfigureAwait(false);
        }

        public override string ToString() => $@"{nameof(MediaFoundationLiveCapture)} (encoded {recording.EncodedFrames}, dropped {recording.CapturePoolDrops + recording.WorkerQueueDrops})";
    }
}
