// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Dialog
{
    public partial class LiveStreamingDialog : PopupDialog
    {
        private readonly OsuPasswordTextBox destination;

        public LiveStreamingDialog(Action<string> start)
        {
            HeaderText = VideoRecordingStrings.StreamTitle;
            BodyText = VideoRecordingStrings.StreamHelp;
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
