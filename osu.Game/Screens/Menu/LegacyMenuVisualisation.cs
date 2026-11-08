// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Menu
{
    /// <summary>
    /// osu!stable's main menu spectrum (<c>MenuVisualisation</c>): 1024 additive <c>menu-vis</c> bars wound four times around the cookie,
    /// starting <see cref="Radius"/> units from its centre. Each bar takes three times the level of one 2048-point FFT bin, decays by 5% per
    /// 60fps frame, and the bin each bar reads shifts by 50 every 10ms, so the spectrum appears to rotate.
    /// </summary>
    /// <remarks>
    /// Lengths are in stable's 640x480 units (a texture pixel at 1x is 1 / 1.6 units). Lazer exposes a 512-point FFT (256 bins),
    /// so stable bin <c>k</c> reads lazer bin <c>k / 4</c>, which covers the same frequency.
    /// </remarks>
    public partial class LegacyMenuVisualisation : Drawable
    {
        private const int bar_count = 1024;

        /// <summary>
        /// Bars span <c>0.4 + i / 1024 * Overshoot / 2</c> turns, with stable's overshoot of 8.
        /// </summary>
        private const float overshoot = 8;

        private const float max_alpha = 0.4f;

        private const double column_interval = 10;

        private const int column_shift = 50;

        /// <summary>
        /// Stable's fallback <c>MenuGlow</c> colour.
        /// </summary>
        public static readonly Color4 DEFAULT_COLOUR = new Color4(128, 128, 160, 255);

        /// <summary>
        /// The distance from the centre at which bars start, in stable units. Driven by the cookie every frame.
        /// </summary>
        public float Radius { get; set; } = 150;

        /// <summary>
        /// Stable's <c>vis.Alpha</c>: the overall opacity multiplier, driven by the cookie's position and kiai.
        /// </summary>
        public float VisualisationAlpha { get; set; } = 1;

        private readonly float[] data = new float[bar_count];
        private readonly float[] scales = new float[bar_count];
        private readonly float[] alphas = new float[bar_count];
        private readonly Vector2[] directions = new Vector2[bar_count];

        private double columnCurrentMilliseconds;
        private double startOffset;

        private IShader shader = null!;
        private Texture? texture;

        [Resolved]
        private IBeatSyncProvider beatSyncProvider { get; set; } = null!;

        public LegacyMenuVisualisation()
        {
            Blending = BlendingParameters.Additive;

            for (int i = 0; i < bar_count; i++)
            {
                float angle = MathF.PI * 2 * (0.4f + i / (float)bar_count * (overshoot * 0.5f));
                directions[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            }
        }

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders, ISkinSource skin, SkinManager skins)
        {
            shader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
            texture = skin.GetStableTexture(@"menu-vis") ?? skins.DefaultClassicSkin.GetStableTexture(@"menu-vis");

            var glow = skin.GetConfig<GlobalSkinColours, Color4>(GlobalSkinColours.MenuGlow)?.Value;
            Colour = glow ?? DEFAULT_COLOUR;
        }

        protected override void Update()
        {
            base.Update();

            updateData();

            double elapsed = Time.Elapsed;

            // stable skips a frame that took over a second.
            if (elapsed <= 1000)
                updateVisualisation(elapsed);

            Invalidate(Invalidation.DrawNode);
        }

        /// <summary>
        /// Stable's <c>Visualisation.Update</c>: read the spectrum while the track plays, otherwise let it decay by 5% per 60fps frame.
        /// </summary>
        private void updateData()
        {
            var track = beatSyncProvider.Clock;

            if (track?.IsRunning == true)
            {
                ReadOnlySpan<float> bins = beatSyncProvider.CurrentAmplitudes.FrequencyAmplitudes.Span;

                for (int i = 0; i < bar_count; i++)
                {
                    int bin = i / 4;
                    data[i] = bin < bins.Length ? bins[bin] : 0;
                }

                return;
            }

            float decay = (float)Math.Pow(0.95, Time.Elapsed / (1000 / 60.0));

            for (int i = 0; i < bar_count; i++)
                data[i] = data[i] < 0.01f ? 0 : data[i] * decay;
        }

        private void updateVisualisation(double elapsed)
        {
            while (elapsed > 0)
            {
                columnCurrentMilliseconds += elapsed;
                double overflow = Math.Max(0, columnCurrentMilliseconds - column_interval);
                elapsed -= overflow;

                updateColumn(elapsed);

                if (overflow == 0)
                    break;

                columnCurrentMilliseconds = 0;
                startOffset = (startOffset + column_shift) % bar_count;
                elapsed = overflow;
            }
        }

        private void updateColumn(double elapsed)
        {
            double decay = Math.Pow(0.95, elapsed / (1000 / 60.0));

            for (int i = bar_count - 1; i >= 0; i--)
            {
                int bar = bar_count - i - 1;
                float scale = (float)(Math.Max(scales[bar], data[(int)((i + startOffset) % bar_count)] * 3) * decay);

                if (scale < 0.01f)
                {
                    scales[bar] = 0;
                    alphas[bar] = 0;
                }
                else
                {
                    scales[bar] = scale;
                    alphas[bar] = Math.Max(0, VisualisationAlpha * max_alpha * Math.Min(1, (scale - 0.04f) / 0.08f));
                }
            }
        }

        protected override DrawNode CreateDrawNode() => new LegacyVisualisationDrawNode(this);

        private class LegacyVisualisationDrawNode : DrawNode
        {
            protected new LegacyMenuVisualisation Source => (LegacyMenuVisualisation)base.Source;

            private IShader shader = null!;
            private Texture? texture;
            private float radius;
            private Vector2 barSize;

            private readonly float[] scales = new float[bar_count];
            private readonly float[] alphas = new float[bar_count];

            private IVertexBatch<TexturedVertex2D>? vertexBatch;

            public LegacyVisualisationDrawNode(LegacyMenuVisualisation source)
                : base(source)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();

                shader = Source.shader;
                texture = Source.texture;
                radius = Source.Radius;

                // the sprite's size in stable units: 1x texture pixels / 1.6, half height as stable draws it (VectorScale.Y = 0.5).
                barSize = texture == null
                    ? new Vector2(160, 5)
                    : new Vector2(texture.DisplayWidth, texture.DisplayHeight * 0.5f) / LegacySkin.STABLE_MAGIC_SCALE_FACTOR;

                Source.scales.AsSpan().CopyTo(scales);
                Source.alphas.AsSpan().CopyTo(alphas);
            }

            protected override void Draw(IRenderer renderer)
            {
                base.Draw(renderer);

                if (texture == null)
                    return;

                vertexBatch ??= renderer.CreateQuadBatch<TexturedVertex2D>(bar_count, 2);

                shader.Bind();

                Vector2 inflation = DrawInfo.MatrixInverse.ExtractScale().Xy;

                for (int i = 0; i < bar_count; i++)
                {
                    if (alphas[i] <= 0)
                        continue;

                    Vector2 direction = Source.directions[i];
                    Vector2 normal = new Vector2(-direction.Y, direction.X) * (barSize.Y / 2);
                    Vector2 start = direction * radius;
                    Vector2 length = direction * (barSize.X * scales[i]);

                    var quad = new Quad(
                        Vector2Extensions.Transform(start - normal, DrawInfo.Matrix),
                        Vector2Extensions.Transform(start - normal + length, DrawInfo.Matrix),
                        Vector2Extensions.Transform(start + normal, DrawInfo.Matrix),
                        Vector2Extensions.Transform(start + normal + length, DrawInfo.Matrix));

                    ColourInfo colour = DrawColourInfo.Colour;
                    colour.ApplyChild(new Color4(1, 1, 1, alphas[i]));

                    // inflate by a pixel in each direction, relative to the bar's size, so its edges stay smooth.
                    renderer.DrawQuad(texture, quad, colour, null, vertexBatch.AddAction, Vector2.Divide(inflation, new Vector2(barSize.X * scales[i], barSize.Y)));
                }

                shader.Unbind();
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                vertexBatch?.Dispose();
            }
        }
    }
}
