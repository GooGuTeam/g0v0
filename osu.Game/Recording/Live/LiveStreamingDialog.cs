// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Recording.Live
{
    public partial class LiveStreamingDialog : PopupDialog
    {
        private readonly OsuPasswordTextBox destination;

        /// <param name="start">Starts streaming to the entered address.</param>
        /// <param name="viaFfmpeg">Whether FFmpeg will publish the stream, which exposes the address to other local processes.</param>
        public LiveStreamingDialog(Action<string> start, bool viaFfmpeg)
        {
            HeaderText = VideoRecordingStrings.StreamTitle;
            BodyText = viaFfmpeg ? LocalisableString.Interpolate($"{VideoRecordingStrings.StreamHelp} {VideoRecordingStrings.StreamFfmpegNotice}") : VideoRecordingStrings.StreamHelp;
            Icon = FontAwesome.Solid.BroadcastTower;
            MainContent.Add(destination = new OsuPasswordTextBox
            {
                RelativeSizeAxes = Axes.X,
                Height = 40,
                PlaceholderText = VideoRecordingStrings.StreamDestination,
            });
            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton
                {
                    Text = VideoRecordingStrings.StreamStart,
                    Action = () =>
                    {
                        string address = destination.Text.Trim();
                        destination.Text = string.Empty;
                        start(address);
                    },
                },
                new PopupDialogCancelButton { Text = VideoRecordingStrings.ExportCancel },
            };
        }

        protected override void Dispose(bool isDisposing)
        {
            destination.Text = string.Empty;
            base.Dispose(isDisposing);
        }
    }
}
