// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Lines;
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
        private readonly SliderViewportBounds viewportBounds = new SliderViewportBounds();
        private IReadOnlyList<Vector2>? viewportVertices;
        private PathDrawRange? viewportRange;
        private int viewportPathVersion = -1;
        private Vector2 viewportMinimum;
        private Vector2 viewportMaximum;

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
            if (Path.DrawRange is not { } range || !float.IsFinite(start.X) || !float.IsFinite(start.Y))
            {
                Path.Scale = Vector2.One;
                Path.Position = Vector2.Zero;
                return;
            }

            float radius = (ToScreenSpace(new Vector2(PathRadius, 0)) - start).Length;
            float fullWidth = (ToScreenSpace(new Vector2(Path.Width, 0)) - start).Length;
            float fullHeight = (ToScreenSpace(new Vector2(0, Path.Height)) - start).Length;

            // Ordinary sliders cannot exceed the viewport at any snaking progress. Avoid building
            // a range index for them at all; retain extra padding for stable's 1.15-radius bounds.
            if (fullWidth + radius * 2.3 < limit && fullHeight + radius * 2.3 < limit)
            {
                Path.Scale = Vector2.One;
                Path.Position = Vector2.Zero;
                return;
            }

            // Use the visible range rather than the fixed full-path bounds. Read vertices directly:
            // CurrentCurve would allocate/copy potentially hundreds of thousands of points per frame.
            if (!ReferenceEquals(viewportVertices, Path.Vertices) || viewportRange != range || viewportPathVersion != pathVersion.Value)
            {
                (viewportMinimum, viewportMaximum) = viewportBounds.Query(Path.Vertices, pathVersion.Value, range);
                viewportVertices = Path.Vertices;
                viewportRange = range;
                viewportPathVersion = pathVersion.Value;
            }

            Vector2 firstCorner = toDisplay(viewportMinimum);
            Vector2 secondCorner = toDisplay(viewportMaximum);
            Vector2 thirdCorner = toDisplay(new Vector2(viewportMinimum.X, viewportMaximum.Y));
            Vector2 fourthCorner = toDisplay(new Vector2(viewportMaximum.X, viewportMinimum.Y));
            Vector2 minimum = Vector2.ComponentMin(Vector2.ComponentMin(firstCorner, secondCorner), Vector2.ComponentMin(thirdCorner, fourthCorner));
            Vector2 maximum = Vector2.ComponentMax(Vector2.ComponentMax(firstCorner, secondCorner), Vector2.ComponentMax(thirdCorner, fourthCorner));
            var viewport = LegacySliderViewport.FromBounds(minimum, maximum, radius);
            var scale = viewport.ScaleFor(new Vector2(limit));

            if (!float.IsFinite(scale.X) || !float.IsFinite(scale.Y))
                scale = Vector2.One;

            // Transform only the stroke, not the gameplay/snaking endpoints. The viewport origin
            // includes stable's radius padding, integer truncation and asymmetric negative clipping.
            var anchor = ToLocalSpace(viewport.Origin);
            Path.Scale = scale;
            Path.Position = anchor * (Vector2.One - scale);

            Vector2 toDisplay(Vector2 point) => ToScreenSpace(Path.PositionInBoundingBox(point));

        }

        protected virtual Color4 GetBodyAccentColour(ISkinSource skin, Color4 hitObjectAccentColour) => hitObjectAccentColour;
    }
}
