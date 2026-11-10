// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace osu.Game.Recording.Export
{
    /// <summary>
    /// A private export environment, never the running client's player or sound device.
    /// Implementations own rendering and audio resources and perform thread marshalling.
    /// </summary>
    public interface IReplayExportSession : IAsyncDisposable
    {
        /// <summary>
        /// Advance simulation (including all intervening judgements and sample events),
        /// render to a private GPU target, mix the specified PCM interval, then encode both.
        /// Must await backpressure rather than drop frames. The texture lease remains alive
        /// until conversion completes. Cancellation must leave resources safe to dispose.
        /// </summary>
        Task EncodeFrameAsync(long frameIndex, long timestamp, long audioStartSample, int audioSampleCount, CancellationToken cancellationToken);

        /// <summary>
        /// Drain codecs, finalise the temporary file and atomically publish the destination.
        /// Dispose without successful completion must not publish an incomplete video.
        /// </summary>
        Task CompleteAsync(CancellationToken cancellationToken);
    }
}
