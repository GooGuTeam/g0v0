// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Legacy
{
    public partial class LegacySliderBall : CompositeDrawable
    {
        public Color4 BallColour => ballAnimation.Colour;
        private readonly ISkin skin;

        private readonly LegacySliderBallAnimation legacyAnimation = new LegacySliderBallAnimation();
        private readonly List<int> visualTicks = new List<int>();

        private readonly IBindable<Color4> accentColour = new Bindable<Color4>();

        private Sprite layerNd = null!;
        private Sprite layerSpec = null!;

        private TextureAnimation ballAnimation = null!;
        private Texture[] ballTextures = null!;
        private Slider? currentSlider;
        private int pathVersion = -1;
        private int samplerRevision = -1;
        private double currentVelocity, currentTickDistance, currentStart;
        private int currentSpans;
        private bool timingValid;
        private bool usingLegacyAnimation;
        private float lastVisualRotation;
        private bool flipOnReverse;

        [Resolved(canBeNull: true)]
        private DrawableHitObject? parentObject { get; set; }

        public LegacySliderBall(ISkin skin)
        {
            this.skin = skin;

            AutoSizeAxes = Axes.Both;
        }

        #region Disposal

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (parentObject != null)
            {
                parentObject.HitObjectApplied -= onHitObjectApplied;
                parentObject.ApplyCustomUpdateState -= updateStateTransforms;
            }
        }

        #endregion

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (parentObject != null)
            {
                parentObject.ApplyCustomUpdateState += updateStateTransforms;
                updateStateTransforms(parentObject, parentObject.State.Value);

                if (skin.GetConfig<SkinConfiguration.LegacySetting, bool>(SkinConfiguration.LegacySetting.AllowSliderBallTint)?.Value == true)
                {
                    accentColour.BindTo(parentObject.AccentColour);
                    accentColour.BindValueChanged(a => ballAnimation.Colour = a.NewValue, true);
                }
            }
        }

        protected override void Update()
        {
            base.Update();
            if (parentObject is not DrawableSlider drawable || ballAnimation.FrameCount == 0) return;

            Slider slider = drawable.HitObject;
            var sampler = drawable.Ball.LegacyPosition;

            if (!sampler.TryGetPosition(slider.Path, slider.StartTime, slider.Velocity, slider.SpanCount(), Time.Current, out _, slider.Position))
            {
                restoreFallbackAnimation();
                return;
            }

            if (currentSlider != slider || samplerRevision != sampler.Revision || pathVersion != slider.Path.Version.Value || currentVelocity != slider.Velocity
                || currentTickDistance != slider.TickDistance
                || currentStart != slider.StartTime || currentSpans != slider.SpanCount())
            {
                currentSlider = slider;
                samplerRevision = sampler.Revision;
                pathVersion = slider.Path.Version.Value;
                currentVelocity = slider.Velocity;
                currentTickDistance = slider.TickDistance;
                currentStart = slider.StartTime;
                currentSpans = slider.SpanCount();
                legacyAnimation.Reset();
                lastVisualRotation = 0;
                timingValid = LegacySliderBallTiming.Build(sampler.Edges, (int)slider.StartTime, sampler.EndTime, slider.SpanCount(), slider.Velocity, slider.TickDistance,
                    slider.Path.ExpectedDistance.Value ?? slider.Path.Distance, visualTicks);
            }

            if (!timingValid)
            {
                restoreFallbackAnimation();
                return;
            }

            double delay = Math.Max(0.15 / slider.Velocity * LegacySkinExtensions.SIXTY_FRAME_TIME, LegacySkinExtensions.SIXTY_FRAME_TIME);
            int frame = legacyAnimation.Update((int)Math.Round(Time.Current), (int)slider.StartTime, sampler.EndTime, slider.SpanCount(), visualTicks, ballAnimation.FrameCount, delay,
                drawable.Tracking.Value);
            usingLegacyAnimation = true;
            ballAnimation.IsPlaying = false;
            ballAnimation.GotoFrame(frame);
            bool flip = flipOnReverse;
            float initialAngle = sampler.Edges.Count > 0 ? (float)Math.Atan2(sampler.Edges[0].From.Y - sampler.Edges[0].To.Y, sampler.Edges[0].From.X - sampler.Edges[0].To.X) : 0;
            bool vertical = sampler.EdgeCount > 2 && initialAngle >= -Math.PI / 2 && initialAngle <= Math.PI / 2;
            ballAnimation.Scale = new Vector2(legacyAnimation.Reverse && flip ? -1 : 1, vertical ? -1 : 1);
            if (sampler.HasActiveMovement)
                lastVisualRotation = LegacySliderBallAnimation.Rotation(sampler.Direction, legacyAnimation.Reverse);
            ballAnimation.Rotation = lastVisualRotation - drawable.Ball.LegacyRotation;
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            // The parent's final layout pass may update its rotation after this child.
            // Reconcile the local correction each frame without re-sampling animation state.
            if (parentObject is DrawableSlider drawable && usingLegacyAnimation)
                ballAnimation.Rotation = lastVisualRotation - drawable.Ball.LegacyRotation;

            //undo rotation on layers which should not be rotated.
            float appliedRotation = Parent!.Rotation;

            layerNd.Rotation = -appliedRotation;
            layerSpec.Rotation = -appliedRotation;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Vector2 maxSize = OsuLegacySkinTransformer.MAX_FOLLOW_CIRCLE_AREA_SIZE;

            // Stable's legacy alias is read second and overrides SliderBallFlip when present.
            flipOnReverse = skin.GetConfig<OsuSkinConfiguration, bool>(OsuSkinConfiguration.SliderBallDontRotate)?.Value
                            ?? skin.GetConfig<OsuSkinConfiguration, bool>(OsuSkinConfiguration.SliderBallFlip)?.Value ?? false;
            var ballColour = skin.GetConfig<OsuSkinColour, Color4>(OsuSkinColour.SliderBall)?.Value ?? Color4.White;
            ballTextures = skin.GetTextures("sliderb", default, default, true, "", maxSize, out _);

            InternalChildren = new Drawable[]
            {
                layerNd = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Texture = skin.GetTexture("sliderb-nd")?.WithMaximumSize(maxSize),
                    Colour = new Color4(5, 5, 5, 255)
                },
                ballAnimation = new LegacySkinExtensions.SkinnableTextureAnimation
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Colour = ballColour
                },
                layerSpec = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Texture = skin.GetTexture("sliderb-spec")?.WithMaximumSize(maxSize),
                    Blending = BlendingParameters.Additive
                }
            };

            if (parentObject != null)
                parentObject.HitObjectApplied += onHitObjectApplied;

            onHitObjectApplied(parentObject);
        }

        private void restoreFallbackAnimation()
        {
            usingLegacyAnimation = false;
            ballAnimation.IsPlaying = true;
            ballAnimation.Scale = Vector2.One;
            ballAnimation.Rotation = 0;
        }

        private void onHitObjectApplied(DrawableHitObject? drawableObject = null)
        {
            currentSlider = null;
            usingLegacyAnimation = false;
            lastVisualRotation = 0;
            legacyAnimation.Reset();
            double frameDelay;

            if (drawableObject?.HitObject != null)
            {
                DrawableSlider drawableSlider = (DrawableSlider)drawableObject;

                frameDelay = Math.Max(
                    0.15 / drawableSlider.HitObject.Velocity * LegacySkinExtensions.SIXTY_FRAME_TIME,
                    LegacySkinExtensions.SIXTY_FRAME_TIME);
            }
            else
                frameDelay = LegacySkinExtensions.SIXTY_FRAME_TIME;

            ballAnimation.ClearFrames();
            foreach (var texture in ballTextures)
                ballAnimation.AddFrame(texture, frameDelay);
        }

        private void updateStateTransforms(DrawableHitObject drawableObject, ArmedState _)
        {
            // Gets called by slider ticks, tails, etc., leading to duplicated
            // animations which in this case have no visual impact (due to
            // instant fade) but may negatively affect performance
            if (drawableObject is not DrawableSlider)
                return;

            using (BeginAbsoluteSequence(drawableObject.StateUpdateTime))
                this.FadeIn();

            using (BeginAbsoluteSequence(drawableObject.HitStateUpdateTime))
                this.FadeOut();
        }
    }
}
