// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Utils;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Legacy
{
    public partial class LegacyReverseArrow : CompositeDrawable
    {
        private DrawableSliderRepeat drawableRepeat { get; set; } = null!;

        private Drawable proxy = null!;

        private Bindable<Color4> accentColour = null!;

        private bool textureIsDefaultSkin;

        private Drawable arrow = null!;

        private bool shouldRotate;

        [Resolved(CanBeNull = true)]
        private OsuVisualOrdering? visualOrdering { get; set; }

        [BackgroundDependencyLoader]
        private void load(DrawableHitObject drawableObject, ISkinSource skinSource)
        {
            const string lookup_name = @"reversearrow";

            drawableRepeat = (DrawableSliderRepeat)drawableObject;

            AutoSizeAxes = Axes.Both;

            var skin = skinSource.FindProvider(s => s.GetTexture(lookup_name) != null);

            InternalChild = arrow = new Sprite
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Texture = skin?.GetTexture(lookup_name)?.WithMaximumSize(maxSize: OsuHitObject.OBJECT_DIMENSIONS * 2),
            };

            textureIsDefaultSkin = skin is ISkinTransformer transformer && transformer.Skin is DefaultLegacySkin;

            shouldRotate = skinSource.GetConfig<SkinConfiguration.LegacySetting, decimal>(SkinConfiguration.LegacySetting.Version)?.Value <= 1;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            proxy = CreateProxy();

            if (visualOrdering?.IsActive == true)
            {
                // This component already proxies itself out of the repeat's skinnable container.
                // Proxying that container cannot draw the arrow, so order the existing proxy directly.
                visualOrdering.Register(this, drawableRepeat, OsuVisualKind.Arrow, existingProxy: proxy);
            }
            else
            {
                drawableRepeat.HitObjectApplied += onHitObjectApplied;
                onHitObjectApplied(drawableRepeat);
            }

            accentColour = drawableRepeat.AccentColour.GetBoundCopy();
            accentColour.BindValueChanged(c =>
            {
                arrow.Colour = textureIsDefaultSkin && c.NewValue.R + c.NewValue.G + c.NewValue.B > 600 / 255f ? Color4.Black : Color4.White;
            }, true);
        }

        private void onHitObjectApplied(DrawableHitObject drawableObject)
        {
            Debug.Assert(proxy.Parent == null);

            // see logic in LegacySliderHeadHitCircle.
            drawableRepeat.DrawableSlider.OverlayElementContainer.Add(proxy);

            arrow.Alpha = 1f;
            arrow.Scale = Vector2.One;
        }

        protected override void Update()
        {
            base.Update();

            if (Time.Current >= drawableRepeat.HitStateUpdateTime && drawableRepeat.State.Value == ArmedState.Hit)
            {
                double animDuration = Math.Min(300, drawableRepeat.HitObject.SpanDuration);
                float progress = Math.Clamp((float)((Time.Current - drawableRepeat.HitStateUpdateTime) / animDuration), 0f, 1f);
                arrow.Scale = new Vector2(Interpolation.ValueAt(progress, 1f, 1.4f, 0f, 1f, Easing.Out));
            }
            else
            {
                const double duration = 300;
                const float rotation = 5.625f;

                double loopCurrentTime = (Time.Current - drawableRepeat.AnimationStartTime.Value) % duration;

                // Reference: https://github.com/peppy/osu-stable-reference/blob/2280c4c436f80d04f9c79d3c905db00ac2902273/osu!/GameplayElements/HitObjects/Osu/HitCircleSliderEnd.cs#L79-L96
                if (shouldRotate)
                {
                    arrow.Rotation = Interpolation.ValueAt(loopCurrentTime, rotation, -rotation, 0, duration);
                    arrow.Scale = new Vector2(Interpolation.ValueAt(loopCurrentTime, 1.3f, 1, 0, duration));
                }
                else
                {
                    arrow.Scale = new Vector2(Interpolation.ValueAt(loopCurrentTime, 1.3f, 1, 0, duration, Easing.Out));
                }
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            visualOrdering?.Unregister(this);

            base.Dispose(isDisposing);

            if (drawableRepeat.IsNotNull())
            {
                drawableRepeat.HitObjectApplied -= onHitObjectApplied;
            }
        }
    }
}
