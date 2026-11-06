// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Platform;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Default
{
    public abstract partial class PlaySliderBody : SnakingSliderBody
    {
        protected IBindable<float> ScaleBindable { get; private set; } = null!;

        protected IBindable<Color4> AccentColourBindable { get; private set; } = null!;

        private IBindable<int> pathVersion = null!;

        [Resolved(CanBeNull = true)]
        private OsuRulesetConfigManager? config { get; set; }

        private readonly Bindable<bool> configSnakingOut = new Bindable<bool>();
        private readonly Bindable<int> viewportSize = new Bindable<int>();

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private GameHost? host { get; set; }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, DrawableHitObject drawableObject)
        {
            var drawableSlider = (DrawableSlider)drawableObject;

            ScaleBindable = drawableSlider.ScaleBindable.GetBoundCopy();
            ScaleBindable.BindValueChanged(scale => PathRadius = OsuHitObject.OBJECT_RADIUS * scale.NewValue, true);

            pathVersion = drawableSlider.PathVersion.GetBoundCopy();
            pathVersion.BindValueChanged(_ => Scheduler.AddOnce(Refresh));

            AccentColourBindable = drawableObject.AccentColour.GetBoundCopy();
            AccentColourBindable.BindValueChanged(accent => AccentColour = GetBodyAccentColour(skin, accent.NewValue), true);

            config?.BindWith(OsuRulesetSetting.SnakingInSliders, SnakingIn);
            config?.BindWith(OsuRulesetSetting.SnakingOutSliders, configSnakingOut);
            config?.BindWith(OsuRulesetSetting.SliderViewportSize, viewportSize);

            SnakingOut.BindTo(configSnakingOut);

            BorderColour = GetBorderColour(skin);
        }

        protected virtual Color4 GetBorderColour(ISkinSource skin) => Color4.White;

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            Vector2 automaticLimit = new Vector2(renderer.Viewport.Width, renderer.Viewport.Height);
            if (host?.Window != null)
                automaticLimit = new Vector2(host.Window.ClientSize.Width, host.Window.ClientSize.Height);

            if (automaticLimit.X <= 0 || automaticLimit.Y <= 0)
                automaticLimit = new Vector2(renderer.MaxTextureSize);
            else
                automaticLimit = Vector2.ComponentMin(automaticLimit, new Vector2(renderer.MaxTextureSize));

            Vector2 viewportLimit = viewportSize.Value > 0
                ? Vector2.ComponentMin(new Vector2(viewportSize.Value), new Vector2(renderer.MaxTextureSize))
                : automaticLimit;

            var start = ToScreenSpace(Vector2.Zero);
            var pixelsPerLocalUnit = new Vector2(
                (ToScreenSpace(Vector2.UnitX) - start).Length,
                (ToScreenSpace(Vector2.UnitY) - start).Length);

            // Stable draws the unchanged stroke into a window-sized render target. Limiting the path's
            // framebuffer reproduces that clipping without scaling its geometry, border or judgement coordinates.
            Path.Size = GetViewportLimitedSize(Size, viewportLimit, pixelsPerLocalUnit);
            Path.Scale = Vector2.One;
            Path.Position = Vector2.Zero;
        }

        internal static Vector2 GetViewportLimitedSize(Vector2 pathSize, Vector2 viewportLimit, Vector2 pixelsPerLocalUnit)
        {
            if (!float.IsFinite(pathSize.X) || !float.IsFinite(pathSize.Y)
                || !float.IsFinite(viewportLimit.X) || !float.IsFinite(viewportLimit.Y)
                || !float.IsFinite(pixelsPerLocalUnit.X) || !float.IsFinite(pixelsPerLocalUnit.Y)
                || viewportLimit.X <= 0 || viewportLimit.Y <= 0
                || pixelsPerLocalUnit.X <= 0 || pixelsPerLocalUnit.Y <= 0)
                return pathSize;

            return new Vector2(
                Math.Min(pathSize.X, viewportLimit.X / pixelsPerLocalUnit.X),
                Math.Min(pathSize.Y, viewportLimit.Y / pixelsPerLocalUnit.Y));
        }

        protected virtual Color4 GetBodyAccentColour(ISkinSource skin, Color4 hitObjectAccentColour) => hitObjectAccentColour;
    }
}
