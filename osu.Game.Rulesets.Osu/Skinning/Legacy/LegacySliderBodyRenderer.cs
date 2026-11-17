// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osu.Framework.Imaging;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Legacy
{
    internal partial class LegacySliderBodyRenderer : Drawable
    {
        // Screen-space geometry is deliberately independent of this drawable's huge parent
        // bounds. This affects draw culling only; gameplay input remains on the original path.
        public override Quad ScreenSpaceDrawQuad => new Quad(viewport.Origin.X, viewport.Origin.Y,
            Math.Min(viewport.RequestedSize.X, viewportLimit.X), Math.Min(viewport.RequestedSize.Y, viewportLimit.Y));

        private readonly List<LegacySliderBallPath.Edge> edges = new List<LegacySliderBallPath.Edge>();
        private readonly LegacySliderBodyMesh.LineCache lineCache = new LegacySliderBodyMesh.LineCache();

        // Line lists handed to draw nodes. Snaking republishes the list every frame; recycling released
        // snapshots avoids a (frequently large object heap) array per frame. Only touched on the update thread.
        private readonly Stack<LineSnapshot> snapshotPool = new Stack<LineSnapshot>();
        private LineSnapshot? lineSnapshot;

        // The ridge mesh uses D32. FBO attachment disposal is deferred by the framework;
        // colour rasterisation is compared against the original D16 oracle separately.
        private readonly BufferedDrawNodeSharedData sharedData = new BufferedDrawNodeSharedData(TexturePixelFormat.R8G8B8A8Float, new[] { RenderBufferFormat.D32 });

        private readonly FrameState frameState = new FrameState();
        private IShader meshShader = null!, textureShader = null!;
        private MeshShaders? meshShaders;
        private IRenderer? renderer;
        private Texture? gradient;
        private LegacySliderBodyMesh.Viewport viewport;
        private Vector2 viewportLimit = new Vector2(32768);
        private float radius;
        private int revision;
        private Color4 accent, border;
        private SliderPath? path;
        private int pathVersion;
        private bool pathValid;
        private float gradientRadius;
        private float gradientBorderSize;
        private bool customGradient;
        private Vector2 pathOrigin;
        private osu.Game.Rulesets.Objects.Legacy.LegacySliderPath? originalPath;
        private Vector2 previousOffset;
        private float previousRatio, previousRadius;
        private double previousStart = -1, previousEnd = -1;

        #region Disposal

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            if (!isDisposing) return;

            if (gradient != null) renderer?.ScheduleDisposal(static texture => texture.Dispose(), gradient);
            meshShaders?.Dispose();
            sharedData.Dispose();
        }

        #endregion

        public bool Configure(SliderPath sliderPath, Vector2 origin, Vector2 displayOrigin, float ratio, float fieldRadius,
                              double start, double end, Color4 colour, Color4 borderColour, Vector2 limit,
                              Func<float, Color4>? sampleGradient = null, float borderSize = 1)
        {
            bool limitChanged = viewportLimit != limit;
            viewportLimit = limit;
            bool changed = path != sliderPath || pathVersion != sliderPath.Version.Value || pathOrigin != origin || !ReferenceEquals(originalPath, sliderPath.LegacyPath);

            if (changed)
            {
                path = sliderPath;
                pathVersion = sliderPath.Version.Value;
                pathOrigin = origin;
                originalPath = sliderPath.LegacyPath;
                pathValid = LegacySliderBallPath.Build(sliderPath, origin, edges);
                lineCache.SetSource(edges);
            }

            if (!pathValid || edges.Count == 0 || !IsLoaded || gradient == null) return false;

            if (changed || limitChanged || previousOffset != displayOrigin || previousRatio != ratio || previousRadius != fieldRadius || previousStart != start || previousEnd != end)
            {
                radius = fieldRadius * ratio;
                viewport = lineCache.Prepare(start, end, displayOrigin, ratio, radius, sliderPath.ExpectedDistance.Value);
                publishLines(lineCache.Lines);
                previousOffset = displayOrigin;
                previousRatio = ratio;
                previousRadius = fieldRadius;
                previousStart = start;
                previousEnd = end;
                revision++;
                Invalidate(Invalidation.DrawNode | Invalidation.MiscGeometry);
            }

            // The classic profile depends on screen radius (including display scale).
            // Skin-provided profiles retain their existing field-radius cache key.
            float currentGradientRadius = sampleGradient == null ? radius : fieldRadius;

            if (accent != colour || border != borderColour || gradientRadius != currentGradientRadius || gradientBorderSize != borderSize || customGradient != (sampleGradient != null))
            {
                gradientRadius = currentGradientRadius;
                gradientBorderSize = borderSize;
                customGradient = sampleGradient != null;
                accent = colour;
                border = borderColour;
                var raw = new Image<Rgba32>(LegacySliderBodyGradient.TEXTURE_WIDTH, 1);
                var classicGradient = sampleGradient == null ? new LegacySliderBodyGradient(colour, borderColour, currentGradientRadius) : default;

                for (int i = 0; i < LegacySliderBodyGradient.TEXTURE_WIDTH; i++)
                {
                    float x = (i + 0.5f) / LegacySliderBodyGradient.TEXTURE_WIDTH;

                    if (sampleGradient != null)
                    {
                        Color4 pixel = sampleGradient(x);
                        float alpha = Math.Min(x / 0.02f, 1);
                        raw[i, 0] = new Rgba32(pixel.R, pixel.G, pixel.B, pixel.A * alpha);
                        continue;
                    }

                    Color4 classicPixel = classicGradient.Sample(x);
                    raw[i, 0] = new Rgba32(classicPixel.R, classicPixel.G, classicPixel.B, classicPixel.A);
                }

                gradient.SetData(new TextureUpload(raw));
                revision++;
                Invalidate(Invalidation.DrawNode);
            }

            return viewport.RequestedSize.X > 0 && viewport.RequestedSize.Y > 0;
        }

        private void publishLines(List<LegacySliderBodyMesh.Edge> lines)
        {
            LineSnapshot? next = null;

            while (snapshotPool.TryPop(out var pooled))
            {
                if (pooled.Lines.Length < lines.Count) continue;

                next = pooled;
                break;
            }

            // Snaking in grows the list; leave headroom so it is not reallocated for every new line.
            next ??= new LineSnapshot(new LegacySliderBodyMesh.Edge[lines.Count + lines.Count / 2]);
            CollectionsMarshal.AsSpan(lines).CopyTo(next.Lines);
            next.Count = lines.Count;
            next.References = 1;

            releaseLines(lineSnapshot);
            lineSnapshot = next;
        }

        private void releaseLines(LineSnapshot? snapshot)
        {
            if (snapshot == null || --snapshot.References > 0) return;

            if (snapshotPool.Count < 4)
                snapshotPool.Push(snapshot);
        }

        protected override DrawNode CreateDrawNode()
        {
            return new BodyDrawNode(this);
        }

        /// <summary>
        /// An immutable (while referenced) copy of the line list. Draw nodes hold references from
        /// <see cref="DrawNode.ApplyState"/>, which runs on the update thread while the node is not being drawn.
        /// </summary>
        private sealed class LineSnapshot
        {
            public readonly LegacySliderBodyMesh.Edge[] Lines;
            public int Count;
            public int References;

            public LineSnapshot(LegacySliderBodyMesh.Edge[] lines)
            {
                Lines = lines;
            }
        }

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders, IRenderer renderer)
        {
            this.renderer = renderer;
            meshShaders = new MeshShaders(renderer, shaders);
            meshShader = meshShaders.Load("StableBody", "StableBody");
            textureShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
            gradient = new DisposableTexture(renderer.CreateTexture(LegacySliderBodyGradient.TEXTURE_WIDTH, 1, true));
        }

        private sealed class MeshShaders : ShaderManager
        {
            private readonly ShaderManager fallback;

            public MeshShaders(IRenderer renderer, ShaderManager fallback)
                : base(renderer, new osu.Framework.IO.Stores.ResourceStore<byte[]>())
            {
                this.fallback = fallback;
            }

            public override byte[]? GetRawData(string fileName)
            {
                string? source = fileName switch
                {
                    "sh_StableBody.vs" =>
                        "#include \"sh_Utils.h\"\nlayout(location=0) in highp vec3 m_Position;\nlayout(location=1) in lowp vec4 m_Colour;\nlayout(location=2) in highp vec2 m_TexCoord;\nlayout(location=0) out highp vec2 uv;\nvoid main(void)\n{\nuv=m_TexCoord;\ngl_Position=g_ProjMatrix*vec4(m_Position,1.0);\n}\n",
                    "sh_StableBody.fs" =>
                        "layout(location=0) in highp vec2 uv;\nlayout(set=0,binding=0) uniform texture2D tex;\nlayout(set=0,binding=1) uniform sampler smp;\nlayout(location=0) out vec4 colour;\nvoid main(void)\n{\ncolour=texture(sampler2D(tex,smp),uv);\n}\n",
                    _ => null
                };
                return source == null ? fallback.GetRawData(fileName) : System.Text.Encoding.UTF8.GetBytes(source);
            }
        }

        private sealed class FrameState
        {
            public int Revision = -1;
        }

        private class BodyDrawNode : DrawNode
        {
            private LegacySliderBodyRenderer source => (LegacySliderBodyRenderer)Source;
            private readonly BufferedDrawNodeSharedData sharedData;
            private readonly FrameState frameState;
            private IShader meshShader = null!, textureShader = null!;
            private Texture gradient = null!;
            private LineSnapshot? lines;
            private LegacySliderBodyMesh.LineFrame[] frames = Array.Empty<LegacySliderBodyMesh.LineFrame>();
            private float radius;
            private Vector2 viewportLimit;
            private IVertexBatch<TexturedVertex3D>? quadBatch, capBatch;
            private LegacySliderBodyMesh.Viewport viewport;
            private int revision = -1;

            public BodyDrawNode(LegacySliderBodyRenderer source)
                : base(source)
            {
                sharedData = source.sharedData;
                frameState = source.frameState;
            }

            #region Disposal

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                quadBatch?.Dispose();
                capBatch?.Dispose();
            }

            #endregion

            public override void ApplyState()
            {
                base.ApplyState();
                meshShader = source.meshShader;
                textureShader = source.textureShader;
                gradient = source.gradient!;
                viewport = source.viewport;
                if (revision == source.revision) return;

                if (!ReferenceEquals(lines, source.lineSnapshot))
                {
                    source.releaseLines(lines);
                    lines = source.lineSnapshot;
                    if (lines != null) lines.References++;
                }

                radius = source.radius;
                viewportLimit = source.viewportLimit;
                revision = source.revision;
            }

            protected override void Draw(IRenderer renderer)
            {
                base.Draw(renderer);
                if (lines == null || lines.Count == 0) return;

                var screen = renderer.Viewport;
                if (!sharedData.IsInitialised) sharedData.Initialise(renderer);
                var buffer = sharedData.MainBuffer;

                if (frameState.Revision != revision || buffer.Size != new Vector2(screen.Width, screen.Height))
                {
                    buffer.Size = new Vector2(screen.Width, screen.Height);
                    buffer.Bind();
                    // Stable projects in 32-bit floats onto a viewport clamped by the driver, and draws
                    // into the window upside down. Both decide whether lines far thinner than a pixel
                    // cover a pixel centre, so vertices are projected here rather than by the shader:
                    // the framebuffer then only receives window pixels, never a 32768+ viewport.
                    var projection = new LegacySliderBodyMesh.WindowProjection(viewport, viewportLimit);
                    float windowHeight = screen.Height;
                    renderer.PushViewport(new RectangleI(0, 0, screen.Width, screen.Height));
                    renderer.PushScissorState(false);
                    renderer.PushOrtho(new RectangleF(0, 0, screen.Width, screen.Height));
                    renderer.PushDepthInfo(new DepthInfo(true, true, BufferTestFunction.LessThanOrEqual));

                    try
                    {
                        renderer.Clear(new ClearInfo(Color4.Transparent));
                        renderer.SetBlend(BlendingParameters.None);
                        meshShader.Bind();

                        try
                        {
                            // Bind after the non-default shader, so texture-slot batching
                            // cannot redirect the gradient away from sampler zero.
                            renderer.BindTexture(gradient);
                            quadBatch ??= renderer.CreateQuadBatch<TexturedVertex3D>(4096, 10);
                            capBatch ??= renderer.CreateLinearBatch<TexturedVertex3D>(7200, 10, PrimitiveTopology.Triangles);
                            var lineSpan = new ReadOnlySpan<LegacySliderBodyMesh.Edge>(lines.Lines, 0, lines.Count);
                            if (frames.Length < lineSpan.Length)
                                frames = new LegacySliderBodyMesh.LineFrame[lineSpan.Length + lineSpan.Length / 2];
                            var frameSpan = frames.AsSpan(0, lineSpan.Length);
                            LegacySliderBodyMesh.ComputeFrames(lineSpan, frameSpan);

                            var quads = new WindowSink(quadBatch, projection, windowHeight);
                            LegacySliderBodyMesh.EmitQuads(lineSpan, frameSpan, radius, ref quads);
                            quadBatch.Draw();
                            var caps = new WindowSink(capBatch, projection, windowHeight);
                            LegacySliderBodyMesh.EmitCaps(lineSpan, frameSpan, radius, ref caps);
                            capBatch.Draw();
                        }
                        finally { meshShader.Unbind(); }
                    }
                    finally
                    {
                        renderer.PopDepthInfo();
                        renderer.PopOrtho();
                        renderer.PopScissorState();
                        renderer.PopViewport();
                        buffer.Unbind();
                    }

                    frameState.Revision = revision;
                }

                renderer.SetBlend(DrawColourInfo.Blending);
                textureShader.Bind();
                // The framebuffer holds stable's upside-down window image; flip it back, as the copy to
                // the body texture does there.
                Vector2 topLeft = viewport.Origin;
                Vector2 bottomRight = viewport.Origin + new Vector2(screen.Width, screen.Height);
                renderer.DrawFrameBuffer(buffer, new Quad(new Vector2(topLeft.X, bottomRight.Y), bottomRight, topLeft, new Vector2(bottomRight.X, topLeft.Y)), DrawColourInfo.Colour);
                textureShader.Unbind();
            }
        }

        /// <summary>
        /// Projects mesh vertices into stable's (upside-down) window and feeds them to a batch.
        /// </summary>
        internal readonly struct WindowSink : LegacySliderBodyMesh.IVertexSink
        {
            private readonly IVertexBatch<TexturedVertex3D>? batch;
            private readonly LegacySliderBodyMesh.WindowProjection projection;
            private readonly float windowHeight;

            public WindowSink(IVertexBatch<TexturedVertex3D>? batch, LegacySliderBodyMesh.WindowProjection projection, float windowHeight)
            {
                this.batch = batch;
                this.projection = projection;
                this.windowHeight = windowHeight;
            }

            public TexturedVertex3D Transform(TexturedVertex3D vertex)
            {
                Vector2 window = projection.ToWindow(vertex.Position);
                vertex.Position = new Vector3(window.X, windowHeight - window.Y, vertex.Position.Z);
                return vertex;
            }

            public void Add(in TexturedVertex3D vertex) => batch?.Add(vertex);
        }
    }
}
