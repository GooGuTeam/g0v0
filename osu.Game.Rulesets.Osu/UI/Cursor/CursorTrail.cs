// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

#nullable disable

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shaders.Types;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Visualisation;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Timing;
using osu.Game.Recording.Export;
using osuTK;
using osuTK.Graphics;
using osuTK.Graphics.ES30;

namespace osu.Game.Rulesets.Osu.UI.Cursor
{
    [DrawVisualiserHidden]
    public partial class CursorTrail : Drawable, IRequireHighFrequencyMousePosition
    {
        private const int max_sprites = 2048;

        /// <summary>
        /// An exponentiating factor to ease the trail fade.
        /// </summary>
        protected virtual float FadeExponent => 1.7f;

        /// <summary>
        /// The scale used on creation of a new trail part.
        /// </summary>
        public Vector2 NewPartScale { get; set; } = Vector2.One;

        /// <summary>
        /// The rotation (in degrees) to apply to trail parts when <see cref="AllowPartRotation"/> is <c>true</c>.
        /// </summary>
        public float PartRotation { get; set; }

        /// <summary>
        /// Whether to rotate trail parts based on the value of <see cref="PartRotation"/>.
        /// </summary>
        protected bool AllowPartRotation { get; set; }

        /// <summary>
        /// Whether to drop the trail whenever this drawable's transform to screen space changes (e.g. the UI scale being adjusted).
        /// Parts are stored in local space, so a changing transform would otherwise smear them and streak a trail from the stale position.
        /// </summary>
        public bool ClearOnTransformChange { get; set; }

        private Matrix3 lastDrawMatrix;

        private Vector2 cursorScale = Vector2.One;

