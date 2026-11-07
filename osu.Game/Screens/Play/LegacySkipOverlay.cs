// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// osu!stable's skip button (<c>Player.InitializeSkip</c>): the skin's <c>play-skip</c> animation in the bottom-right corner, at 60% opacity
    /// until hovered. It fades in when skipping becomes possible and stays until it no longer is, without hiding while the mouse is idle.
    /// </summary>
    public partial class LegacySkipOverlay : SkipOverlay
    {
        private LegacySkipButton button = null!;

        public LegacySkipOverlay(double startTime)
            : base(startTime)
        {
            // the overlay is only as large as the button, so the rest of the screen stays free for gameplay.
            RelativePositionAxes = Axes.None;
            RelativeSizeAxes = Axes.None;
            Anchor = Anchor.BottomRight;
            Origin = Anchor.BottomRight;
            Position = Vector2.Zero;
        }

        // pSprite.FadeIn(400) when skipping becomes possible, IntroSkipBoundaryEnded's FadeOut(200) once it isn't.
        protected override double ButtonFadeInDuration => 400;

        protected override double ButtonFadeOutDuration => 200;

        protected override bool HideWhenIdle => false;

        protected override OsuClickableContainer CreateButton(IBindable<bool> inSkipPeriod) => button = new LegacySkipButton
        {
            Enabled = { BindTarget = inSkipPeriod },
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Size = button.Size;
            RemainingTimeBox.Hide();
        }

        private partial class LegacySkipButton : OsuClickableContainer
        {
            // osu!ui.dll's play-skip is 196x149 at 1x. stable draws a 1x texture's pixels a pixel each at 768 high, as g0v0's units are.
            private static readonly Vector2 default_size = new Vector2(196, 149);

            private const float idle_alpha = 153 / 255f;

            private Drawable content = null!;
            private SkinnableSound confirmSample = null!;

            // stable's skip button makes no sound until it's clicked.
            protected override HoverSounds CreateHoverSounds(HoverSampleSet sampleSet) => new HoverSounds(sampleSet) { Enabled = { Value = false } };

            public LegacySkipButton()
            {
                Anchor = Anchor.BottomRight;
                Origin = Anchor.BottomRight;
            }

            [BackgroundDependencyLoader]
            private void load(ISkinSource skin)
            {
                var frames = skin.GetTextures(@"play-skip", default, default, true, @"-", null, out _);

                if (frames.Length > 0)
                {
                    Size = new Vector2(frames[0].DisplayWidth, frames[0].DisplayHeight);

                    // pAnimation.SetFramerateFromSkin: the skin's AnimationFramerate, or the whole animation over a second.
                    content = skin.GetAnimation(@"play-skip", true, true, applyConfigFrameRate: true)!;
                    content.RelativeSizeAxes = Axes.Both;
                    content.Size = Vector2.One;
                }
                else
                {
                    Size = default_size;
                    content = new LegacyMissingTexture(@"play-skip", UserInterfaceStrings.LegacySkip, new Color4(255, 165, 0, 255), default_size);
                }

                content.Alpha = idle_alpha;

                Children = new[]
                {
                    content,
                    confirmSample = new SkinnableSound(new SampleInfo(@"menuhit")),
                };
            }

            // stable's hover effect fades between 60% and full opacity over 300ms.
            protected override bool OnHover(HoverEvent e)
            {
                content.FadeTo(1, 300);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                content.FadeTo(idle_alpha, 300);
                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                if (!Enabled.Value)
                    return false;

                // Player.DoSkip plays the skin's menuhit.
                confirmSample.Play();

                return base.OnClick(e);
            }
        }
    }
}
