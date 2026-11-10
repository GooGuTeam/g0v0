// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osu.Game.Skinning.Select;
using osuTK;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// torii: el truco de skinning del "mode button" de osu!stable. los skins traen un selection-mode
    /// grande + mode-*-small asi la parte de ARRIBA del song select se vuelve skinneable. stable dibuja
    /// ese grafico mode-button DETRAS del texto/controles del song select. el footer de torii dibuja las
    /// mismas texturas, pero el footer queda arriba de la pantalla, asi que tapaban la info del beatmap /
    /// Group / Sort / Rankings. aca dibujamos el mismo grafico mode-button, como capa ADELANTE del carousel
    /// pero DETRAS del chrome legacy, asi los labels/cajas se leen por encima (el mode button propio del
    /// footer queda clampeado a tamano normal asi no vuelve a tapar). posicionado para coincidir con el del footer.
    /// </summary>
    public partial class LegacyTopDecoration : CompositeDrawable
    {
        // the mode button's x from the footer (LegacyFooter.ButtonsX), and the mode icon's offset from it.
        private const float icon_offset_x = 57.6f / 2 * 1.6f;

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
                // bg selection-mode: va en el bottom-left del mode button (como LegacyFooterButton).
                background = new Sprite
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                },
                // icono mode-*-small: con offset desde el bottom-left del boton (como LegacyRulesetFooterButton).
                icon = new Sprite
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.Centre,
                    Y = -35 * 1.6f,
                    // stable dibuja el iconito de modo aditivo (brilla sobre la decoracion).
                    Blending = BlendingParameters.Additive,
                },
            };
        }

        protected override void Update()
        {
            base.Update();

            // follows the footer's buttons, which move to 120 units in on narrower than widescreen windows.
            background.X = LegacyFooter.ButtonsX(DrawWidth);
            icon.X = background.X + icon_offset_x;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            skin.SourceChanged += updateTextures;
            ruleset.BindValueChanged(_ => updateTextures(), true);
        }

        private void updateTextures()
        {
            background.Texture = skin.GetStableTexture(@"selection-mode");
            icon.Texture = skin.GetStableTexture($@"mode-{ruleset.Value.ShortName}-small");
        }

        protected override void Dispose(bool isDisposing)
        {
            if (skin.IsNotNull())
                skin.SourceChanged -= updateTextures;

            base.Dispose(isDisposing);
        }
    }
}
