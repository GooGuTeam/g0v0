// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;

namespace osu.Game.Recording.Live
{
    /// <summary>
    /// A running capture of the game window and audio, into a file or a live stream.
    /// </summary>
    public interface ILiveCapture
    {
        /// <summary>
        /// Completes once the first frame reached the encoder (and, when streaming, the publisher connected); faults if startup fails.
        /// </summary>
        Task Started { get; }

        /// <summary>
        /// Completes when the capture ends, after <see cref="StopAsync"/> or by itself; faults if it fails.
        /// </summary>
        Task Completion { get; }

        /// <summary>
        /// Stops capturing and finalises the output. Call from outside the draw thread.
        /// </summary>
        Task StopAsync();
    }
}
