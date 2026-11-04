// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Rendering;
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

            float limit = viewportSize.Value > 0 ? viewportSize.Value : renderer.MaxTextureSize;
            var start = ToScreenSpace(Vector2.Zero);
            if (!float.IsFinite(start.X) || !float.IsFinite(start.Y) || !float.IsFinite(Path.Width) || !float.IsFinite(Path.Height))
            {
                Path.Scale = Vector2.One;
                Path.Position = Vector2.Zero;
                return;
            }

            float width = (ToScreenSpace(new Vector2(Path.Width, 0)) - start).Length;
            float height = (ToScreenSpace(new Vector2(0, Path.Height)) - start).Length;
            var scale = new Vector2(width > 0 ? Math.Min(1, limit / width) : 1, height > 0 ? Math.Min(1, limit / height) : 1);

            if (!float.IsFinite(scale.X) || !float.IsFinite(scale.Y))
                scale = Vector2.One;

            // Transform the complete stroke while leaving positions used for judgement and snaking unchanged.
            var anchor = ToLocalSpace(new Vector2(Math.Max(0, start.X), Math.Max(0, start.Y)));
            Path.Scale = scale;
            Path.Position = anchor * (Vector2.One - scale);
        }

        protected virtual Color4 GetBodyAccentColour(ISkinSource skin, Color4 hitObjectAccentColour) => hitObjectAccentColour;
    }
}
