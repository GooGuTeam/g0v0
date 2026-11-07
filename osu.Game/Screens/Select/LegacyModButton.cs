// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Select
{
    public partial class LegacyModButton : CompositeDrawable, IHasTooltip
    {
        public readonly IReadOnlyList<Mod> AvailableMods;
        public readonly IReadOnlyList<string> TextureNames;

        public Mod? ActiveMod { get; private set; }

        public Action<LegacyModButton, bool>? OnCycle { get; set; }

        private Sprite glowSprite = null!;
        private Container iconContainer = null!;
        private readonly List<Drawable> iconDrawables = new List<Drawable>();

        private Sample? hoverSample;
        private Sample? checkOnSample;
        private Sample? checkOffSample;

        public LocalisableString TooltipText
        {
            get
            {
                var mod = ActiveMod ?? AvailableMods.FirstOrDefault();
                if (mod == null)
                    return string.Empty;

                // interpolate as a localisable string: a plain string interpolation bakes in the description's English fallback.
                return LocalisableString.Interpolate($"{mod.Name}\n{mod.Description}");
            }
        }

        public LegacyModButton(Vector2 position, params (string texture, Mod mod)[] states)
        {
            Position = position;
            Origin = Anchor.Centre;
            Size = new Vector2(50, 50);

            AvailableMods = states.Select(s => s.mod).ToList();
            TextureNames = states.Select(s => s.texture).ToList();
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, SkinManager skins, AudioManager audio)
        {
            hoverSample = audio.Samples.Get("click-short") ?? audio.Samples.Get("menuclick");
            checkOnSample = audio.Samples.Get(@"UI/check-on");
            checkOffSample = audio.Samples.Get(@"UI/check-off");

            // stable loads the glow from its own resources only (ModButton: SkinSource.Osu), so skins never replace it.
            var glowTexture = skins.DefaultClassicSkin.GetStableTexture("lighting");

            InternalChildren = new Drawable[]
            {
                glowSprite = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Texture = glowTexture,
                    Colour = Color4.SkyBlue,
                    Blending = BlendingParameters.Additive,
                    Alpha = 0,
                    Scale = new Vector2(1 / LegacySkin.STABLE_MAGIC_SCALE_FACTOR),
                },
                iconContainer = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                }
            };

            for (int i = 0; i < AvailableMods.Count; i++)
            {
                string texName = TextureNames[i];
                var tex = skin.GetStableTexture(texName) ?? skins.DefaultClassicSkin.GetStableTexture(texName);

                Drawable drawable;

                if (tex != null)
                {
                    drawable = new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Texture = tex,
                        Scale = new Vector2(1 / LegacySkin.STABLE_MAGIC_SCALE_FACTOR),
                        Alpha = 0,
                    };
                }
                else
                {
                    drawable = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Font = OsuFont.Inter.With(size: 16, weight: FontWeight.Bold),
                        Text = AvailableMods[i].Acronym,
                        Colour = Color4.White,
                        Alpha = 0,
                    };
                }

                iconDrawables.Add(drawable);
                iconContainer.Add(drawable);
            }

            UpdateState(ActiveMod, playSound: false);
        }

        public void UpdateState(Mod? newActiveMod, bool playSound = false)
        {
            var previousMod = ActiveMod;
            ActiveMod = newActiveMod;

            if (ActiveMod == null)
            {
                for (int i = 0; i < iconDrawables.Count; i++)
                {
                    if (i == 0)
                        iconDrawables[i].FadeIn(100);
                    else
                        iconDrawables[i].FadeOut(100);
                }

                iconContainer.ScaleTo(1.0f, 400, Easing.OutElastic);
                iconContainer.RotateTo(0f, 400, Easing.OutElastic);
                glowSprite.FadeOut(100);

                if (playSound && previousMod != null)
                    checkOffSample?.Play();
            }
            else
            {
                int activeIndex = -1;
                for (int i = 0; i < AvailableMods.Count; i++)
                {
                    if (AvailableMods[i].GetType() == ActiveMod.GetType())
                    {
                        activeIndex = i;
                        break;
                    }
                }

                if (activeIndex == -1)
                    activeIndex = 0;

                for (int i = 0; i < iconDrawables.Count; i++)
                {
                    if (i == activeIndex)
                        iconDrawables[i].FadeIn(100);
                    else
                        iconDrawables[i].FadeOut(100);
                }

                iconContainer.ScaleTo(1.2f, 400, Easing.OutElastic);
                // stable rotates by 0.1 radians; osu!framework rotations are in degrees.
                iconContainer.RotateTo(MathHelper.RadiansToDegrees(0.1f), 400, Easing.OutElastic);
                glowSprite.FadeTo(0.2f, 100);

                if (playSound)
                    checkOnSample?.Play();
            }
        }

        protected override bool OnHover(HoverEvent e)
        {
            hoverSample?.Play();
            return true;
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (e.Button == MouseButton.Left)
            {
                OnCycle?.Invoke(this, false);
                return true;
            }

            return base.OnClick(e);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button == MouseButton.Right)
            {
                OnCycle?.Invoke(this, true);
                return true;
            }

            return base.OnMouseDown(e);
        }
    }
}