        public Vector2 CursorScale
        {
            get => cursorScale;
            set
            {
                cursorScale = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        /// <summary>
        /// The trail part texture origin.
        /// </summary>
        protected Anchor TrailOrigin
        {
            get => trailOrigin;
            set
            {
                trailOrigin = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        private readonly TrailPart[] parts = new TrailPart[max_sprites];
        private Anchor trailOrigin = Anchor.Centre;
        private int currentIndex;
        private IShader shader;
        private double timeOffset;
        private float time;

        public CursorTrail()
        {
            // as we are currently very dependent on having a running clock, let's make our own clock for the time being.
            Clock = new FramedClock();

            RelativeSizeAxes = Axes.Both;

            for (int i = 0; i < max_sprites; i++)
            {
                // -1 signals that the part is unusable, and should not be drawn
                parts[i].InvalidationID = -1;
            }
        }

        [Resolved(canBeNull: true)]
        private ReplayExportPresentationClock presentationClock { get; set; }

        [BackgroundDependencyLoader]
        private void load(IRenderer renderer, ShaderManager shaders)
        {
            // Preserve real-time trails in live gameplay. Export must use output time even
            // when rendering faster than real time or playing a rate-adjusted beatmap.
            if (presentationClock != null)
                Clock = new FramedClock(presentationClock, processSource: false);

            texture ??= renderer.WhitePixel;
            shader = shaders.Load(@"CursorTrail", FragmentShaderDescriptor.TEXTURE);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            resetTime();
        }

        private Texture texture;

        public Texture Texture
        {
            get => texture;
            set
            {
                if (texture == value)
                    return;

                texture = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        /// <summary>
        /// The amount of time to fade the cursor trail pieces.
        /// </summary>
        protected virtual double FadeDuration => 300;

        public override bool IsPresent => true;

        protected override void Update()
        {
            base.Update();

            Invalidate(Invalidation.DrawNode);

            if (ClearOnTransformChange)
            {
                var drawMatrix = DrawInfo.Matrix;

                if (drawMatrix != lastDrawMatrix)
                {
                    lastDrawMatrix = drawMatrix;
                    clearParts();
                }
            }

            const int fade_clock_reset_threshold = 1000000;

            time = (float)((Time.Current - timeOffset) / FadeDuration);
            if (time > fade_clock_reset_threshold)
                resetTime();
        }

        private void clearParts()
        {
            for (int i = 0; i < parts.Length; i++)
                parts[i].InvalidationID = -1;

            lastPosition = null;
            resampler = new InputResampler();
        }

        private void resetTime()
        {
            for (int i = 0; i < parts.Length; ++i)
            {
                parts[i].Time -= time;

                if (parts[i].InvalidationID != -1)
                    ++parts[i].InvalidationID;
            }

            time = 0;
            timeOffset = Time.Current;
        }

        /// <summary>
        /// Whether to interpolate mouse movements and add trail pieces at intermediate points.
        /// </summary>
        protected virtual bool InterpolateMovements => true;

        protected virtual float IntervalMultiplier => 1.0f;
        protected virtual bool AvoidDrawingNearCursor => false;

        private Vector2? lastPosition;
        private InputResampler resampler = new InputResampler();

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true;

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            AddTrail(e.ScreenSpaceMousePosition);
            return base.OnMouseMove(e);
        }

        protected void AddTrail(Vector2 position)
        {
            position = ToLocalSpace(position);

            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
            {
                clearParts();
                return;
            }

            if (InterpolateMovements)
            {
                if (!lastPosition.HasValue)
                {
                    lastPosition = position;
                    resampler.AddPosition(lastPosition.Value);
                    return;
                }

                foreach (Vector2 pos2 in resampler.AddPosition(position))
                {
                    Trace.Assert(lastPosition.HasValue);

                    Vector2 pos1 = lastPosition.Value;
                    Vector2 diff = pos2 - pos1;
                    float distance = diff.Length;
                    Vector2 direction = diff / distance;

                    float interval = Texture.DisplayWidth * CursorScale.X / 2.5f * IntervalMultiplier;
                    int count = GetInterpolationCount(distance, interval, AvoidDrawingNearCursor, out double firstDistance);
                    if (count == 0 && (!float.IsFinite(distance) || !float.IsFinite(interval) || interval <= 0))
                    {
                        lastPosition = pos2;
                        continue;
                    }

                    // Only the last max_sprites pieces survive the ring buffer anyway. Iterate with
                    // an integer counter: float distance accumulation can stop advancing on Aspire
                    // jumps once its ULP exceeds the interval, permanently blocking the update thread.
                    for (int i = 0; i < count; i++)
                    {
                        lastPosition = pos1 + direction * (float)(firstDistance + (double)i * interval);
                        addPart(lastPosition.Value);
                    }
                }
            }
            else
            {
                lastPosition = position;
                addPart(lastPosition.Value);
            }
        }

        internal static int GetInterpolationCount(float distance, float interval, bool avoidNearCursor, out double firstDistance)
        {
            firstDistance = 0;
            if (!float.IsFinite(distance) || !float.IsFinite(interval) || interval <= 0 || distance <= 0)
                return 0;

            double stopAt = (double)distance - (avoidNearCursor ? interval : 0);
            double total = Math.Max(0, Math.Ceiling(stopAt / interval) - 1);
            int count = (int)Math.Min(max_sprites, total);
            firstDistance = Math.Max(1, total - count + 1) * interval;
            return count;
        }

        private void addPart(Vector2 localSpacePosition)
        {
            parts[currentIndex].Position = localSpacePosition;
            parts[currentIndex].Time = time + 1;
            parts[currentIndex].Scale = NewPartScale;
            ++parts[currentIndex].InvalidationID;

            currentIndex = (currentIndex + 1) % max_sprites;
        }

        protected override DrawNode CreateDrawNode() => new TrailDrawNode(this);

        private struct TrailPart
        {
            public Vector2 Position;
            public float Time;
            public Vector2 Scale;
            public long InvalidationID;
        }

        private class TrailDrawNode : DrawNode
        {
            protected new CursorTrail Source => (CursorTrail)base.Source;

            private IShader shader;
            private Texture texture;

            private float time;
            private float fadeExponent;
            private float angle;
            private Vector2 cursorScale;

            private readonly TrailPart[] parts = new TrailPart[max_sprites];
            private Vector2 originPosition;

            private IVertexBatch<TexturedTrailVertex> vertexBatch;

            public TrailDrawNode(CursorTrail source)
                : base(source)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();

                shader = Source.shader;
                texture = Source.texture;
                time = Source.time;
                fadeExponent = Source.FadeExponent;
                angle = Source.AllowPartRotation ? float.DegreesToRadians(Source.PartRotation) : 0;
                cursorScale = Source.cursorScale;

                originPosition = Vector2.Zero;

                if (Source.TrailOrigin.HasFlag(Anchor.x1))
                    originPosition.X = 0.5f;
                else if (Source.TrailOrigin.HasFlag(Anchor.x2))
                    originPosition.X = 1f;

                if (Source.TrailOrigin.HasFlag(Anchor.y1))
                    originPosition.Y = 0.5f;
                else if (Source.TrailOrigin.HasFlag(Anchor.y2))
                    originPosition.Y = 1f;

                Source.parts.CopyTo(parts, 0);
            }

            private IUniformBuffer<CursorTrailParameters> cursorTrailParameters;

            protected override void Draw(IRenderer renderer)
            {
                base.Draw(renderer);

                vertexBatch ??= renderer.CreateQuadBatch<TexturedTrailVertex>(max_sprites, 1);

                cursorTrailParameters ??= renderer.CreateUniformBuffer<CursorTrailParameters>();
                cursorTrailParameters.Data = cursorTrailParameters.Data with
                {
                    FadeClock = time,
                    FadeExponent = fadeExponent
                };

                shader.Bind();
                shader.BindUniformBlock("m_CursorTrailParameters", cursorTrailParameters);

                texture.Bind();

                RectangleF textureRect = texture.GetTextureRect();

                renderer.PushLocalMatrix(DrawInfo.Matrix);

                float sin = MathF.Sin(angle);
                float cos = MathF.Cos(angle);

                foreach (var part in parts)
                {
                    if (part.InvalidationID == -1)
                        continue;

                    if (time - part.Time >= 1)
                        continue;

                    vertexBatch.Add(new TexturedTrailVertex
                    {
                        Position = rotateAround(
                            new Vector2(
                                part.Position.X - texture.DisplayWidth * originPosition.X * part.Scale.X * cursorScale.X,
                                part.Position.Y + texture.DisplayHeight * (1 - originPosition.Y) * part.Scale.Y * cursorScale.Y),
                            part.Position, sin, cos),
                        TexturePosition = textureRect.BottomLeft,
                        TextureRect = new Vector4(0, 0, 1, 1),
                        Colour = DrawColourInfo.Colour.BottomLeft.Linear,
                        Time = part.Time
                    });

                    vertexBatch.Add(new TexturedTrailVertex
                    {
                        Position = rotateAround(
                            new Vector2(
                                part.Position.X + texture.DisplayWidth * (1 - originPosition.X) * part.Scale.X * cursorScale.X,
                                part.Position.Y + texture.DisplayHeight * (1 - originPosition.Y) * part.Scale.Y * cursorScale.Y),
                            part.Position, sin, cos),
                        TexturePosition = textureRect.BottomRight,
                        TextureRect = new Vector4(0, 0, 1, 1),
                        Colour = DrawColourInfo.Colour.BottomRight.Linear,
                        Time = part.Time
                    });

                    vertexBatch.Add(new TexturedTrailVertex
                    {
                        Position = rotateAround(
                            new Vector2(
                                part.Position.X + texture.DisplayWidth * (1 - originPosition.X) * part.Scale.X * cursorScale.X,
                                part.Position.Y - texture.DisplayHeight * originPosition.Y * part.Scale.Y * cursorScale.Y),
                            part.Position, sin, cos),
                        TexturePosition = textureRect.TopRight,
                        TextureRect = new Vector4(0, 0, 1, 1),
                        Colour = DrawColourInfo.Colour.TopRight.Linear,
                        Time = part.Time
                    });

                    vertexBatch.Add(new TexturedTrailVertex
                    {
                        Position = rotateAround(
                            new Vector2(
                                part.Position.X - texture.DisplayWidth * originPosition.X * part.Scale.X * cursorScale.X,
                                part.Position.Y - texture.DisplayHeight * originPosition.Y * part.Scale.Y * cursorScale.Y),
                            part.Position, sin, cos),
                        TexturePosition = textureRect.TopLeft,
                        TextureRect = new Vector4(0, 0, 1, 1),
                        Colour = DrawColourInfo.Colour.TopLeft.Linear,
                        Time = part.Time
                    });
                }

                renderer.PopLocalMatrix();

                vertexBatch.Draw();
                shader.Unbind();
            }

            private static Vector2 rotateAround(Vector2 input, Vector2 origin, float sin, float cos)
            {
                float xTranslated = input.X - origin.X;
                float yTranslated = input.Y - origin.Y;

                return new Vector2(xTranslated * cos - yTranslated * sin, xTranslated * sin + yTranslated * cos) + origin;
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                vertexBatch?.Dispose();
                cursorTrailParameters?.Dispose();
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            private record struct CursorTrailParameters
            {
                public UniformFloat FadeClock;
                public UniformFloat FadeExponent;
                private readonly UniformPadding8 pad1;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TexturedTrailVertex : IEquatable<TexturedTrailVertex>, IVertex
        {
            [VertexMember(2, VertexAttribPointerType.Float)]
            public Vector2 Position;

            [VertexMember(4, VertexAttribPointerType.Float)]
            public Color4 Colour;

            [VertexMember(2, VertexAttribPointerType.Float)]
            public Vector2 TexturePosition;

            [VertexMember(4, VertexAttribPointerType.Float)]
            public Vector4 TextureRect;

            [VertexMember(1, VertexAttribPointerType.Float)]
            public float Time;

            public bool Equals(TexturedTrailVertex other)
            {
                return Position.Equals(other.Position)
                       && TexturePosition.Equals(other.TexturePosition)
                       && Colour.Equals(other.Colour)
                       && Time.Equals(other.Time);
            }
        }
    }
}
