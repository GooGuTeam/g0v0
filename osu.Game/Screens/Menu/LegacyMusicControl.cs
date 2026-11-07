// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Menu
{
    /// <summary>
    /// Replicates osu!stable's top-right MusicControl (from MusicControl.cs).
    /// Provides track control buttons (prev, play, pause, stop, next, info),
    /// an interactive seek progress bar, and a title/artist marquee banner.
    /// </summary>
    public partial class LegacyMusicControl : CompositeDrawable
    {
        private const float bar_width = 134f;
        private const float bar_height = 3f;

        [Resolved]
        private MusicController musicController { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        private Box seekProgress = null!;
        private Box seekBackground = null!;
        private Container npContainer = null!;
        private FillFlowContainer transportControls = null!;
        private Container seekContainer = null!;
        private OsuSpriteText npText = null!;
        private Sprite npSprite = null!;

        private const float np_height = 20;

        /// <summary>
        /// menu-np's origin is 100 texture pixels (at 1x) in from its left edge.
        /// </summary>
        private const float np_sprite_origin = 100 / LegacySkin.STABLE_MAGIC_SCALE_FACTOR;

        /// <summary>
        /// Stable truncates the title to 54 characters.
        /// </summary>
        private const int np_max_length = 54;

        [Resolved(canBeNull: true)]
        private NowPlayingOverlay? nowPlaying { get; set; }

        private bool isSeeking;

        public LegacyMusicControl()
        {
            Anchor = Anchor.TopRight;
            Origin = Anchor.TopRight;
            Position = new Vector2(-9, 2);
            RelativeSizeAxes = Axes.X;
            Height = 64;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, SkinManager skins)
        {
            InternalChildren = new Drawable[]
            {
                // Transport buttons row
                transportControls = new FillFlowContainer
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(2, 0),
                    Children = new Drawable[]
                    {
                        new MusicControlButton(FontAwesome.Solid.StepBackward, () => musicController.PreviousTrack()),
                        new MusicControlButton(FontAwesome.Solid.Play, () => musicController.Play()),
                        new MusicControlButton(FontAwesome.Solid.Pause, () =>
                        {
                            if (musicController.CurrentTrack.IsRunning)
                                musicController.Stop();
                            else
                                musicController.Play();
                        }),
                        new MusicControlButton(FontAwesome.Solid.Stop, () =>
                        {
                            musicController.Stop();
                            musicController.SeekTo(0);
                        }),
                        new MusicControlButton(FontAwesome.Solid.StepForward, () => musicController.NextTrack()),
                        new MusicControlButton(FontAwesome.Solid.Info, showSongInfo),
                        new MusicControlButton(FontAwesome.Solid.Bars, () => nowPlaying?.ToggleVisibility()),
                    }
                },
                // Seek bar container
                seekContainer = new Container
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Position = new Vector2(0, 15),
                    Size = new Vector2(bar_width, bar_height + 6),
                    Children = new Drawable[]
                    {
                        seekBackground = new Box
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Size = new Vector2(bar_width, bar_height),
                            Colour = new Color4(255, 255, 255, 22),
                        },
                        seekProgress = new Box
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Size = new Vector2(0, bar_height),
                            Colour = Color4.White,
                        }
                    }
                },
                // Now playing (stable MusicControl, Field TopRight): the title in 14pt white ends 10 units from the screen's right edge,
                // and menu-np is drawn at its own size with its 100th texture pixel at the title's start, running off to the right.
                // This control sits 9 units in from the edge and 2 down, hence the offsets below.
                npContainer = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = np_height,
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        npSprite = new Sprite
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopLeft,
                            Y = -2,
                            Texture = skin.GetStableTexture(@"menu-np") ?? skins.DefaultClassicSkin.GetStableTexture(@"menu-np"),
                            Scale = new Vector2(1 / LegacySkin.STABLE_MAGIC_SCALE_FACTOR),
                        },
                        npText = new OsuSpriteText
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            X = -1,
                            // a 14pt GDI font is 14 * 4/3 pixels at 1x, i.e. 14 * 4/3 / 1.6 stable units.
                            Font = OsuFont.Inter.With(size: 14 * 4 / 3f / LegacySkin.STABLE_MAGIC_SCALE_FACTOR),
                            Colour = Color4.White,
                        },
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            beatmap.BindValueChanged(onBeatmapChanged, true);
        }

        private void onBeatmapChanged(ValueChangedEvent<WorkingBeatmap> e)
        {
            var metadata = e.NewValue?.BeatmapInfo?.Metadata;
            if (metadata != null)
            {
                string title = $"{metadata.Artist} - {metadata.Title}";
                npText.Text = title.Length > np_max_length ? title[..np_max_length] + "..." : title;
                showSongInfo();
            }
        }

        private void showSongInfo()
        {
            // Stable displays metadata above the transport controls for eight seconds.
            // slides in from 90 units to the right over a second, and back out between 6 and 8 seconds.
            npContainer.ClearTransforms();
            npContainer.FadeIn(1000, Easing.Out)
                       .Delay(5000)
                       .FadeOut(2000, Easing.Out);
            npContainer.MoveToX(90)
                       .MoveToX(0, 1000, Easing.Out)
                       .Delay(5000)
                       .MoveToX(90, 2000, Easing.Out);

            transportControls.ClearTransforms();
            transportControls.MoveToY(20, 400)
                             .Delay(7200)
                             .MoveToY(0, 400);
            seekContainer.ClearTransforms();
            seekContainer.MoveToY(35, 400)
                         .Delay(7200)
                         .MoveToY(15, 400);
        }

        protected override void Update()
        {
            base.Update();

            npSprite.X = npText.X - npText.DrawWidth - np_sprite_origin;

            var track = musicController.CurrentTrack;

            if (track != null && track.IsLoaded && track.Length > 0)
            {
                if (!isSeeking)
                {
                    float progress = (float)Math.Clamp(track.CurrentTime / track.Length, 0, 1);
                    seekProgress.Width = progress * bar_width;
                }
            }
            else
            {
                seekProgress.Width = 0;
            }
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (seekBackground.ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
            {
                isSeeking = true;
                seekToPosition(e.ScreenSpaceMousePosition);
                return true;
            }

            return base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            base.OnMouseUp(e);
            isSeeking = false;
        }

        protected override bool OnDragStart(DragStartEvent e) => isSeeking;

        protected override void OnDrag(DragEvent e)
        {
            if (isSeeking)
                seekToPosition(e.ScreenSpaceMousePosition);
        }

        private void seekToPosition(Vector2 screenSpacePosition)
        {
            var localPos = seekBackground.ToLocalSpace(screenSpacePosition);
            float ratio = Math.Clamp(localPos.X / bar_width, 0f, 1f);
            seekProgress.Width = ratio * bar_width;

            var track = musicController.CurrentTrack;
            if (track != null && track.IsLoaded && track.Length > 0)
                musicController.SeekTo(ratio * track.Length);
        }

        private partial class MusicControlButton : ClickableContainer
        {
            private readonly SpriteIcon icon;

            public MusicControlButton(IconUsage iconUsage, Action action)
            {
                Size = new Vector2(18, 18);
                Action = action;

                Child = icon = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(14),
                    Icon = iconUsage,
                    Colour = Color4.White,
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                icon.ScaleTo(1.25f, 100, Easing.OutQuad);
                icon.FadeColour(Color4.Gold, 100, Easing.OutQuad);
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                base.OnHoverLost(e);
                icon.ScaleTo(1f, 100, Easing.OutQuad);
                icon.FadeColour(Color4.White, 100, Easing.OutQuad);
            }
        }
    }
}
