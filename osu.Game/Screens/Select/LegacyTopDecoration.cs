// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// osu!stable's "mode button" skinning trick. Skins ship a large selection-mode plus a
    /// mode-*-small so the TOP part of song select becomes skinnable. stable draws that mode-button
    /// graphic BEHIND the song select text/controls. torii's footer draws the same textures, but the
    /// footer sits at the top of the screen, so it covered the beatmap info / Group / Sort / Rankings.
    /// Here we draw the same mode-button graphic as a layer IN FRONT of the carousel but BEHIND the
    /// legacy chrome, so the labels/boxes read on top of it (the footer's own mode button stays clamped
    /// to normal size so it can't cover them again). Positioned to match the footer's.
    /// </summary>
    public partial class LegacyTopDecoration : CompositeDrawable
    {
        // Matches LegacyFooter.buttons_pos_16_9 (140 * 1.6), the X of the footer's button cluster.
        private const float components_x = 140 * 1.6f;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        private Sprite background = null!;
        private Sprite icon = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                // selection-mode background: sits at the mode button's bottom-left (like LegacyFooterButton).
                background = new Sprite
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Position = new Vector2(components_x, 0),
                },
                // mode-*-small icon: offset from the button's bottom-left (like LegacyRulesetFooterButton).
                icon = new Sprite
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.Centre,
                    Position = new Vector2(components_x + 57.6f / 2 * 1.6f, -35 * 1.6f),
                    // stable draws the little mode icon additively (it glows over the decoration).
                    Blending = BlendingParameters.Additive,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            skin.SourceChanged += updateTextures;
            ruleset.BindValueChanged(_ => updateTextures(), true);
        }

        private void updateTextures()
        {
            background.Texture = skin.GetTexture(@"selection-mode");
            icon.Texture = skin.GetTexture($@"mode-{ruleset.Value.ShortName}-small");
        }

        protected override void Dispose(bool isDisposing)
        {
            if (skin.IsNotNull())
                skin.SourceChanged -= updateTextures;

            base.Dispose(isDisposing);
        }
    }
}
