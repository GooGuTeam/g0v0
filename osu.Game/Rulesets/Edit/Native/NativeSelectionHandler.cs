// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit.Compose.Components;

namespace osu.Game.Rulesets.Edit.Native
{
    /// <summary>
    /// Moves selected objects by re-applying <see cref="NativeHitObjectComposer{TObject,TAction}.PositionObject"/> at their dragged positions.
    /// Time moves are handled by the timeline as for every ruleset.
    /// </summary>
    public partial class NativeSelectionHandler<TObject, TAction> : EditorSelectionHandler
        where TObject : HitObject
        where TAction : struct, Enum
    {
        [Resolved]
        private HitObjectComposer composer { get; set; } = null!;

        public override bool HandleMovement(MoveSelectionEvent<HitObject> moveEvent)
        {
            var nativeComposer = (NativeHitObjectComposer<TObject, TAction>)composer;
            bool moved = false;

            foreach (var blueprint in SelectedBlueprints.ToArray())
            {
                if (blueprint.Item is not TObject hitObject)
                    continue;

                if (!nativeComposer.PositionObject(hitObject, blueprint.ScreenSpaceSelectionPoint + moveEvent.ScreenSpaceDelta))
                    continue;

                EditorBeatmap.Update(hitObject);
                moved = true;
            }

            return moved;
        }
    }
}
