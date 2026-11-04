// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Recording
{
    public static class RecordingBackendExtensions
    {
        /// <summary>
        /// The backend that encodes for this setting on the current platform: Media Foundation on Windows and FFmpeg elsewhere when automatic.
        /// </summary>
        /// <exception cref="PlatformNotSupportedException">Media Foundation was selected outside Windows.</exception>
        public static RecordingBackend Resolve(this RecordingBackend backend)
        {
            switch (backend)
            {
                case RecordingBackend.Automatic:
                    return OperatingSystem.IsWindows() ? RecordingBackend.MediaFoundation : RecordingBackend.Ffmpeg;

                case RecordingBackend.MediaFoundation:
                    if (!OperatingSystem.IsWindows())
                        throw new PlatformNotSupportedException(@"The Media Foundation backend requires Windows. Select FFmpeg in the recording settings.");

                    return backend;

                case RecordingBackend.Ffmpeg:
                    return backend;

                default:
                    throw new ArgumentOutOfRangeException(nameof(backend), backend, null);
            }
        }
    }
}
