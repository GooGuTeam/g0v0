// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osuTK;

namespace osu.Game.Skinning.Select
{
    public partial class LegacyFooterButton : ClickableContainer
    {
        private readonly string kind;

        private Sprite hoverSprite = null!;
        private SkinnableSound hoverSound = null!;
        private SkinnableSound clickSound = null!;

        /// <summary>Where this button's textures come from. null = the current skin.</summary>
        public ISkin? TextureSource { get; init; }

        /// <summary>
        /// When this button's base glyph is actually the skin's giant skinnable-top decoration
        /// (typically selection-mode), we don't draw it as the button glyph: clamped to the slot it would
        /// come out as a weird mini version of the whole decoration. LegacyFooter draws it separately.
        /// </summary>
        public bool SuppressBaseGlyph { get; init; }

        // stable's button slot. The clickable hit area is AT LEAST this, so a missing / 0x0 texture never
        // leaves the button unclickable; and it grows to cover the hover glyph when the skin ships larger
        // buttons (stable takes input from the -over sprite, i.e. its hit area scales with the texture too).
        private const float slot_width = 74;
        private const float slot_height = 90;

        // Beyond this the texture is footer decoration art, not a button glyph.
        private const float max_glyph_dimension = 250;

        public LegacyFooterButton(string kind)
        {
            this.kind = kind;

            Enabled.Value = true;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, SkinManager skins)
        {
            // Buttons can end up with different heights when the skin ships oversized glyphs; anchored
            // to the bottom they stay baseline-aligned inside the footer row.
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;

            ISkin source = TextureSource ?? skin;
            const Anchor sprite_anchor = Anchor.BottomLeft;

            Texture? texture(string name)
            {
                var tex = source.GetTexture(name) ?? skins.DefaultClassicSkin.GetTexture(name);

                // A decoration-sized texture is no good as a button glyph (some skins stuff the whole
                // footer art into these slots); fall back to the bundled classic glyph instead of drawing
                // it huge over everything.
                if (tex != null && Math.Max(tex.DisplayWidth, tex.DisplayHeight) > max_glyph_dimension)
                    tex = skins.DefaultClassicSkin.GetTexture(name);

                return tex;
            }

            // The two textures: the base glyph (always visible) and the "-over" one (additive hover glow,
            // as in stable). If the skin has no "-over" we fall back to the base so hover still gives feedback.
            var baseTex = texture($"selection-{kind}");
            var overTex = texture($"selection-{kind}-over") ?? baseTex;

            // If this button actually carries the giant decoration (e.g. selection-mode used as the
            // skinnable top), we draw it neither as glyph nor as hover: LegacyFooter draws it separately.
            if (SuppressBaseGlyph)
            {
                baseTex = null;
                overTex = null;
            }

            var overSize = overTex != null ? new Vector2(overTex.DisplayWidth, overTex.DisplayHeight) : Vector2.Zero;
            Size = Vector2.ComponentMax(new Vector2(slot_width, slot_height), overSize);

            Children = new Drawable[]
            {
                new Sprite
                {
                    Anchor = sprite_anchor,
                    Origin = sprite_anchor,
                    Texture = baseTex,
                    BypassAutoSizeAxes = Axes.Both,
                    Size = sizeFor(baseTex),
                },
                // stable draws the -over EXACTLY at the same position as the base (same
                // field/origin/position), only faded in by alpha on hover. No nudges.
                hoverSprite = new Sprite
                {
                    Anchor = sprite_anchor,
                    Origin = sprite_anchor,
                    Texture = overTex,
                    BypassAutoSizeAxes = Axes.Both,
                    Size = sizeFor(overTex),
                    Alpha = 0,
                    AlwaysPresent = true,
                    Blending = BlendingParameters.Additive,
                },
                hoverSound = new SkinnableSound(new SampleInfo("click-short")),
                clickSound = new SkinnableSound(new SampleInfo("click-short-confirm")),
            };

            // Natural size, as in stable: a large button simply sticks out of the footer bar.
            Vector2 sizeFor(Texture? tex) => tex == null ? Vector2.Zero : new Vector2(tex.DisplayWidth, tex.DisplayHeight);
        }

        protected override bool OnHover(HoverEvent e)
        {
            hoverSprite.FadeIn(100);
            hoverSound.Play();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hoverSprite.FadeOut(100);
            base.OnHoverLost(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            clickSound.Play();
            return base.OnClick(e);
        }
    }
}
