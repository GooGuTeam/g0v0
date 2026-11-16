// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Rulesets.Objects;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Edit.Native
{
    /// <summary>
    /// Places one object at <see cref="NativeHitObjectComposer{TObject,TAction}.PlacementTime"/> on left click,
    /// positioned by <see cref="NativeHitObjectComposer{TObject,TAction}.PositionObject"/>.
    /// </summary>
    public partial class NativePlacementBlueprint<TObject, TAction> : HitObjectPlacementBlueprint
        where TObject : HitObject
        where TAction : struct, Enum
    {
        public new TObject HitObject => (TObject)base.HitObject;

        [Resolved]
        private HitObjectComposer composer { get; set; } = null!;

        private NativeHitObjectComposer<TObject, TAction> nativeComposer => (NativeHitObjectComposer<TObject, TAction>)composer;

        private readonly Circle cursor;
        private bool positionValid;

        public NativePlacementBlueprint(TObject hitObject)
            : base(hitObject)
        {
            InternalChild = cursor = new Circle
            {
                Origin = Anchor.Centre,
                Size = new Vector2(16),
                Alpha = 0.8f,
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            cursor.Colour = colours.Yellow;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            BeginPlacement();
        }

        // Charts outside the legacy modes routinely stack objects at one time; never replace on placement.
        public override bool ReplacesExistingObject(HitObject existing) => false;

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != MouseButton.Left || !positionValid)
                return false;

            EndPlacement(true);
            return true;
        }

        public override SnapResult UpdateTimeAndPosition(Vector2 screenSpacePosition, double fallbackTime)
        {
            double time = nativeComposer.PlacementTime;
            base.UpdateTimeAndPosition(screenSpacePosition, time);

            positionValid = nativeComposer.PositionObject(HitObject, screenSpacePosition);
            cursor.Position = ToLocalSpace(screenSpacePosition);
            cursor.Alpha = positionValid ? 0.8f : 0.2f;

            ApplyDefaultsToHitObject();
            return new SnapResult(screenSpacePosition, time);
        }
    }
}
