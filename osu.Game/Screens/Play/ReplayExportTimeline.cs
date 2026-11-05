// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// Integer-indexed offline export timeline. Wall time never advances this timeline.
    /// The worker acknowledges each fully encoded video/audio interval before advancing.
    /// </summary>
    public sealed class ReplayExportTimeline
    {
        public int FramesPerSecond { get; }
        public int SampleRate { get; }
        public long TotalFrames { get; }
        public long FrameIndex { get; private set; }
        public bool IsComplete => FrameIndex == TotalFrames;
        public double Progress => TotalFrames == 0 ? 1 : (double)FrameIndex / TotalFrames;
        public double TimeMilliseconds => (double)FrameIndex * 1000 / FramesPerSecond;
        public long Timestamp => scale(FrameIndex, 10000000);
        public long AudioStartSample => scale(FrameIndex, SampleRate);
        public int AudioSampleCount => IsComplete ? 0 : checked((int)(scale(FrameIndex + 1, SampleRate) - AudioStartSample));

        public ReplayExportTimeline(TimeSpan duration, int framesPerSecond = 60, int sampleRate = 48000)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
            if (framesPerSecond <= 0 || framesPerSecond > 1000)
                throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
            if (sampleRate < framesPerSecond || sampleRate > 384000)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            FramesPerSecond = framesPerSecond;
            SampleRate = sampleRate;
            TotalFrames = checked((long)decimal.Ceiling((decimal)duration.Ticks * framesPerSecond / TimeSpan.TicksPerSecond));
        }

        public void AcknowledgeFrame(long encodedFrameIndex)
        {
            if (IsComplete || encodedFrameIndex != FrameIndex)
                throw new InvalidOperationException(@"Export frames must be encoded exactly once and in order.");
            FrameIndex++;
        }

        private long scale(long index, int rate) => checked(index / FramesPerSecond * rate + index % FramesPerSecond * rate / FramesPerSecond);
    }
}
