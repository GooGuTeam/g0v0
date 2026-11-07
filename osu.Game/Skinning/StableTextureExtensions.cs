// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Textures;

namespace osu.Game.Skinning
{
    public static class StableTextureExtensions
    {
        /// <summary>
        /// Looks up a texture to draw as osu!stable does. Stable clamps every sprite that does not repeat to its texture's edge pixels
        /// (<c>pSprite.Draw</c>), so a stretched or scaled skin element keeps its edge colour to its border. The default lookup samples
        /// transparent padding past the edges instead, which fades stretched textures out towards their ends.
        /// </summary>
        public static Texture? GetStableTexture(this ISkin skin, string componentName)
        {
            var texture = skin.GetTexture(componentName, WrapMode.ClampToEdge, WrapMode.ClampToEdge);

            // The packaged renderer can mistake clamped 1x1 textures for its untextured white quad.
            // Repeating a single texel produces the same colour as clamping to its edge.
            if (texture is { Width: 1, Height: 1 })
                return skin.GetTexture(componentName, WrapMode.Repeat, WrapMode.Repeat);

            return texture;
        }
    }
}
