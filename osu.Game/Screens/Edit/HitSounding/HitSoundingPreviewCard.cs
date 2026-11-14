// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Screens.Edit.HitSounding
{
    public partial class HitSoundingPreviewCard : CompositeDrawable, IHasTooltip
    {
        public int SetIndex { get; }
        public LocalisableString TooltipText => SetIndex == 0 ? HitSoundingStrings.TooltipUserSkin : default;
        private readonly Action<int> play;
        private Container surface = null!;
        private OsuSpriteText status = null!;
        private PreviewButton button = null!;
        private bool? selected;
        private bool? playing;

        [Resolved]
        private OverlayColourProvider colours { get; set; } = null!;

        public HitSoundingPreviewCard(int setIndex, Action<int> play)
        {
            SetIndex = setIndex;
            this.play = play;
            Height = 87;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = surface = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 5,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background4 },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.Both, Padding = new MarginPadding(10), Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 10),
                        Children = new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X, Height = 22,
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText { Text = HitSoundingStrings.Set(SetIndex), Font = OsuFont.Default.With(size: 17, weight: FontWeight.Bold), Colour = colours.Light3 },
                                    status = new OsuSpriteText
                                    {
                                        Anchor = Anchor.CentreRight, Origin = Anchor.CentreRight,
                                        Font = OsuFont.Default.With(size: 12), Colour = colours.Light3
                                    }
                                }
                            },
                            button = new PreviewButton { RelativeSizeAxes = Axes.X, Height = 35, Action = () => play(SetIndex) }
                        }
                    }
                }
            };
        }

        public void Refresh(bool isSelected, bool isPlaying, bool enabled)
        {
            button.Enabled.Value = enabled;
            if (selected == isSelected && playing == isPlaying)
                return;

            selected = isSelected;
            playing = isPlaying;
            surface.BorderThickness = isSelected ? 2 : 0;
            surface.BorderColour = colours.Highlight1;
            surface.Children[0].Colour = isSelected ? colours.Background1 : colours.Background4;
            status.Text = isSelected ? HitSoundingStrings.Selected : SetIndex == 0 ? HitSoundingStrings.UserSkin : HitSoundingStrings.CustomSet(SetIndex);
            status.Colour = isSelected ? colours.Highlight1 : colours.Light3;
            button.Text = isPlaying ? HitSoundingStrings.StopPreview : HitSoundingStrings.PlayPreview;
        }

        private partial class PreviewButton : OsuButton
        {
            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                BackgroundColour = colours.Background3;
                SpriteText.Font = OsuFont.Default.With(size: 14);
                Text = HitSoundingStrings.PlayPreview;
            }
        }
    }
}
