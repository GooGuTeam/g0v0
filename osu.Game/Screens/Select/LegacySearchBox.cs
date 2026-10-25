// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// The osu!stable style search box for the legacy song select top bar. A green-yellow "Search:"
    /// label followed by an inline text box over a soft dark background (so it stays legible over the
    /// beatmap background, like stable's faint menu-button-background). Binds two-way to the shared
    /// filter query so typing filters the carousel just like the (hidden) modern search.
    /// </summary>
    public partial class LegacySearchBox : CompositeDrawable
    {
        public Bindable<string> Current { get; } = new Bindable<string>();

        [BackgroundDependencyLoader]
        private void load()
        {
            AutoSizeAxes = Axes.Both;

            InternalChild = new Container
            {
                AutoSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 5,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(0f, 0f, 0f, 0.4f),
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.X,
                        Height = 26,
                        Padding = new MarginPadding { Horizontal = 8 },
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(5, 0),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = @"Search:",
                                Font = LegacyFonts.Get(20, FontWeight.Bold),
                                Colour = Color4.GreenYellow,
                                Shadow = true,
                            },
                            new SearchTextBox
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Width = 200,
                                Height = 24,
                                Current = { BindTarget = Current },
                            },
                        },
                    },
                },
            };
        }

        private partial class SearchTextBox : FocusedTextBox
        {
            // Left/right arrows stay free for the carousel (previous/next group), as in stable; the caret
            // doesn't need them with short queries.
            public override bool HandleLeftRightArrows => false;

            public SearchTextBox()
            {
                // As in lazer: grab and hold focus so the user can type straight away without clicking.
                // Only present in legacy mode, so it doesn't steal focus anywhere else.
                HoldFocus = true;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                BackgroundUnfocused = Color4.Transparent;
                BackgroundFocused = Color4.Transparent;
            }

            protected override bool OnKeyDown(KeyDownEvent e)
            {
                // Do NOT consume Enter: FocusedTextBox would commit on it and swallow the key, so
                // GlobalAction.Select (start the beatmap) would never fire. Let it through, just like
                // FocusedTextBox already does for Escape and GlobalAction.Back. The search filters live
                // through the two-way binding, so the commit isn't needed.
                if (e.Key == Key.Enter || e.Key == Key.KeypadEnter)
                    return false;

                return base.OnKeyDown(e);
            }
        }
    }
}
