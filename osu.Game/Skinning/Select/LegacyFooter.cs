// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.Menu;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning.Select
{
    public partial class LegacyFooter : CompositeDrawable
    {
        private Container components = null!;
        private Container buttonsContainer = null!;
        private LogoTrackingContainer logoTrackingContainer = null!;
        private IDisposable? logoTracking;

        private const float buttons_pos_4_3 = 120 * 1.6f;
        private const float buttons_pos_16_9 = 140 * 1.6f;
        private const float footer_bar_height = 96;

        // Torii: the screen-level footer hooks these up so the legacy chrome can trigger the real
        // song select actions. The upstream PR leaves them dangling.
        public Action? BackAction { get; init; }
        public Action? ModsAction { get; init; }
        public Action? RandomAction { get; init; }
        public Action? OptionsAction { get; init; }

        /// <summary>
        /// Items for the stable-style options popup (the little menu that opens above the options button
        /// with the beatmap actions). If set and OptionsAction is null, the button opens this menu instead
        /// of the modern overlay.
        /// </summary>
        public Func<MenuItem[]>? OptionsMenuItems { get; init; }

        private OsuMenu optionsMenu = null!;

        /// <summary>
        /// Opens/closes the stable-style options popup (also triggered by song select's F3 hotkey while
        /// the legacy chrome is active).
        /// </summary>
        public void ShowOptions()
        {
            if (optionsMenu.State == MenuState.Open)
            {
                optionsMenu.Close();
                return;
            }

            var items = OptionsMenuItems?.Invoke();

            if (items == null || items.Length == 0)
                return;

            optionsMenu.Items = items;
            optionsMenu.Open();
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, OsuConfigManager config, SkinManager skins)
        {
            RelativeSizeAxes = Axes.Both;

            const float mods_button_off = 57.6f * 1.6f;
            const float random_button_off = mods_button_off + 48 * 1.6f;
            const float options_button_off = random_button_off + 48 * 1.6f;
            const float user_pos_off = options_button_off + 48 * 2 * 1.6f;

            // Torii: skin vs bundled footer.
            // The "skin the song select footer" toggle. On: uses the active skin's songselect-bottom plus
            // the skin's selection-* glyphs. Off: a clean, even bar with the bundled classic buttons.
            bool useSkin = config.Get<bool>(OsuSetting.ToriiLegacyFooterUseSkin);

            // null = take the textures from the current skin so skinned footers show up, otherwise the
            // bundled classic one. The per-texture fallback to classic lives in LegacyFooterButton, and
            // every button has a FIXED 74x90 hit area that doesn't depend on the skin's texture. That way
            // a skin without selection-* (or with an oddly sized one) still gives a clickable button
            // instead of collapsing or flying off screen.
            ISkin? buttonSource = useSkin ? null : skins.DefaultClassicSkin;
            // When skinned we always want a footer background: the skin's songselect-bottom, else the
            // bundled classic one (the default with the blue border). This is the BACK layer; the user's
            // stats card goes on top, and the skin's selection-mode cosmetic on top of that.
            var bottomTexture = useSkin ? (skin.GetTexture(@"songselect-bottom") ?? skins.DefaultClassicSkin.GetTexture(@"songselect-bottom")) : null;

            // Some skins pack the footer into a giant "selection-mode" (the same texture as the skinnable
            // top decoration, where the BOTTOM PART is the footer design). We draw that bottom part as a
            // cosmetic ON TOP of the stats card (it covers performance/acc/lvl and has a transparent circle
            // for the avatar). Note: use the `skin` lookup directly, because
            // skins.CurrentSkin.Value.GetTexture returns null for legacy skin textures (no transformer).
            var modeTexture = useSkin ? skin.GetTexture(@"selection-mode") : null;
            bool skinFooterDecoration = modeTexture != null && Math.Max(modeTexture.DisplayWidth, modeTexture.DisplayHeight) > 90;

            bool showCleanBar = bottomTexture == null;

            InternalChildren = new Drawable[]
            {
                // Clean fallback bar, shown when there's no skin footer to draw (neither a
                // songselect-bottom nor a selection-mode decoration).
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = footer_bar_height,
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Colour = ColourInfo.GradientVertical(Color4.Black.Opacity(0.5f), Color4.Black.Opacity(0.85f)),
                    Alpha = showCleanBar ? 1 : 0,
                },
                // Footer background (BACK layer): the skin's songselect-bottom, else the bundled classic one.
                new Sprite
                {
                    Texture = bottomTexture,
                    RelativeSizeAxes = Axes.X,
                    Width = 1,
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Alpha = bottomTexture != null ? 1 : 0,
                },
                new LegacyBackButton
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Action = BackAction,
                },
                // Just the user's stats card. The buttons moved to their own layer (buttonsContainer),
                // which comes AFTER the selection-mode decoration so their hover glow isn't covered.
                components = new Container
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.Both,
                    X = buttons_pos_16_9,
                    Child = new LegacyFooterUser
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        X = user_pos_off + 3 * 1.6f,
                        Y = 2 * 1.6f,
                    },
                },
                // FRONT layer: the skin's selection-mode cosmetic, on top of the stats card (it covers
                // performance/acc/lvl, and the transparent circle lets the avatar show through). Same
                // texture and X as the top decoration so they line up; clipped to the footer band so its
                // upper design doesn't ride up over the song select chrome. The Y aligns it with the stats.
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Width = 1,
                    Height = 110,
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Masking = true,
                    Alpha = skinFooterDecoration ? 1 : 0,
                    Child = new Sprite
                    {
                        Texture = modeTexture,
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Position = new Vector2(buttons_pos_16_9, -2),
                    },
                },
                // The footer buttons (mode/mods/random/options), ON TOP of the selection-mode decoration so
                // their hover glow isn't covered. Same X as components, kept in sync in Update().
                buttonsContainer = new Container
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.Both,
                    X = buttons_pos_16_9,
                    Children = new Drawable[]
                    {
                        // The stable-style options popup, anchored above the options button.
                        optionsMenu = new OsuMenu(Direction.Vertical)
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Position = new Vector2(options_button_off, -(90 + 6)),
                            MaxHeight = 420,
                        },
                        new Container
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            AutoSizeAxes = Axes.Both,
                            Children = new LegacyFooterButton[]
                            {
                                new LegacyRulesetFooterButton { TextureSource = buttonSource, SuppressBaseGlyph = skinFooterDecoration },
                                new LegacyFooterButton("mods") { X = mods_button_off, Action = ModsAction, TextureSource = buttonSource },
                                new LegacyFooterButton("random") { X = random_button_off, Action = RandomAction, TextureSource = buttonSource },
                                new LegacyFooterButton("options") { X = options_button_off, Action = OptionsAction ?? ShowOptions, TextureSource = buttonSource },
                            },
                        },
                    },
                },
                (logoTrackingContainer = new LogoTrackingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                }).WithChild(logoTrackingContainer.LogoFacade.With(f =>
                {
                    f.Anchor = Anchor.BottomRight;
                    f.Origin = Anchor.Centre;
                    // TODO: lazer positions the logo differently from stable, but aesthetically lazer's
                    // looks better. Having the logo move when switching between a lazer and a legacy skin
                    // would look bad. For reference, in stable the logo sits near Vector2(-70, -50).
                    f.Position = new Vector2(-76, -36);
                })),
            };
        }

        protected override void Update()
        {
            base.Update();

            bool isWidescreen = Precision.DefinitelyBigger(DrawWidth, 1024);
            components.X = isWidescreen ? buttons_pos_16_9 : buttons_pos_4_3;
            // The buttons (front layer, separate from components) follow the same X.
            buttonsContainer.X = components.X;
        }

        public void StartTrackingLogo(OsuLogo logo, float duration = 0, Easing easing = Easing.None)
        {
            logoTracking = logoTrackingContainer.StartTracking(logo, duration, easing);
        }

        public void StopTrackingLogo()
        {
            logoTracking?.Dispose();
            logoTracking = null;
        }
    }
}
