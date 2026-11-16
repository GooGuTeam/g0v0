// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Edit.Tools;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK;

namespace osu.Game.Rulesets.Edit.Native
{
    /// <summary>
    /// A composer for rulesets outside the four legacy modes, whose objects are neither osu!-playfield coordinates nor scrolling lanes
    /// (judgement lines, 3D tracks, free-floating notes and so on).
    /// </summary>
    /// <remarks>
    /// The ruleset only describes what can be placed (<see cref="Templates"/>) and how a screen-space point maps onto an object
    /// (<see cref="PositionObject"/>). The framework supplies tools, placement at the snapped current time, selection outlines,
    /// drag-moving through <see cref="PositionObject"/>, deletion, and a property inspector driven by <see cref="EditablePropertyAttribute"/>.
    /// Saving and undo go through the ruleset's <see cref="Formats.RulesetBeatmapFormat"/>.
    /// </remarks>
    public abstract partial class NativeHitObjectComposer<TObject, TAction> : HitObjectComposer<TObject, TAction>
        where TObject : HitObject
        where TAction : struct, Enum
    {
        protected NativeHitObjectComposer(Ruleset ruleset)
            : base(ruleset)
        {
        }

        /// <summary>
        /// Combo colouring is a legacy-mode concept; native rulesets opt in by overriding this.
        /// </summary>
        public override Bindable<TernaryState>? SelectionNewComboState => null;

        /// <summary>
        /// The kinds of object that can be placed, in toolbox order.
        /// </summary>
        protected abstract IEnumerable<NativeObjectTemplate<TObject>> Templates { get; }

        /// <summary>
        /// Applies a screen-space position to <paramref name="hitObject"/>, whose time has already been set.
        /// Used for both placement and drag-moving.
        /// </summary>
        /// <returns>Whether the position could be applied (e.g. false when no target exists under the cursor).</returns>
        public abstract bool PositionObject(TObject hitObject, Vector2 screenSpacePosition);

        /// <summary>
        /// The time new objects are placed at. Defaults to the beat-snapped current editor time,
        /// which suits playfields where time is not a spatial axis.
        /// </summary>
        public virtual double PlacementTime => BeatSnapProvider.SnapTime(EditorClock.CurrentTime);

        protected sealed override IReadOnlyList<CompositionTool<TAction>> CompositionTools =>
            Templates.Select(t => (CompositionTool<TAction>)new TemplateCompositionTool(t)).ToArray();

        protected override ComposeBlueprintContainer CreateBlueprintContainer() => new NativeBlueprintContainer(this);

        protected override Drawable CreateHitObjectInspector() => new EditablePropertyInspector();

        private class TemplateCompositionTool : CompositionTool<TAction>
        {
            private readonly NativeObjectTemplate<TObject> template;

            public TemplateCompositionTool(NativeObjectTemplate<TObject> template)
                : base(template.Name)
            {
                this.template = template;
            }

            public override Drawable CreateIcon() => new SpriteIcon { Icon = template.Icon };

            public override HitObjectPlacementBlueprint CreatePlacementBlueprint() => new NativePlacementBlueprint<TObject, TAction>(template.Create());
        }

        private partial class NativeBlueprintContainer : ComposeBlueprintContainer
        {
            public NativeBlueprintContainer(NativeHitObjectComposer<TObject, TAction> composer)
                : base(composer)
            {
            }

            protected override SelectionHandler<HitObject> CreateSelectionHandler() => new NativeSelectionHandler<TObject, TAction>();

            public override HitObjectSelectionBlueprint CreateHitObjectBlueprintFor(HitObject hitObject) => new NativeSelectionBlueprint(hitObject);

            protected override bool TryMoveBlueprints(DragEvent e, IList<(SelectionBlueprint<HitObject> blueprint, Vector2[] originalSnapPositions)> blueprints)
            {
                // Position-only: the reference blueprint follows the cursor, the rest keep their relative offsets.
                var (reference, original) = blueprints.First();
                Vector2 target = original.First() + (e.ScreenSpaceMousePosition - e.ScreenSpaceMouseDownPosition);

                return SelectionHandler.HandleMovement(new MoveSelectionEvent<HitObject>(reference, target - reference.ScreenSpaceSelectionPoint));
            }
        }
    }
}
