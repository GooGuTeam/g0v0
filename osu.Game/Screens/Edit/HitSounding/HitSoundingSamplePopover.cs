// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit.Compose.Components.Timeline;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// A sample editing popover which operates on <see cref="HitSoundingNode"/>s rather than whole hit objects.
    /// </summary>
    /// <remarks>
    /// The sample mutations themselves are inherited from <see cref="SamplePointPiece.SampleEditPopover"/> and therefore
    /// continue to use the editor's standard reversible transaction flow; only the set of samples being edited changes.
    /// </remarks>
    public partial class HitSoundingSamplePopover : SamplePointPiece.SampleEditPopover
    {
        /// <summary>
        /// The nodes this popover edits, frozen when it was opened so that later selection changes do not retarget an edit in progress.
        /// </summary>
        private readonly HitSoundingNode[] targets;

        public HitSoundingSamplePopover(HitSoundingNode node, HitSoundingEditModel model)
            : base(node.HitObject)
        {
            targets = model.SelectedNodes.Contains(node) ? model.SelectedNodes.ToArray() : new[] { node };
        }

        /// <summary>
        /// Restricts the edited samples to the nodes which were selected when the popover was opened,
        /// or to the clicked node alone if it was not part of the selection.
        /// </summary>
        protected override IEnumerable<(HitObject hitObject, IList<HitSampleInfo> samples)> GetRelevantSamples(HitObject[] hitObjects)
        {
            foreach (var target in targets)
                yield return (target.HitObject, target.Samples);
        }

        /// <summary>
        /// Slider loops only react to the whistle addition, so a selection consisting solely of body nodes only offers that toggle.
        /// </summary>
        protected override IEnumerable<string> GetAdditionSamples()
        {
            foreach (var target in targets)
            {
                if (!target.IsBody)
                    return HitSampleInfo.ALL_ADDITIONS;
            }

            return new[] { HitSampleInfo.HIT_WHISTLE };
        }
    }
}
