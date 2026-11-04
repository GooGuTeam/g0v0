// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Audio;
using osu.Framework.Audio.Callbacks;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Veldrid;
using osu.Framework.Graphics.Video;
using osu.Framework.Logging;
using osu.Framework.Platform;
using Veldrid;

namespace osu.Game.Recording.Live
{
    /// <summary>
    /// Cross-platform capture of the game window and audio, encoded by an installed FFmpeg with libx264 and AAC.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Frames come from the framework's GPU capture (<see cref="IGpuFrameCaptureRenderer"/>) on the draw thread. Each is copied into one of
    /// <see cref="readback_depth"/> staging textures and mapped once its fence has signalled, a frame or so later, so the draw thread never waits for the GPU.
    /// A pacing thread writes the latest frame to FFmpeg's standard input at the configured rate, repeating it when the game draws slower,
    /// so the video stays on the wall clock that the audio follows.
    /// </para>
    /// <para>
    /// Game audio (<see cref="AudioManager.BeginCapture"/>) reaches FFmpeg through a loopback TCP connection, its second input, so the two inputs
    /// cannot block each other the way two pipes from one process can. Both writers drop data rather than stall the game when FFmpeg falls behind.
    /// FFmpeg scales and letterboxes to the output size, and writes fragmented MP4 (playable if interrupted) or publishes FLV over RTMP/RTMPS.
    /// </para>
    /// </remarks>
    public sealed class FfmpegLiveCapture : ILiveCapture
    {
        private const int readback_depth = 3;
        private const int capture_capacity = 4;
        private const int stderr_tail_length = 2048;

        /// <summary>
        /// How far audio lags behind real time before it is sent, so that late audio callbacks still arrive in order.
        /// </summary>
        private const int audio_latency_samples = MixerAudioCapture.SAMPLE_RATE / 10;

        /// <summary>
        /// How far the video writer may fall behind before it skips frames instead of catching up with repeats.
        /// </summary>
        private static readonly TimeSpan max_video_lag = TimeSpan.FromSeconds(1);

        private static readonly TimeSpan startup_timeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan exit_timeout = TimeSpan.FromSeconds(30);

        private readonly GameHost host;
        private readonly AudioManager audio;
        private readonly VideoRecordingOptions options;
        private readonly string executable;
        private readonly string output;
        private readonly bool streaming;

        private readonly TaskCompletionSource started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource drawThreadReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource stopping = new CancellationTokenSource();
        private readonly long startTimestamp = Stopwatch.GetTimestamp();

        // Draw thread only.
        private GpuFrameCaptureSession? session;
        private readonly Texture?[] staging = new Texture?[readback_depth];
        private readonly CommandList?[] commands = new CommandList?[readback_depth];
        private readonly Fence?[] fences = new Fence?[readback_depth];
        private readonly Queue<int> pendingReadbacks = new Queue<int>();
        private uint width;
        private uint height;
        private PixelFormat format;

        // Shared between the draw thread (producer) and the video writer (consumer).
        private readonly object frameLock = new object();
        private byte[]? latestFrame;
        private byte[]? spareFrame;
        private bool hasNewFrame;

        private Process? encoder;
        private TcpListener? audioListener;
        private Task<string>? stderrTail;
        private Exception? failure;
        private int stopRequested;
        private int finishing;

        public Task Started => started.Task;

        public Task Completion => completion.Task;

        /// <summary>
        /// Frames lost because the capture pool, the staging textures or the encoder were busy.
        /// </summary>
        public long DroppedFrames { get; private set; }

        /// <summary>
        /// Frames written again because the game drew fewer frames than the output rate.
        /// </summary>
        public long RepeatedFrames { get; private set; }

        private FfmpegLiveCapture(GameHost host, AudioManager audio, VideoRecordingOptions options, string executable, string output, bool streaming)
        {
            this.host = host;
            this.audio = audio;
            this.options = options;
            this.executable = executable;
            this.output = output;
            this.streaming = streaming;
        }

