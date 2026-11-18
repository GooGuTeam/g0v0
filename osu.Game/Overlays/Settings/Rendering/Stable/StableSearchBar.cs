// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.Containers;
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
    public partial class StableSearchBar : CompositeDrawable
    {
        private static readonly float icon_gap = M.SEARCH_FONT_SIZE * 0.25f;

        private readonly StableSearchTextBox searchTextBox;
        private readonly SpriteIcon searchIcon;

        public StableSearchBar(SettingsSearchTextBox textBox)
        {
            RelativeSizeAxes = Axes.X;
            Height = M.SEARCH_BAR_HEIGHT;

            if (textBox is StableSearchTextBox stableBox)
            {
                searchTextBox = stableBox;
            }
            else
            {
                searchTextBox = new StableSearchTextBox
                {
                    PlaceholderText = textBox.PlaceholderText,
                    Current = textBox.Current,
                };
            }

            searchTextBox.RelativeSizeAxes = Axes.Both;
            searchTextBox.Size = Vector2.One;
            searchTextBox.Anchor = Anchor.Centre;
            searchTextBox.Origin = Anchor.Centre;

            var children = new List<Drawable>();

            if (!ReferenceEquals(textBox, searchTextBox))
            {
                textBox.Alpha = 0;
                textBox.AlwaysPresent = true;
                children.Add(textBox);
            }

            children.Add(searchTextBox);
            children.Add(searchIcon = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Icon = FontAwesome.Solid.Search,
                Size = new Vector2(M.SEARCH_ICON_SIZE),
                Shadow = true,
            });

            InternalChildren = children;
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            float textLeft = ToLocalSpace(searchTextBox.VisibleTextScreenSpaceTopLeft).X;
            searchIcon.X = Math.Max(StableSettingsItem.CONTENT_RIGHT, textLeft - icon_gap - M.SEARCH_ICON_SIZE);
        }

        public partial class StableSearchTextBox : SettingsSearchTextBox
        {
            private Caret? caret;

            protected override float LeftRightPadding => StableSettingsItem.CONTENT_RIGHT + M.SEARCH_ICON_SIZE + icon_gap;

            protected override FontUsage Font => M.Font(M.SEARCH_FONT_SIZE);

            public Vector2 VisibleTextScreenSpaceTopLeft
                => TextContainer.DrawWidth > 0 ? TextContainer.ScreenSpaceDrawQuad.TopLeft : Placeholder.ScreenSpaceDrawQuad.TopLeft;

            public StableSearchTextBox()
            {
                DrawBorder = false;

                foreach (var icon in Children.OfType<SpriteIcon>().ToArray())
                    Remove(icon, true);

                TextFlow.Padding = new MarginPadding();
                TextContainer.Anchor = Anchor.Centre;
                TextContainer.Origin = Anchor.Centre;
                TextContainer.Height = M.SEARCH_FONT_SIZE / M.SEARCH_BAR_HEIGHT;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                BackgroundUnfocused = Color4.Transparent;
                BackgroundFocused = Color4.Transparent;
                BackgroundCommit = Color4.Transparent;
                BorderThickness = 0;
                CornerRadius = 0;
                Placeholder.Colour = Color4.White;
            }

            protected override SpriteText CreatePlaceholder() => new OsuSpriteText
            {
                Font = Font,
                Shadow = true,
                Colour = Color4.White,
            };

            protected override Drawable GetDrawableCharacter(char c) => new FallingDownContainer
            {
                AutoSizeAxes = Axes.Both,
                Child = new OsuSpriteText
                {
                    Text = c.ToString(),
                    Font = Font,
                    Shadow = true,
                },
            };

            protected override Caret CreateCaret() => caret = base.CreateCaret();

            protected override void UpdateAfterChildren()
            {
                base.UpdateAfterChildren();

                if (caret != null && HasFocus)
                {
                    // pTextBox shows its cursor only when text or IME composition is active, not over the centred placeholder.
                    if (string.IsNullOrEmpty(Text) && !ImeCompositionActive)
                        caret.Alpha = 0;
                    else
                    {
                        caret.ClearTransforms(false, nameof(Alpha));
                        caret.Alpha = 1;
                    }
                }
            }
        }
    }
}
