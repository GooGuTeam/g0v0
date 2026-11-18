// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Menu
{
    /// <summary>
    /// Interactive button widget replicating osu!stable's menu button (from Menu.cs).
    /// Supports skins with "menu-button-{kind}" and "-over" overlay, skinnable hover/click samples,
    /// and the additive blue click afterimage on action buttons.
    /// </summary>
    public partial class LegacyMenuButton : ClickableContainer
    {
        public readonly string Kind;
        public readonly Vector2 InitialPosition;

        private Action<UIEvent>? actionWithEvent;

        /// <summary>
        /// An action that receives the triggering event. Like <see cref="ClickableContainer.Action"/>, setting it enables the button.
        /// </summary>
        public Action<UIEvent>? ActionWithEvent
        {
            get => actionWithEvent;
            set
            {
                actionWithEvent = value;
                Enabled.Value = value != null || Action != null;
            }
        }

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        private Sprite baseSprite = null!;
        private Sprite overSprite = null!;
        private OsuSpriteText fallbackText = null!;

        private ISample? hoverSample;
        private ISample? clickSample;

        public LegacyMenuButton(string kind, Vector2 initialPosition)
        {
            Kind = kind;
            InitialPosition = initialPosition;

            Anchor = Anchor.Centre;
            Origin = Anchor.TopLeft;
            Position = initialPosition;
            Alpha = 0;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                baseSprite = new Sprite
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Scale = new Vector2(1 / LegacySkin.STABLE_MAGIC_SCALE_FACTOR),
                },
                overSprite = new Sprite
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Scale = new Vector2(1 / LegacySkin.STABLE_MAGIC_SCALE_FACTOR),
                    Blending = BlendingParameters.Inherit,
                    Alpha = 0,
                },
                fallbackText = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Font = OsuFont.GetFont(size: 24, weight: FontWeight.Bold),
                    Colour = Color4.White,
                    Text = Kind.ToUpperInvariant(),
                }
            };

            updateSkin();
            skin.SourceChanged += onSkinChanged;
        }

        private void onSkinChanged() => Schedule(updateSkin);

        private void updateSkin()
        {
            Texture? getTex(string name) => skin.GetStableTexture(name) ?? skins.DefaultClassicSkin.GetStableTexture(name);

            var baseTex = getTex($"menu-button-{Kind}");
            var overTex = getTex($"menu-button-{Kind}-over") ?? baseTex;

            baseSprite.Texture = baseTex;
            overSprite.Texture = overTex;
            fallbackText.Alpha = baseTex == null ? 1 : 0;

            if (baseTex == null)
            {
                AutoSizeAxes = Axes.None;
                Size = new Vector2(300, 50);
            }
            else
            {
                AutoSizeAxes = Axes.Both;
            }

            bool isBack = Kind.Contains(@"back", StringComparison.OrdinalIgnoreCase);
            hoverSample = resolveSample($"menu-{Kind}-hover", @"menuclick");
            clickSample = resolveSample($"menu-{Kind}-click", isBack ? @"menuback" : @"menuhit");
        }

        private ISample? resolveSample(string primary, string fallback) =>
            skin.GetSample(new SampleInfo(primary))
            ?? skin.GetSample(new SampleInfo(fallback))
            ?? audio.Samples.Get(primary)
            ?? audio.Samples.Get(fallback);

        protected override bool OnHover(HoverEvent e)
        {
            if (!Enabled.Value)
                return base.OnHover(e);

            // Stable Menu.cs line 721: Moves +20px X over 580ms with EasingTypes.OutElastic (25)
            this.MoveToX(InitialPosition.X + 20, 580, Easing.OutElastic);
            overSprite.FadeIn(30);

            hoverSample?.Play();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);

            // Stable Menu.cs line 727: Moves back to initial position over 400ms with EasingTypes.OutCubic (7) / OutQuad
            this.MoveToX(InitialPosition.X, 400, Easing.OutQuad);
            overSprite.FadeOut(500);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (!Enabled.Value)
                return false;

            clickSample?.Play();
            spawnClickAfterimage();

            ActionWithEvent?.Invoke(e);
            Action?.Invoke();
            return true;
        }

        /// <summary>
        /// Stable's <c>Menu.menuItem_OnClick</c> (<c>Menu.cs:764-781</c>): spawns a transient additive blue clone
        /// behind the button/cookie on buttons other than <c>play</c> and <c>back</c>, fading out over 500ms.
        /// </summary>
        private void spawnClickAfterimage()
        {
            if (baseSprite.Texture == null
                || string.Equals(Kind, @"play", StringComparison.OrdinalIgnoreCase)
                || Kind.Contains(@"back", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var afterimage = new AfterimageSprite(Position)
            {
                Depth = 1,
                BypassAutoSizeAxes = Axes.Both,
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Texture = baseSprite.Texture,
                Scale = baseSprite.Scale,
                Blending = BlendingParameters.Additive,
                Colour = new Color4(0, 78, 165, 255),
                Alpha = 0.6f,
            };

            Add(afterimage);
            afterimage.FadeOut(500).Expire();
        }

        /// <summary>
        /// Stable's <c>Menu.SetIdle(false)</c>: fade in over 350ms while sliding in from 100 units to the left over 400ms.
        /// </summary>
        public void SlideIn()
        {
            this.FadeIn(350);
            this.MoveTo(InitialPosition - new Vector2(100, 0))
                .MoveTo(InitialPosition, 400, Easing.Out);
        }

        /// <summary>
        /// Stable's <c>Menu.SetIdle(true)</c>: fade out over 500ms while drifting 100 units to the left over 2000ms.
        /// </summary>
        public void SlideOut(bool instant = false)
        {
            this.FadeOut(instant ? 0 : 500);
            this.MoveTo(InitialPosition - new Vector2(100, 0), instant ? 0 : 2000, Easing.Out);
        }

        /// <summary>
        /// Stable's <c>Menu.fadeAllButtons</c>, run when leaving the menu: move 50 units to the left and fade out over 100ms.
        /// </summary>
        public void FadeAway()
        {
            ClearTransforms();
            this.MoveToOffset(new Vector2(-50, 0), 100);
            this.FadeOut(100);
        }

        public void TransitionOut(bool movingUp)
        {
            this.FadeOut(80, Easing.OutQuad);
            this.MoveTo(InitialPosition + new Vector2(movingUp ? 15 : -30, 0), 80, Easing.OutQuad);
        }

        public void TransitionIn(bool movingUp)
        {
            Position = InitialPosition + new Vector2(movingUp ? 15 : -30, 0);
            this.FadeIn(80, Easing.OutQuad);
            this.MoveTo(InitialPosition, 200, Easing.OutQuad);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;

            base.Dispose(isDisposing);
        }

        private partial class AfterimageSprite : Sprite
        {
            private readonly Vector2 spawnPosition;

            public AfterimageSprite(Vector2 spawnPosition)
            {
                this.spawnPosition = spawnPosition;
            }

            protected override void Update()
            {
                base.Update();

                if (Parent != null)
                    Position = spawnPosition - Parent.Position;
            }
        }
    }
}
