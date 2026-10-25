// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// Legacy (osu!stable style) text with a soft drop shadow plus a thin, near-opaque outline around
    /// the glyphs, so it reads like stable's UI text over light or busy backgrounds. The stroke is
    /// stamped in 8 directions with a sub-pixel offset (constant, NOT scaled by font size) so it stays
    /// tight and crisp without emboldening the text; the real copy (in front) carries a subtle shadow on
    /// top. All copies share the same bitmap font atlas so they land in the same draw call. A near
    /// drop-in replacement for <see cref="OsuSpriteText"/>: set Text, Font, Colour, Anchor/Origin as usual.
    /// </summary>
    public partial class StrokedLegacyText : CompositeDrawable
    {
        // Unit offsets in 8 directions (sides + corners) so every glyph gets the full ring.
        private static readonly Vector2[] directions =
        {
            new Vector2(-1, -1), new Vector2(0, -1), new Vector2(1, -1),
            new Vector2(-1, 0), new Vector2(1, 0),
            new Vector2(-1, 1), new Vector2(0, 1), new Vector2(1, 1),
        };

        private readonly OsuSpriteText main;
        private readonly OsuSpriteText[] strokes = new OsuSpriteText[directions.Length];

        /// <summary>Absolute stroke thickness in local px. Thin: a tight outline rather than a halo.</summary>
        public float StrokeWidth { get; init; } = 0.35f;

        public LocalisableString Text
        {
            set
            {
                main.Text = value;
                foreach (var s in strokes)
                    s.Text = value;
            }
        }

        public FontUsage Font
        {
            set
            {
                main.Font = value;
                foreach (var s in strokes)
                    s.Font = value;
            }
        }

        /// <summary>Colour of the front text. White by default.</summary>
        public new Color4 Colour
        {
            set => main.Colour = value;
        }

        /// <summary>Colour of the stroke ring. Near-opaque black by default, as in stable.</summary>
        public Color4 StrokeColour
        {
            set
            {
                foreach (var s in strokes)
                    s.Colour = value;
            }
        }

        public StrokedLegacyText()
        {
            AutoSizeAxes = Axes.Both;

            var children = new OsuSpriteText[directions.Length + 1];

            for (int i = 0; i < directions.Length; i++)
            {
                children[i] = strokes[i] = new OsuSpriteText
                {
                    Shadow = false,
                    // Thin, soft outline: the edge is visible without looking black/heavy.
                    Colour = new Color4(0f, 0f, 0f, 0.5f),
                    Position = directions[i],
                };
            }

            children[directions.Length] = main = new OsuSpriteText
            {
                // Barely any drop shadow, softer than before so it doesn't blacken the text.
                Shadow = true,
                ShadowColour = new Color4(0f, 0f, 0f, 0.3f),
                ShadowOffset = new Vector2(0f, 0.08f),
                Colour = Color4.White,
            };

            InternalChildren = children;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Apply the final stroke width now that the StrokeWidth init property has been set.
            for (int i = 0; i < directions.Length; i++)
                strokes[i].Position = directions[i] * StrokeWidth;
        }
    }
}
