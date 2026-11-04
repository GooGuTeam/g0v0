// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays;

namespace osu.Game.Screens.Edit.HitSounding
{
    public partial class HitSoundingLaneHeader : OsuClickableContainer
    {
        public int SetIndex { get; }
        public bool IsCollapsed { get; private set; }

        private Container collapsedContent = null!;
        private Container expandedContent = null!;

        public const double TRANSITION_DURATION = 300;

        public HitSoundingLaneHeader(int set, bool isCollapsed, Action toggleCollapsed)
        {
            SetIndex = set;
            IsCollapsed = isCollapsed;
            Action = toggleCollapsed;
            Width = 200;
            Height = HitSoundingSetLayout.RowHeight(isCollapsed);
            Masking = true;
            CornerRadius = 5;
            TooltipText = isCollapsed ? HitSoundingStrings.ExpandSet : HitSoundingStrings.CollapseSet;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            Add(collapsedContent = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = IsCollapsed ? 1 : 0,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background4 },
                    new SpriteIcon
                    {
                        X = 10, Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft,
                        Width = 10, Height = 10, Icon = FontAwesome.Solid.ChevronRight, Colour = colours.Light3
                    },
                    new OsuSpriteText
                    {
                        X = 28, Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft,
                        Text = HitSoundingStrings.Set(SetIndex), Font = OsuFont.Default.With(size: 14), Colour = colours.Light3
                    }
                }
            });

            Add(expandedContent = new Container
            {
                Width = 200,
                Height = HitSoundingLayout.SET_HEIGHT,
                Alpha = IsCollapsed ? 0 : 1,
                Children = new Drawable[]
                {
                    new Container
                    {
                        Width = 70, RelativeSizeAxes = Axes.Y, Masking = true, CornerRadius = 5,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background4 },
                            new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: 17, weight: FontWeight.Bold))
                            {
                                X = 10, Y = 10, Width = 55, AutoSizeAxes = Axes.Y,
                                Text = HitSoundingStrings.Set(SetIndex), Colour = colours.Light3
                            },
                            new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: 12))
                            {
                                X = 10, Y = 55, Width = 55, AutoSizeAxes = Axes.Y,
                                Colour = colours.Light4,
                                Text = SetIndex == 0 ? HitSoundingStrings.UserSkin : HitSoundingStrings.CustomSet(SetIndex),
                            },
                            new SpriteIcon
                            {
                                X = 10, Y = -10, Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft,
                                Width = 10, Height = 10, Icon = FontAwesome.Solid.ChevronDown, Colour = colours.Light3
                            }
                        }
                    }
                }
            });

            for (int i = 0; i < HitSampleInfo.ALL_BANKS.Length; i++)
            {
                string bank = HitSampleInfo.ALL_BANKS[i];
                expandedContent.Add(new Container
                {
                    X = 73,
                    Y = i * (HitSoundingLayout.BANK_HEIGHT + HitSoundingLayout.BANK_SPACING),
                    Width = 127,
                    Height = HitSoundingLayout.BANK_HEIGHT,
                    Masking = true,
                    CornerRadius = 5,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background5 },
                        new SpriteIcon
                        {
                            X = 10, Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft,
                            Width = 20, Height = 20, Colour = colours.Light3,
                            Icon = bank switch
                            {
                                HitSampleInfo.BANK_NORMAL => OsuIcon.EditorBankNormal,
                                HitSampleInfo.BANK_SOFT => OsuIcon.EditorBankSoft,
                                _ => OsuIcon.EditorBankDrum
                            }
                        },
                        new OsuSpriteText
                        {
                            X = 38, Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft,
                            Text = bank switch
                            {
                                HitSampleInfo.BANK_NORMAL => HitSoundingStrings.Normal,
                                HitSampleInfo.BANK_SOFT => HitSoundingStrings.Soft,
                                _ => HitSoundingStrings.Drum
                            },
                            Font = OsuFont.Default.With(size: 16), Colour = colours.Light3
                        }
                    }
                });
            }
        }

        public void SetCollapsed(bool collapsed)
        {
            IsCollapsed = collapsed;
            TooltipText = collapsed ? HitSoundingStrings.ExpandSet : HitSoundingStrings.CollapseSet;
            this.ResizeHeightTo(HitSoundingSetLayout.RowHeight(collapsed), TRANSITION_DURATION, Easing.OutQuint);
            collapsedContent.FadeTo(collapsed ? 1 : 0, TRANSITION_DURATION, Easing.OutQuint);
            expandedContent.FadeTo(collapsed ? 0 : 1, TRANSITION_DURATION, Easing.OutQuint);
        }
    }
}
