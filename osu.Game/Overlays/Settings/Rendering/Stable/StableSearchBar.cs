// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using M = osu.Game.Overlays.Settings.Rendering.Stable.StableOptionsMetrics;

namespace osu.Game.Overlays.Settings.Rendering.Stable
{
    /// <summary>
    /// osu!stable's options search (<c>pSearchBox(18, .., TextAlignment.Centre)</c>): no box, just the centred text
    /// (or the "type to search" placeholder) with a search icon to its left.
    /// </summary>
    /// <remarks>
    /// The real <see cref="SettingsSearchTextBox"/> stays in place, invisible but present, so focus, typing, IME and filtering behave as before;
    /// this drawable only mirrors its text.
    /// </remarks>
    public partial class StableSearchBar : CompositeDrawable
    {
        private const double caret_blink_period = 500;

        private readonly SettingsSearchTextBox textBox;

        private readonly OsuSpriteText text;
        private readonly Box caret;

        public StableSearchBar(SettingsSearchTextBox textBox)
        {
            this.textBox = textBox;

            RelativeSizeAxes = Axes.X;
            Height = M.SEARCH_BAR_HEIGHT;

            textBox.RelativeSizeAxes = Axes.Both;
            textBox.Size = Vector2.One;
            textBox.Anchor = Anchor.Centre;
            textBox.Origin = Anchor.Centre;
            textBox.Alpha = 0;
            textBox.AlwaysPresent = true;

            InternalChildren = new Drawable[]
            {
                textBox,
                new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(M.SEARCH_FONT_SIZE * 0.25f, 0),
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Icon = FontAwesome.Solid.Search,
                            Size = new Vector2(M.SEARCH_ICON_SIZE),
                            Shadow = true,
                        },
                        new Container
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            AutoSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                text = new OsuSpriteText
                                {
                                    Font = M.Font(M.SEARCH_FONT_SIZE),
                                    Shadow = true,
                                },
                                caret = new Box
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreLeft,
                                    Width = 2,
                                    Height = M.SEARCH_FONT_SIZE,
                                    Colour = Color4.White,
                                    Alpha = 0,
                                },
                            },
                        },
                    },
                },
            };
        }

        protected override void Update()
        {
            base.Update();

            bool empty = string.IsNullOrEmpty(textBox.Current.Value);

            text.Text = empty ? textBox.PlaceholderText : textBox.Current.Value;

            // pTextBox shows its cursor only while focused, and not over the default text.
            bool showCaret = textBox.HasFocus && !empty && Time.Current % (caret_blink_period * 2) < caret_blink_period;
            caret.Alpha = showCaret ? 1 : 0;
        }
    }
}
