// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Veldrid;
using osu.Framework.Graphics.Video;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Skinning;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>
    /// Renders an autoplay replay through the offline replay export path with the encoder replaced by a recorder
    /// that only waits for GPU completion. Each frame is unthrottled and deterministic, so frame times include
    /// scene update, draw submission and GPU work, without vsync or window state.
    /// </summary>
    [Explicit("Benchmark; renders on a real GPU device.")]
    public partial class TestSceneOfflineRenderBenchmark : OsuTestScene
    {
        private const int frame_rate = 60;
        private const int audio_frames_per_video_frame = 48000 / frame_rate;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [TestCase(1920, 1080)]
        [TestCase(2400, 1080)]
        public void TestRenderAutoplay(int width, int height)
        {
            int frames = int.TryParse(Environment.GetEnvironmentVariable("G0V0_BENCH_FRAMES"), out int f) ? f : 1800;
            string label = Environment.GetEnvironmentVariable("G0V0_BENCH_LABEL") ?? "default";
            Thread? worker = null;
            Exception? failure = null;
            double[]? times = null;

            AddStep("render", () =>
            {
                var ruleset = new OsuRuleset();
                var beatmap = CreateWorkingBeatmap(ruleset.RulesetInfo);
                var mods = new Mod[] { new OsuModAutoplay() };
                var playable = beatmap.GetPlayableBeatmap(ruleset.RulesetInfo, mods);
                var replayData = ((ICreateReplayData)mods[0]).CreateReplayData(playable, mods);
                var score = new Score
                {
                    Replay = replayData.Replay,
                    ScoreInfo = new ScoreInfo { Ruleset = ruleset.RulesetInfo, Mods = mods, BeatmapInfo = beatmap.BeatmapInfo },
                };
                double startTime = playable.HitObjects.First().StartTime - 1000;

                worker = new Thread(() =>
                {
                    try
                    {
                        times = render(beatmap, score, width, height, startTime, frames);
                    }
                    catch (Exception e)
                    {
                        failure = e;
                    }
                })
                { IsBackground = true, Name = @"Offline render benchmark" };
                worker.Start();

                // Long runs exceed the step timeout of AddUntilStep; nothing else needs the headless host meanwhile.
                worker.Join();
            });

            AddAssert("no failure", () =>
            {
                if (failure != null)
                    throw failure;

                return times != null;
            });

            AddStep("report", () => report(label, width, height, times!));
        }

        private double[] render(WorkingBeatmap beatmap, Score score, int width, int height, double startTime, int frames)
        {
            string destination = Path.Combine(Path.GetTempPath(), $@"g0v0-benchmark-{Guid.NewGuid():N}.mp4");
            var options = new VideoRecordingOptions(frame_rate, width: width, height: height);
            double[] times = new double[frames];

            // A persistent cache like the game's; set G0V0_BENCH_CLEAR_SHADER_CACHE=1 to measure a cold cache.
            string cacheDirectory = Path.Combine(Path.GetTempPath(), @"g0v0-benchmark-shader-cache");
            if (Environment.GetEnvironmentVariable("G0V0_BENCH_CLEAR_SHADER_CACHE") == "1" && Directory.Exists(cacheDirectory))
                Directory.Delete(cacheDirectory, true);

            // Set G0V0_BENCH_FFMPEG to an executable to include FFmpeg readback and piping in each frame,
            // or G0V0_BENCH_NATIVE=1 to include Media Foundation conversion and encoding.
            string? ffmpeg = Environment.GetEnvironmentVariable("G0V0_BENCH_FFMPEG");
            bool native = Environment.GetEnvironmentVariable("G0V0_BENCH_NATIVE") == "1";
            bool encodes = native || !string.IsNullOrEmpty(ffmpeg);

            IOfflineVideoRecording? recording = null;

            IOfflineVideoRecording createRecording(OfflineRenderContext context) => recording =
                native ? new OfflineVideoRecording(context, destination, width, height, options)
                : !string.IsNullOrEmpty(ffmpeg) ? new FfmpegOfflineVideoRecording(context, destination, options, ffmpeg, CancellationToken.None)
                : new GpuWaitRecording(context);

            var setup = Stopwatch.StartNew();
            var session = new OfflineReplayExportSession(beatmap, score, ArgonSkin.CreateInfo(), skins, new Dictionary<OsuSetting, string>(), typeof(OsuRuleset), new Dictionary<string, string>(),
                destination, startTime, 1, options, ReplayVideoBackend.Automatic, string.Empty, new NativeStorage(cacheDirectory), createRecording,
                CancellationToken.None);
            TestContext.Progress.WriteLine($@"session setup {setup.Elapsed.TotalMilliseconds:F0} ms");

            try
            {
                var stopwatch = new Stopwatch();

                for (int i = 0; i < frames; i++)
                {
                    stopwatch.Restart();
                    session.EncodeFrameAsync(i, i * 1000L / frame_rate, (long)i * audio_frames_per_video_frame, audio_frames_per_video_frame, CancellationToken.None).GetAwaiter().GetResult();
                    times[i] = stopwatch.Elapsed.TotalMilliseconds;
                }

                // The recorder writes straight to the destination, so finalise it without the session's publication step.
                if (encodes)
                {
                    var complete = Stopwatch.StartNew();
                    recording!.Complete();
                    TestContext.Progress.WriteLine($@"completion {complete.Elapsed.TotalMilliseconds:F0} ms");
                    if (Environment.GetEnvironmentVariable("G0V0_BENCH_KEEP_OUTPUT") is string keep)
                        File.Copy(destination, keep, true);
                    File.Delete(destination);
                }
            }
            finally
            {
                try
                {
                    session.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    // Timings are still valid; report disposal problems without discarding them.
                    TestContext.Progress.WriteLine($@"session disposal failed: {e.InnerException ?? e}");
                }
            }

            return times;
        }

        private static void report(string label, int width, int height, double[] times)
        {
            // The first second includes scene warm-up (JIT, glyph and texture uploads); report it separately.
            double[] warm = times.Skip(frame_rate).OrderBy(t => t).ToArray();
            double percentile(double p) => warm[Math.Clamp((int)(p * warm.Length), 0, warm.Length - 1)];

            string summary = $@"[{label}] {width}x{height}, {warm.Length} frames after warm-up: mean {warm.Average():F3} ms, p50 {percentile(0.5):F3}, p95 {percentile(0.95):F3}, "
                             + $@"p99 {percentile(0.99):F3}, max {warm[^1]:F3}; first second max {times.Take(frame_rate).Max():F1} ms, total {times.Sum() / 1000:F1} s";

            string csv = Path.Combine(Path.GetTempPath(), $@"g0v0-benchmark-{label}-{width}x{height}.csv");
            File.WriteAllLines(csv, times.Select((t, i) => $@"{i},{t:F4}"));

            Console.WriteLine(summary);
            TestContext.Progress.WriteLine(summary);
            TestContext.Progress.WriteLine($@"per-frame times: {csv}");
        }

        /// <summary>
        /// Discards frames, waiting for the GPU so that each frame's time includes its GPU work.
        /// </summary>
        private sealed class GpuWaitRecording : IOfflineVideoRecording
        {
            private readonly OfflineRenderContext context;

            public long WrittenFrames { get; private set; }
            public long WrittenAudioFrames { get; private set; }
            public int AudioFramesPerVideoFrame => audio_frames_per_video_frame;

            public GpuWaitRecording(OfflineRenderContext context)
            {
                this.context = context;
            }

            public void WriteFrame(Veldrid.Texture texture, float[] audio)
            {
                // G0V0_BENCH_NO_WAIT=1 measures CPU submission alone.
                if (Environment.GetEnvironmentVariable("G0V0_BENCH_NO_WAIT") != "1")
                    context.Device.WaitForIdle();
                WrittenFrames++;
                WrittenAudioFrames += audio_frames_per_video_frame;
            }

            public void Complete()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
