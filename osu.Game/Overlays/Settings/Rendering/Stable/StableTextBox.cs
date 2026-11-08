// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Settings.Entries;
using osuTK;
using osuTK.Graphics;
using M = osu.Game.Overlays.Settings.Rendering.Stable.StableOptionsMetrics;

namespace osu.Game.Overlays.Settings.Rendering.Stable
{
    /// <summary>
    /// osu!stable's options text box (<c>OptionTextbox</c> / <c>pTextBox</c>): the label, then a transparent box with a thin light border below it.
    /// </summary>
    public partial class StableTextBox : StableSettingsItem
    {
        private readonly TextBoxEntry entry;

        public StableTextBox(TextBoxEntry entry)
            : base(entry)
        {
            this.entry = entry;
        }

        protected override Drawable CreateContent() => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, M.TEXTBOX_GAP),
            Children = new Drawable[]
            {
                new TruncatingSpriteText
                {
                    Text = entry.Caption,
                    Font = M.Font(M.LABEL_FONT_SIZE),
                    RelativeSizeAxes = Axes.X,
                },
                new StableTextField
                {
                    RelativeSizeAxes = Axes.X,
                    // 239 of the 244 unit element width.
                    Width = M.TEXTBOX_WIDTH / M.ELEMENT_WIDTH,
                    Height = M.LABEL_FONT_SIZE * 1.5f,
                    PlaceholderText = entry.PlaceholderText,
                    LengthLimit = entry.LengthLimit,
                    Current = entry.Current,
                }.With(textBox =>
                {
                    if (entry.Commit != null)
                    {
                        textBox.CommitOnFocusLost = true;
                        textBox.OnCommit += (_, _) => entry.Commit();
                    }
                }),
            },
        };

        private partial class StableTextField : OsuTextBox
        {

            public StableTextField()
            {
                DrawBorder = false;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                CornerRadius = 0;
                BorderThickness = M.TEXTBOX_BORDER;
                BorderColour = M.TEXTBOX_BORDER_COLOUR;
                BackgroundUnfocused = M.TEXTBOX_BACKGROUND;
                BackgroundFocused = M.TEXTBOX_BACKGROUND_FOCUSED;
                BackgroundCommit = StableSettingsColours.MAIN;
            }

            protected override float LeftRightPadding => 3;

            protected override void OnFocus(FocusEvent e)
            {
                base.OnFocus(e);
                BorderColour = Color4.White;
            }

            protected override void OnFocusLost(FocusLostEvent e)
            {
                base.OnFocusLost(e);
                BorderColour = M.TEXTBOX_BORDER_COLOUR;
            }
        }
    }
}
