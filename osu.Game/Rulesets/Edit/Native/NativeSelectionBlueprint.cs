// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Rulesets.Objects;
using osuTK;

namespace osu.Game.Rulesets.Edit.Native
{
    /// <summary>
    /// Outlines the screen-space bounds of any drawable hit object, whatever transform its playfield applies.
    /// </summary>
    public partial class NativeSelectionBlueprint : HitObjectSelectionBlueprint
    {
        private readonly Container outline;

        public NativeSelectionBlueprint(HitObject hitObject)
            : base(hitObject)
        {
            RelativeSizeAxes = Axes.None;
            InternalChild = outline = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                BorderThickness = 3,
                Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0.15f, AlwaysPresent = true }
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            outline.BorderColour = colours.Yellow;
            outline.Colour = colours.Yellow;
        }

        protected override void Update()
        {
            base.Update();

            if (Parent == null || !DrawableObject.IsPresent)
                return;

            var quad = DrawableObject.ScreenSpaceDrawQuad;
            var topLeft = Vector2.ComponentMin(Vector2.ComponentMin(Parent.ToLocalSpace(quad.TopLeft), Parent.ToLocalSpace(quad.TopRight)),
                Vector2.ComponentMin(Parent.ToLocalSpace(quad.BottomLeft), Parent.ToLocalSpace(quad.BottomRight)));
            var bottomRight = Vector2.ComponentMax(Vector2.ComponentMax(Parent.ToLocalSpace(quad.TopLeft), Parent.ToLocalSpace(quad.TopRight)),
                Vector2.ComponentMax(Parent.ToLocalSpace(quad.BottomLeft), Parent.ToLocalSpace(quad.BottomRight)));

            Position = topLeft;
            Size = Vector2.ComponentMax(bottomRight - topLeft, new Vector2(8));
        }
    }
}
