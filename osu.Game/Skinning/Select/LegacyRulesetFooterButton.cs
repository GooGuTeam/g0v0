// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.Skinning.Select
{
    public partial class LegacyRulesetFooterButton : LegacyFooterButton
    {
        private Sprite modeIcon = null!;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        public LegacyRulesetFooterButton()
            : base("mode")
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddInternal(modeIcon = new Sprite
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.Centre,
                BypassAutoSizeAxes = Axes.Both,
                X = 57.6f / 2 * 1.6f,
                Y = -35 * 1.6f,
                // stable draws it additively (s_modeSprite.Additive = true); skins with mode icons on a
                // black background rely on the black turning invisible.
                Blending = BlendingParameters.Additive,
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ISkin source = TextureSource ?? skin;

            ruleset.BindValueChanged(r =>
            {
                // If the skin ships the mode icons as the giant skinnable-top decoration (same as
                // selection-mode), we don't draw them as the button icon: clamped to the slot they look
                // like a weird mini-decoration next to the footer. LegacyFooter draws the decoration separately.
                if (SuppressBaseGlyph)
                {
                    modeIcon.Texture = null;
                    return;
                }

                string name = $@"mode-{r.NewValue.ShortName}-small";
                var tex = source.GetTexture(name) ?? skins.DefaultClassicSkin.GetTexture(name);

                // Some skins ship a giant mode-*-small as the "skinnable top" decoration (drawn separately
                // behind the chrome by LegacyTopDecoration); for those we fall back to the bundled classic
                // icon instead of redrawing the decoration over the footer. Normal icons go at their natural
                // size as in stable, even if they stick out of the slot a little.
                if (tex != null && Math.Max(tex.DisplayWidth, tex.DisplayHeight) > 250)
                    tex = skins.DefaultClassicSkin.GetTexture(name);

                modeIcon.Texture = tex;

                if (tex != null)
                    modeIcon.Size = new Vector2(tex.DisplayWidth, tex.DisplayHeight);
            }, true);
        }
    }
}