        /// <summary>
        /// Records into a new MP4 file.
        /// </summary>
        public static Task<ILiveCapture> RecordAsync(GameHost host, AudioManager audio, string path, VideoRecordingOptions options, string executable, CancellationToken cancellationToken)
        {
            if (File.Exists(path))
                throw new IOException(@"The recording destination already exists.");

            return startAsync(new FfmpegLiveCapture(host, audio, options, executable, Path.GetFullPath(path), false), cancellationToken);
        }

        /// <summary>
        /// Publishes to an RTMP or RTMPS address. FFmpeg receives the address as an argument.
        /// </summary>
        public static Task<ILiveCapture> StreamAsync(GameHost host, AudioManager audio, string destination, VideoRecordingOptions options, string executable, CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(destination, UriKind.Absolute, out var uri) || (uri.Scheme != @"rtmp" && uri.Scheme != @"rtmps"))
                throw new ArgumentException(@"Streaming requires an RTMP or RTMPS address.", nameof(destination));

            return startAsync(new FfmpegLiveCapture(host, audio, options, executable, destination, true), cancellationToken);
        }

        private static async Task<ILiveCapture> startAsync(FfmpegLiveCapture capture, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(capture.executable);

            if (capture.host.Renderer is not IGpuFrameCaptureRenderer)
                throw new NotSupportedException(@"Recording requires the Direct3D 11 or Vulkan renderer.");

            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = cancellationToken.Register(() => ready.TrySetCanceled(cancellationToken));

            capture.host.DrawThread.Scheduler.Add(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    capture.session = ((IGpuFrameCaptureRenderer)capture.host.Renderer).StartGpuFrameCapture(capture_capacity);
                    capture.session.MinimumFrameInterval = Stopwatch.Frequency / capture.options.FrameRate;
                    capture.session.FrameAvailable += capture.pump;

                    if (!ready.TrySetResult())
                        capture.releaseDrawThreadResources();
                }
                catch (Exception e)
                {
                    capture.releaseDrawThreadResources();
                    ready.TrySetException(e);
                }
            });

            await ready.Task.ConfigureAwait(false);

            // The encoder opens once the first frame gives the source size; until then, give up after a while rather than wait forever.
            _ = capture.Started.WaitAsync(startup_timeout).ContinueWith(t =>
            {
                if (t.IsFaulted && !capture.Started.IsCompleted)
                    capture.fail(new TimeoutException(@"No frames were captured. The game may not be drawing."));
            }, TaskScheduler.Default);

            return capture;
        }

        public Task StopAsync()
        {
            requestStop();
            return Completion;
        }

        private void requestStop()
        {
            if (Interlocked.Exchange(ref stopRequested, 1) != 0)
                return;

            stopping.Cancel();

            // The capture session and staging textures belong to the draw thread.
            host.DrawThread.Scheduler.Add(releaseDrawThreadResources);

            // Without an encoder nothing else finishes the capture.
            if (encoder == null)
                _ = finishAsync();
        }

        private void fail(Exception e)
        {
            Interlocked.CompareExchange(ref failure, e, null);
            requestStop();
        }

        #region Draw thread

        private void pump()
        {
            var current = session;
            if (current == null || current.IsDisposed)
                return;

            try
            {
                if (stopping.IsCancellationRequested)
                {
                    releaseDrawThreadResources();
                    return;
                }

                completeReadbacks();

                if (!current.TryAcquireFrame(out var frame) || frame == null)
                    return;

                using (frame)
                {
                    if (encoder == null)
                        open(current.Device, frame);
                    else if (frame.Texture.Width != width || frame.Texture.Height != height)
                        throw new InvalidOperationException(@"Recording stopped because the window size changed.");

                    int slot = -1;

                    for (int i = 0; i < readback_depth && slot < 0; i++)
                    {
                        if (!pendingReadbacks.Contains(i))
                            slot = i;
                    }

                    if (slot < 0)
                    {
                        DroppedFrames++;
                        return;
                    }

                    // The copy is queued before any later capture into the same pool slot, so the lease can be returned right after submitting it.
                    var list = commands[slot]!;
                    list.Begin();
                    list.CopyTexture(frame.Texture, staging[slot]!);
                    list.End();
                    current.Device.SubmitCommands(list, fences[slot]);
                    pendingReadbacks.Enqueue(slot);
                }
            }
            catch (Exception e)
            {
                releaseDrawThreadResources();
                fail(e);
            }
        }

        private void completeReadbacks()
        {
            var device = session!.Device;

            while (pendingReadbacks.TryPeek(out int slot) && fences[slot]!.Signaled)
            {
                pendingReadbacks.Dequeue();
                fences[slot]!.Reset();

                byte[] target;

                lock (frameLock)
                    target = spareFrame ?? new byte[checked((int)(width * height * 4))];

                var mapped = device.Map(staging[slot], MapMode.Read);

                try
                {
                    int row = (int)width * 4;

                    for (int y = 0; y < height; y++)
                        Marshal.Copy(mapped.Data + (nint)(y * mapped.RowPitch), target, y * row, row);
                }
                finally
                {
                    device.Unmap(staging[slot]);
                }

                lock (frameLock)
                {
                    if (hasNewFrame)
                        DroppedFrames++;

                    spareFrame = latestFrame;
                    latestFrame = target;
                    hasNewFrame = true;
                }
            }
        }

        private void open(GraphicsDevice device, GpuCapturedFrame frame)
        {
            width = frame.Texture.Width;
            height = frame.Texture.Height;
            format = frame.Texture.Format;

            string pixelFormat = format switch
            {
                PixelFormat.B8G8R8A8UNorm or PixelFormat.B8G8R8A8UNormSRgb => @"bgra",
                PixelFormat.R8G8B8A8UNorm or PixelFormat.R8G8B8A8UNormSRgb => @"rgba",
                _ => throw new NotSupportedException($@"Recording does not support the {format} swapchain format."),
            };

            for (int i = 0; i < readback_depth; i++)
            {
                staging[i] = device.ResourceFactory.CreateTexture(TextureDescription.Texture2D(width, height, 1, 1, format, TextureUsage.Staging));
                commands[i] = device.ResourceFactory.CreateCommandList();
                fences[i] = device.ResourceFactory.CreateFence(false);
            }

            startEncoder(pixelFormat, !frame.IsUvOriginTopLeft);
        }

        private void releaseDrawThreadResources()
        {
            if (session != null)
            {
                session.FrameAvailable -= pump;
                session.Dispose();
                session = null;
            }

            pendingReadbacks.Clear();

            for (int i = 0; i < readback_depth; i++)
            {
                staging[i]?.Dispose();
                commands[i]?.Dispose();
                fences[i]?.Dispose();
                staging[i] = null;
                commands[i] = null;
                fences[i] = null;
            }

            drawThreadReleased.TrySetResult();
        }

        #endregion

        #region Encoder

        private void startEncoder(string pixelFormat, bool flip)
        {
            audioListener = new TcpListener(IPAddress.Loopback, 0);
            audioListener.Start(1);
            int audioPort = ((IPEndPoint)audioListener.LocalEndpoint).Port;

            var info = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
            };

            string filter = $@"scale={options.Width}:{options.Height}:force_original_aspect_ratio=decrease,pad={options.Width}:{options.Height}:(ow-iw)/2:(oh-ih)/2,format=yuv420p";
            if (flip)
                filter = @"vflip," + filter;

            add(info, @"-nostdin", @"-hide_banner", @"-loglevel", @"error", @"-n",
                @"-thread_queue_size", @"64", @"-f", @"rawvideo", @"-pixel_format", pixelFormat, @"-video_size", $@"{width}x{height}",
                @"-framerate", number(options.FrameRate), @"-i", @"pipe:0",
                @"-thread_queue_size", @"1024", @"-f", @"f32le", @"-ar", number(MixerAudioCapture.SAMPLE_RATE), @"-ac", number(MixerAudioCapture.CHANNELS),
                @"-i", $@"tcp://127.0.0.1:{audioPort}",
                @"-map", @"0:v:0", @"-map", @"1:a:0", @"-vf", filter,
                @"-c:v", @"libx264", @"-preset", @"veryfast", @"-tune", @"zerolatency",
                @"-b:v", number(options.Bitrate), @"-maxrate", number(options.Bitrate), @"-bufsize", number(options.Bitrate * 2), @"-g", number(options.FrameRate * 2));

            if (options.H264Profile != 0)
                add(info, @"-profile:v", options.H264Profile == 66 ? @"baseline" : options.H264Profile == 77 ? @"main" : @"high");

            add(info, @"-c:a", @"aac", @"-b:a", @"160k");

            if (streaming)
                add(info, @"-f", @"flv", output);
            else
                add(info, @"-movflags", @"+frag_keyframe+empty_moov+default_base_moof", @"-f", @"mp4", output);

            encoder = Process.Start(info) ?? throw new InvalidOperationException(@"Unable to start FFmpeg.");
            stderrTail = readTailAsync(encoder.StandardError);

            var pipe = encoder.StandardInput.BaseStream;
            new Thread(() => writeVideo(pipe)) { IsBackground = true, Name = @"FFmpeg live video" }.Start();
            new Thread(writeAudio) { IsBackground = true, Name = @"FFmpeg live audio" }.Start();

            _ = finishAsync();
        }

        private void writeVideo(Stream pipe)
        {
            try
            {
                long written = 0;
                long ticksPerFrame = Stopwatch.Frequency / options.FrameRate;
                long maxLagFrames = (long)(max_video_lag.TotalSeconds * options.FrameRate);
                byte[]? frame = null;

                while (!stopping.IsCancellationRequested)
                {
                    long due = (Stopwatch.GetTimestamp() - startTimestamp) / ticksPerFrame;

                    if (written > due)
                    {
                        long wait = (written * ticksPerFrame - (Stopwatch.GetTimestamp() - startTimestamp)) * 1000 / Stopwatch.Frequency;
                        stopping.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(Math.Clamp(wait, 1, 100)));
                        continue;
                    }

                    if (due - written > maxLagFrames)
                    {
                        DroppedFrames += due - written - 1;
                        written = due;
                    }

                    lock (frameLock)
                    {
                        if (hasNewFrame)
                        {
                            // Hand the previous frame back for reuse and take the newest.
                            if (frame != null)
                                spareFrame = frame;

                            frame = latestFrame;
                            latestFrame = null;
                            hasNewFrame = false;
                        }
                        else if (frame != null)
                            RepeatedFrames++;
                    }

                    if (frame == null)
                    {
                        // Nothing captured yet: the timeline starts with the first frame.
                        stopping.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(5));
                        continue;
                    }

                    pipe.Write(frame);
                    written++;
                    started.TrySetResult();
                }
            }
            catch (Exception e) when (!stopping.IsCancellationRequested)
            {
                fail(new IOException(@"FFmpeg stopped accepting video.", e));
            }
            catch (Exception)
            {
                // The encoder is shutting down.
            }
            finally
            {
                try { pipe.Dispose(); }
                catch (Exception) { }
            }
        }

        private void writeAudio()
        {
            MixerAudioCapture? capture = null;
            Socket? socket = null;

            try
            {
                var accept = audioListener!.AcceptSocketAsync(stopping.Token).AsTask();
                if (!accept.Wait(startup_timeout))
                    throw new TimeoutException(@"FFmpeg did not connect to the audio input.");

                socket = accept.GetResultSafely();
                // Only FFmpeg is expected; nothing else may connect afterwards.
                audioListener.Stop();
                socket.NoDelay = true;

                capture = audio.BeginCapture(startTimestamp);

                float[] buffer = new float[2048 * MixerAudioCapture.CHANNELS];
                byte[] bytes = new byte[buffer.Length * sizeof(float)];

                while (!stopping.IsCancellationRequested)
                {
                    long ready = (long)((Stopwatch.GetTimestamp() - startTimestamp) * (double)MixerAudioCapture.SAMPLE_RATE / Stopwatch.Frequency) - audio_latency_samples;

                    if (capture.Position >= ready)
                    {
                        stopping.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(10));
                        continue;
                    }

                    int count = (int)Math.Min(buffer.Length / MixerAudioCapture.CHANNELS, ready - capture.Position);
                    capture.Read(buffer, count);

                    int length = count * MixerAudioCapture.CHANNELS * sizeof(float);
                    Buffer.BlockCopy(buffer, 0, bytes, 0, length);
                    socket.Send(bytes, 0, length, SocketFlags.None);
                }
            }
            catch (Exception e) when (!stopping.IsCancellationRequested)
            {
                fail(new IOException(@"FFmpeg stopped accepting audio.", e));
            }
            catch (Exception)
            {
                // The encoder is shutting down.
            }
            finally
            {
                capture?.Dispose();

                try
                {
                    socket?.Shutdown(SocketShutdown.Both);
                    socket?.Dispose();
                }
                catch (Exception)
                {
                }

                try { audioListener?.Stop(); }
                catch (Exception) { }
            }
        }

        private async Task finishAsync()
        {
            if (Interlocked.Exchange(ref finishing, 1) != 0)
                return;

            try
            {
                if (encoder != null)
                {
                    var exited = encoder.WaitForExitAsync();

                    // FFmpeg exits by itself when it fails (a refused stream, a missing codec), or after both inputs close.
                    await Task.WhenAny(exited, waitForStop()).ConfigureAwait(false);
                    requestStop();

                    if (await Task.WhenAny(exited, Task.Delay(exit_timeout)).ConfigureAwait(false) != exited)
                    {
                        kill(encoder);
                        failure ??= new TimeoutException(@"FFmpeg did not finish in time.");
                    }

                    await exited.ConfigureAwait(false);

                    string tail = stderrTail == null ? string.Empty : await stderrTail.ConfigureAwait(false);

                    if (encoder.ExitCode != 0 && failure == null)
                        failure = new InvalidOperationException($@"FFmpeg exited with code {encoder.ExitCode}: {redact(tail.Trim())}");
                }

                await drawThreadReleased.Task.WaitAsync(exit_timeout).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                failure ??= e;
            }
            finally
            {
                encoder?.Dispose();

                Logger.Log($@"FFmpeg capture finished: {DroppedFrames} frames dropped, {RepeatedFrames} repeated.");

                if (failure != null)
                {
                    started.TrySetException(failure);
                    completion.TrySetException(failure);
                }
                else
                {
                    started.TrySetCanceled();
                    completion.TrySetResult();
                }
            }
        }

        private Task waitForStop()
        {
            var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            stopping.Token.Register(() => stopped.TrySetResult());
            return stopped.Task;
        }

        /// <summary>
        /// Keeps the publishing address, which carries the stream key, out of error messages.
        /// </summary>
        private string redact(string message) => streaming ? message.Replace(output, @"<stream address>", StringComparison.Ordinal) : message;

        private static async Task<string> readTailAsync(StreamReader reader)
        {
            var tail = new StringBuilder();
            char[] buffer = new char[1024];
            int read;

            while ((read = await reader.ReadAsync(buffer, CancellationToken.None).ConfigureAwait(false)) > 0)
            {
                tail.Append(buffer, 0, read);
                if (tail.Length > stderr_tail_length)
                    tail.Remove(0, tail.Length - stderr_tail_length);
            }

            return tail.ToString();
        }

        private static void kill(Process process)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(true);
            }
            catch (Exception)
            {
            }
        }

        private static void add(ProcessStartInfo info, params string[] arguments)
        {
            foreach (string argument in arguments)
                info.ArgumentList.Add(argument);
        }

        private static string number(int value) => value.ToString(CultureInfo.InvariantCulture);

        #endregion
    }
}
