// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace osu.Game.Screens
{
    /// <summary>
    /// A layer above the screen footer, for screen content that should draw in front of it (the legacy song select's skinnable HUD).
    /// Screens add proxies here while they are current, so the content keeps its place, transitions and input in the screen.
    /// </summary>
    public partial class ScreenFrontLayer : Container
    {
        /// <summary>
        /// Whether the osu! logo should stay below this layer while the footer holds it in front (the legacy song select keeps the cookie
        /// under its HUD until it is hovered, as osu!stable shows its hover copy above everything only then).
        /// </summary>
        public readonly BindableBool HoldLogoBelow = new BindableBool();

        public ScreenFrontLayer()
        {
            RelativeSizeAxes = Axes.Both;
        }

        /// <summary>
        /// Moves <paramref name="drawable"/> (a proxy) into this layer, or back into <paramref name="home"/>, if it is not already there.
        /// </summary>
        public void Hold(Drawable drawable, bool inFront, Container home)
        {
            Container target = inFront ? this : home;

            if (drawable.Parent == target)
                return;

            // only a loaded layer takes the proxy (one outside the game's hierarchy would never draw it).
            if (inFront && !IsLoaded)
                return;

            (drawable.Parent as Container)?.Remove(drawable, false);
            target.Add(drawable);
        }

        /// <summary>
        /// Removes <paramref name="drawable"/> if a disposed screen left it here. Disposal may run off the update thread, so this is scheduled.
        /// </summary>
        public void Release(Drawable drawable) => Schedule(() =>
        {
            if (drawable.Parent == this)
                Remove(drawable, false);
        });

        /// <summary>
        /// Draws a set of a component's parts in this layer, each at its own depth (lower is in front). A proxy hides its original, so the
        /// parts only move once the layer is loaded and drawing; until then they stay where they are.
        /// </summary>
        public sealed class ProxySet
        {
            private readonly (Drawable original, float depth)[] parts;
            private Drawable[]? proxies;

            public ProxySet(params (Drawable original, float depth)[] parts)
            {
                this.parts = parts;
            }

            /// <summary>
            /// Call every frame from the owner's update.
            /// </summary>
            public void Update(ScreenFrontLayer? layer)
            {
                if (proxies != null || layer?.IsLoaded != true)
                    return;

                proxies = new Drawable[parts.Length];

                for (int i = 0; i < parts.Length; i++)
                {
                    var proxy = parts[i].original.CreateProxy();
                    proxy.Depth = parts[i].depth;
                    layer.Add(proxies[i] = proxy);
                }
            }

            /// <summary>
            /// Call from the owner's disposal.
            /// </summary>
            public void Release(ScreenFrontLayer? layer)
            {
                if (proxies == null || layer == null)
                    return;

                foreach (var proxy in proxies)
                    layer.Release(proxy);
            }
        }

        /// <summary>
        /// Depths in this layer, after osu!stable's song select (higher depth there is in front; here lower is).
        /// </summary>
        public static class Depths
        {
            /// <summary>
            /// The mode menu (stable's spriteManagerLow): above the carousel and the scores, below all the song select chrome, so artwork a skin
            /// spreads across the menu stays behind the beatmap details, the top bar and the footer.
            /// </summary>
            public const float MODE_MENU = 2;

            /// <summary>The song select's top bar and beatmap details (spriteManagerHigh, 0.7-0.81), above the mode menu.</summary>
            public const float SONG_SELECT_TOP = 1.75f;

            /// <summary>
            /// The skin's <c>selection-mode</c> (0.95). Stable adds it before the mods, random and options buttons at the same depth, so they draw above it.
            /// </summary>
            public const float SELECTION_MODE = 1.5f;

            /// <summary>The footer buttons (0.95-0.96) and the back button (0.9-0.92).</summary>
            public const float FOREGROUND = 1;

            /// <summary>The dropdowns (0.98, 1) and the small beatmap stat lines (1).</summary>
            public const float TOP_CONTROLS = 0;

            /// <summary>Skin editor components drawn above the song select chrome.</summary>
            public const float HUD = -1;
        }
    }
}
