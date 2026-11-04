// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Audio;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// A single editable hitsound node of a <see cref="HitObject"/>.
    /// </summary>
    /// <remarks>
    /// This is a live view over the samples stored on the hit object, so that it remains valid (and truthful)
    /// when the beatmap is reprocessed. Nodes are pooled by <see cref="HitSoundingEditModel"/> across refreshes.
    /// </remarks>
    public class HitSoundingNode
    {
        /// <summary>
        /// The hit object this node belongs to.
        /// </summary>
        public readonly HitObject HitObject;

        /// <summary>
        /// The index of this node in <see cref="IHasRepeats.NodeSamples"/>, or <c>-1</c> for the samples attached to the object itself.
        /// </summary>
        /// <remarks>
        /// For objects implementing <see cref="IHasRepeats"/>, index <c>0</c> is the head, indices <c>1..RepeatCount</c> are the repeats
        /// and <c>RepeatCount + 1</c> is the tail. Index <c>-1</c> references <see cref="HitObject.Samples"/>, which drives the
        /// body / loop samples of such objects.
        /// </remarks>
        public readonly int NodeIndex;

        public HitSoundingNode(HitObject hitObject, int nodeIndex)
        {
            if (nodeIndex < -1)
                throw new ArgumentOutOfRangeException(nameof(nodeIndex), @"Node index must be -1 or non-negative.");

            if (nodeIndex >= 0 && hitObject is not IHasRepeats)
                throw new ArgumentException($"Node index can only be specified for objects implementing {nameof(IHasRepeats)}.", nameof(nodeIndex));

            HitObject = hitObject;
            NodeIndex = nodeIndex;
        }

        /// <summary>
        /// Whether this node represents the body / loop samples of an object with repeats rather than a single hit node's samples.
        /// </summary>
        public bool IsBody => NodeIndex < 0 && HitObject is IHasRepeats;

        /// <summary>
        /// Whether the samples of this node can be edited as hit sample data.
        /// </summary>
        /// <remarks>
        /// Slider repeat nodes whose <see cref="IHasRepeats.NodeSamples"/> entry has not been materialised resolve to the
        /// object's own sample list, so editing them would silently retarget the object instead of the repeat.
        /// File samples from converted beatmaps ignore bank and suffix changes, so their configuration cells are disabled
        /// as well. Volume editing is still supported for file samples.
        /// </remarks>
        public bool CanEditSamples
        {
            get
            {
                if (NodeIndex >= 0)
                {
                    if (HitObject is not IHasRepeats hasRepeats || NodeIndex >= hasRepeats.NodeSamples.Count)
                        return false;
                }

                IList<HitSampleInfo> samples = Samples;

                for (int i = 0; i < samples.Count; i++)
                {
                    if (samples[i] is ConvertHitObjectParser.FileHitSampleInfo)
                        return false;
                }

                return true;
            }
        }

        /// <summary>
        /// The time at which this node is hit.
        /// </summary>
        public double Time
        {
            get
            {
                if (HitObject is not IHasRepeats hasRepeats)
                    return HitObject.StartTime;

                if (NodeIndex < 0)
                    return HitObject.StartTime + hasRepeats.Duration / hasRepeats.SpanCount() / 2;

                return HitObject.StartTime + hasRepeats.Duration * NodeIndex / hasRepeats.SpanCount();
            }
        }

        /// <summary>
        /// The samples backing this node.
        /// </summary>
        /// <remarks>
        /// Resolved on every access so that the node always reflects the current state of the beatmap.
        /// Never <c>null</c>, but may be empty if the node has no samples.
        /// </remarks>
        public IList<HitSampleInfo> Samples
        {
            get
            {
                if (NodeIndex >= 0 && HitObject is IHasRepeats hasRepeats && NodeIndex < hasRepeats.NodeSamples.Count)
                    return hasRepeats.NodeSamples[NodeIndex];

                return HitObject.Samples;
            }
        }

        /// <summary>
        /// The volume of this node, or <c>0</c> if it has no samples.
        /// </summary>
        public int Volume
        {
            get
            {
                IList<HitSampleInfo> samples = Samples;
                int volume = 0;

                for (int i = 0; i < samples.Count; i++)
                    volume = Math.Max(volume, samples[i].Volume);

                return volume;
            }
        }

        /// <summary>
        /// The bank shown by the normal lane (<paramref name="additionsOnly"/> <c>false</c>) or the addition lane (<c>true</c>).
        /// </summary>
        /// <remarks>
        /// The addition lane reports <see cref="HitObjectComposer.HIT_BANK_AUTO"/> when there is no addition or every addition
        /// follows the normal bank. Otherwise it reports the first explicitly-banked addition's bank. The normal lane falls back
        /// to <see cref="HitSampleInfo.BANK_SOFT"/> when the node has no normal sample.
        /// </remarks>
        public string GetBank(bool additionsOnly)
        {
            IList<HitSampleInfo> samples = Samples;

            if (additionsOnly)
            {
                for (int i = 0; i < samples.Count; i++)
                {
                    HitSampleInfo sample = samples[i];

                    if (!isAddition(sample.Name) || sample.EditorAutoBank)
                        continue;

                    return sample.Bank;
                }

                return HitObjectComposer.HIT_BANK_AUTO;
            }

            for (int i = 0; i < samples.Count; i++)
            {
                if (samples[i].Name == HitSampleInfo.HIT_NORMAL)
                    return samples[i].Bank;
            }

            return HitSampleInfo.BANK_SOFT;
        }

        /// <summary>
        /// Whether this node contains the given addition sample.
        /// </summary>
        /// <param name="sampleName">An addition sample name, as listed in <see cref="HitSampleInfo.ALL_ADDITIONS"/>.</param>
        public bool HasAddition(string sampleName)
        {
            if (string.IsNullOrEmpty(sampleName) || !isAddition(sampleName))
                return false;

            IList<HitSampleInfo> samples = Samples;

            for (int i = 0; i < samples.Count; i++)
            {
                if (string.Equals(samples[i].Name, sampleName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool isAddition(string sampleName) => Array.IndexOf(HitSampleInfo.ALL_ADDITIONS, sampleName) >= 0;

        /// <summary>
        /// The custom sample bank (sample set index) of this node, or <c>0</c> if it has no samples.
        /// </summary>
        public int SetIndex
        {
            get
            {
                IList<HitSampleInfo> samples = Samples;

                if (samples.Count == 0)
                    return 0;

                HitSampleInfo sample = samples[0];

                if (sample.Suffix != null)
                    return int.TryParse(sample.Suffix, out int suffix) ? suffix : 0;

                return sample.UseBeatmapSamples ? 1 : 0;
            }
        }

        public override string ToString() => $"{HitObject} node {NodeIndex}";
    }
}
