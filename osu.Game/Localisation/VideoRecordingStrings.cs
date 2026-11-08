// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class VideoRecordingStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.VideoRecording";

        /// <summary>
        /// "Video bitrate"
        /// </summary>
        public static LocalisableString VideoBitrate => new TranslatableString(getKey(@"video_bitrate"), @"Video bitrate");
        /// <summary>
        /// "{0} Mbps"
        /// </summary>
        public static LocalisableString BitrateValue(int value) => new TranslatableString(getKey(@"bitrate_value"), @"{0} Mbps", value);
        /// <summary>
        /// "Video frame rate"
        /// </summary>
        public static LocalisableString VideoFrameRate => new TranslatableString(getKey(@"video_frame_rate"), @"Video frame rate");
        /// <summary>
        /// "H.264 profile"
        /// </summary>
        public static LocalisableString EncoderProfile => new TranslatableString(getKey(@"encoder_profile"), @"H.264 profile");
        /// <summary>
        /// "Automatic"
        /// </summary>
        public static LocalisableString EncoderAutomatic => new TranslatableString(getKey(@"encoder_automatic"), @"Automatic");
        /// <summary>
        /// "Changes apply to the next recording, stream or replay export."
        /// </summary>
        public static LocalisableString RecordingSettingsHelp => new TranslatableString(getKey(@"recording_settings_help"), @"Changes apply to the next recording, stream or replay export.");
        /// <summary>
        /// "Use Automatic unless you need a specific profile. Unsupported profiles prevent encoding."
        /// </summary>
        public static LocalisableString EncoderHelp => new TranslatableString(getKey(@"encoder_help"), @"Use Automatic unless you need a specific profile. Unsupported profiles prevent encoding.");

        /// <summary>
        /// "Video resolution"
        /// </summary>
        public static LocalisableString VideoResolution => new TranslatableString(getKey(@"video_resolution"), @"Video resolution");
        /// <summary>
        /// "Output video size. Window capture preserves aspect ratio and adds black bars where needed."
        /// </summary>
        public static LocalisableString ResolutionHelp => new TranslatableString(getKey(@"resolution_help"), @"Output video size. Window capture preserves aspect ratio and adds black bars where needed.");
        /// <summary>
        /// "Encoding backend"
        /// </summary>
        public static LocalisableString RecordingBackend => new TranslatableString(getKey(@"recording_backend"), @"Encoding backend");
        /// <summary>
        /// "Used for recording, streaming and replay export. Automatic selects Media Foundation on Windows and FFmpeg on Linux/macOS. Install FFmpeg with libx264 and AAC support to use it."
        /// </summary>
        public static LocalisableString RecordingBackendHelp => new TranslatableString(getKey(@"recording_backend_help"), @"Used for recording, streaming and replay export. Automatic selects Media Foundation on Windows and FFmpeg on Linux/macOS. Install FFmpeg with libx264 and AAC support to use it.");
        /// <summary>
        /// "Media Foundation hardware H.264 (Windows)"
        /// </summary>
        public static LocalisableString MediaFoundationBackend => new TranslatableString(getKey(@"media_foundation_backend"), @"Media Foundation hardware H.264 (Windows)");
        /// <summary>
        /// "FFmpeg (libx264)"
        /// </summary>
        public static LocalisableString FfmpegBackend => new TranslatableString(getKey(@"ffmpeg_backend"), @"FFmpeg (libx264)");
        /// <summary>
        /// "FFmpeg executable path"
        /// </summary>
        public static LocalisableString FfmpegPath => new TranslatableString(getKey(@"ffmpeg_path"), @"FFmpeg executable path");

        /// <summary>
        /// "Recording started."
        /// </summary>
        public static LocalisableString Started => new TranslatableString(getKey(@"started"), @"Recording started.");
        /// <summary>
        /// "Recording saved: {0}"
        /// </summary>
        public static LocalisableString Saved(string path) => new TranslatableString(getKey(@"saved"), @"Recording saved: {0}", path);
        /// <summary>
        /// "Recording failed; output may be incomplete: {0}"
        /// </summary>
        public static LocalisableString Failed(string error) => new TranslatableString(getKey(@"failed"), @"Recording failed; output may be incomplete: {0}", error);

        /// <summary>
        /// "Create video"
        /// </summary>
        public static LocalisableString ExportReplay => new TranslatableString(getKey(@"export_replay"), @"Create video");

        /// <summary>
        /// "Create autoplay video"
        /// </summary>
        public static LocalisableString ExportAutoplay => new TranslatableString(getKey(@"export_autoplay"), @"Create autoplay video");

        /// <summary>
        /// "Preparing replay video export…"
        /// </summary>
        public static LocalisableString ExportPreparing => new TranslatableString(getKey(@"export_preparing"), @"Preparing replay video export…");

        /// <summary>
        /// "Finalising video…"
        /// </summary>
        public static LocalisableString ExportFinalising => new TranslatableString(getKey(@"export_finalising"), @"Finalising video…");

        /// <summary>
        /// "Rendering replay: {0}/{1} frames ({2:0.00}×)"
        /// </summary>
        public static LocalisableString ExportProgress(long frame, long total, double speed) => new TranslatableString(getKey(@"export_progress"), @"Rendering replay: {0}/{1} frames ({2:0.00}×)", frame, total, speed);

        /// <summary>
        /// "Cancel"
        /// </summary>
        public static LocalisableString ExportCancel => new TranslatableString(getKey(@"export_cancel"), @"Cancel");

        /// <summary>
        /// "Live streaming"
        /// </summary>
        public static LocalisableString StreamTitle => new TranslatableString(getKey(@"stream_title"), @"Live streaming");
        /// <summary>
        /// "Enter your complete RTMPS publishing URL, including the stream key. It stays in memory for this session. Your game window and game audio will be broadcast."
        /// </summary>
        public static LocalisableString StreamHelp => new TranslatableString(getKey(@"stream_help"), @"Enter your complete RTMPS publishing URL, including the stream key. It stays in memory for this session. Your game window and game audio will be broadcast.");
        /// <summary>
        /// "RTMPS publishing URL"
        /// </summary>
        public static LocalisableString StreamDestination => new TranslatableString(getKey(@"stream_destination"), @"RTMPS publishing URL");
        /// <summary>
        /// "FFmpeg publishes this stream, so the URL is passed to the FFmpeg process, where other programs on this computer can read it."
        /// </summary>
        public static LocalisableString StreamFfmpegNotice => new TranslatableString(getKey(@"stream_ffmpeg_notice"), @"FFmpeg publishes this stream, so the URL is passed to the FFmpeg process, where other programs on this computer can read it.");
        /// <summary>
        /// "Start streaming"
        /// </summary>
        public static LocalisableString StreamStart => new TranslatableString(getKey(@"stream_start"), @"Start streaming");
        /// <summary>
        /// "Live streaming started."
        /// </summary>
        public static LocalisableString StreamStarted => new TranslatableString(getKey(@"stream_started"), @"Live streaming started.");
        /// <summary>
        /// "Live streaming stopped."
        /// </summary>
        public static LocalisableString StreamStopped => new TranslatableString(getKey(@"stream_stopped"), @"Live streaming stopped.");
        /// <summary>
        /// "Unable to stream. Check your publishing URL, network connection and video encoder."
        /// </summary>
        public static LocalisableString StreamFailed => new TranslatableString(getKey(@"stream_failed"), @"Unable to stream. Check your publishing URL, network connection and video encoder.");
        /// <summary>
        /// "Stop the current recording or stream before starting another capture."
        /// </summary>
        public static LocalisableString CaptureBusy => new TranslatableString(getKey(@"capture_busy"), @"Stop the current recording or stream before starting another capture.");

        /// <summary>
        /// "Recording and live streaming"
        /// </summary>
        public static LocalisableString CaptureSettings => new TranslatableString(getKey(@"capture_settings"), @"Recording and live streaming");

        /// <summary>
        /// "Start recording"
        /// </summary>
        public static LocalisableString RecordToggle => new TranslatableString(getKey(@"record_toggle"), @"Start recording");

        /// <summary>
        /// "Stop recording"
        /// </summary>
        public static LocalisableString RecordStop => new TranslatableString(getKey(@"record_stop"), @"Stop recording");

        /// <summary>
        /// "Stop streaming"
        /// </summary>
        public static LocalisableString StreamStop => new TranslatableString(getKey(@"stream_stop"), @"Stop streaming");

        /// <summary>
        /// "Start / stop video recording"
        /// </summary>
        public static LocalisableString ToggleRecording => new TranslatableString(getKey(@"toggle_recording"), @"Start / stop video recording");

        /// <summary>
        /// "Open streaming dialog / stop stream"
        /// </summary>
        public static LocalisableString ToggleStreaming => new TranslatableString(getKey(@"toggle_streaming"), @"Open streaming dialog / stop stream");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
