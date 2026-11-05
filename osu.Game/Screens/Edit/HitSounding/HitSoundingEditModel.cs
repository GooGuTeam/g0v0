// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Audio;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// Models the editable hitsound nodes of an <see cref="EditorBeatmap"/> and provides the mutations performed by the hitsounding screen.
    /// </summary>
    [Cached]
    public partial class HitSoundingEditModel : Component
    {
        /// <summary>
        /// Sentinel index used by a <see cref="PendingSampleUpdate"/> to append a newly materialised sample.
        /// </summary>
        private const int append_index = -1;

        /// <summary>
        /// All hitsound nodes of the beatmap, sorted by <see cref="HitSoundingNode.Time"/>.
        /// </summary>
        public IReadOnlyList<HitSoundingNode> Nodes
        {
            get
            {
                applyPendingChanges();
                return nodes;
            }
        }

        /// <summary>
        /// The nodes currently selected by the user.
        /// </summary>
        /// <remarks>
        /// Changes to this list are mirrored to <see cref="EditorBeatmap.SelectedHitObjects"/> for integration with the rest of the editor.
        /// They deliberately do not raise <see cref="Changed"/>, as selection is not a change to the beatmap.
        /// </remarks>
        public BindableList<HitSoundingNode> SelectedNodes { get; } = [];

        /// <summary>
        /// The normal-bank set displayed as an empty cell: beatmap samples when available, otherwise the user skin.
        /// </summary>
        public int DefaultSampleSetIndex { get; private set; }

        /// <summary>
        /// Invoked when the node list or the samples backing it may have changed.
        /// </summary>
        public event Action? Changed;

        /// <summary>
        /// Invoked after an explicit sample edit performed through this model has been committed to the beatmap.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Raised by <see cref="SetBank"/>, <see cref="RestoreDefaults"/>, <see cref="SetVolume"/> and <see cref="ToggleAddition"/>,
        /// after the beatmap transaction has ended and the node list has been refreshed, and only when at least one node actually
        /// had its samples changed. Edits which resolve to no change (an already-selected bank, an unchanged volume, an additions-only
        /// bank change on nodes without additions) never raise it, so listeners can audition unconditionally.
        /// </para>
        /// <para>
        /// Undo / redo, beatmap reprocessing and skin changes only raise <see cref="Changed"/>; they never raise this event.
        /// The supplied list is a fresh snapshot which the model does not retain; the node instances remain pooled, so listeners must
        /// read their samples during the callback rather than storing the list.
        /// </para>
        /// </remarks>
        public event Action<IReadOnlyList<HitSoundingNode>>? SamplesEdited;

        private readonly EditorBeatmap beatmap;

        private readonly List<HitSoundingNode> nodes = [];
        private readonly Dictionary<NodeKey, HitSoundingNode> nodeCache = [];

        private bool dirty;
        private bool applying;
        private bool applyQueued;
        private bool synchronisingSelection;

        public HitSoundingEditModel(EditorBeatmap beatmap)
        {
            this.beatmap = beatmap;

            beatmap.HitObjectAdded += onHitObjectChanged;
            beatmap.HitObjectRemoved += onHitObjectChanged;
            beatmap.HitObjectUpdated += onHitObjectChanged;
            beatmap.BeatmapReprocessed += invalidate;

            updateDefaultSampleSet();
            if (beatmap.BeatmapSkin != null)
                beatmap.BeatmapSkin.BeatmapSkinChanged += scheduleDefaultSampleSetUpdate;

            SelectedNodes.CollectionChanged += onSelectedNodesChanged;

            rebuild();
        }

        /// <summary>
        /// Applies a bank (and sample set) change to the given nodes.
        /// </summary>
        /// <remarks>
        /// A supplied <paramref name="setIndex"/> is applied to every sample of each targeted node (one shared set per node).
        /// An additions-only edit never changes the normal bank, and is a no-op on nodes without an addition.
        /// </remarks>
        /// <param name="targets">The nodes to change. Nodes no longer present in the beatmap are ignored.</param>
        /// <param name="bank">The target bank. May be <see cref="HitObjectComposer.HIT_BANK_AUTO"/> when <paramref name="additionsOnly"/> is <c>true</c>.</param>
        /// <param name="setIndex">The target sample set index. <c>0</c> is the default set, <c>1</c> the beatmap skin, <c>&gt;= 2</c> custom sets.
        /// Pass <c>-1</c> to leave the sample set untouched.</param>
        /// <param name="additionsOnly">Whether only addition samples should have their bank changed, preserving the normal sample.</param>
        public void SetBank(IEnumerable<HitSoundingNode> targets, string bank, int setIndex, bool additionsOnly)
        {
            if (string.IsNullOrEmpty(bank))
                return;

            if (additionsOnly)
            {
                if (bank != HitObjectComposer.HIT_BANK_AUTO && Array.IndexOf(HitSampleInfo.ALL_BANKS, bank) < 0)
                    return;
            }
            else if (Array.IndexOf(HitSampleInfo.ALL_BANKS, bank) < 0)
                return;

            applyPendingChanges();

            var plans = new List<PendingNodeUpdate>();

            foreach (var node in enumerateNodes(targets))
                planBankChange(node, bank, setIndex, additionsOnly, plans);

            raiseSamplesEdited(commit(plans));
        }

        /// <summary>
        /// Changes the shared sample set without changing each node's normal or addition banks.
        /// </summary>
        public void SetSetIndex(IEnumerable<HitSoundingNode> targets, int setIndex)
        {
            if (setIndex < 0)
                return;

            applyPendingChanges();
            var plans = new List<PendingNodeUpdate>();

            foreach (var node in enumerateNodes(targets))
            {
                if (!node.CanEditSamples)
                    continue;

                if (node.Samples.Count == 0)
                {
                    planBankChange(node, node.GetBank(false), setIndex, false, plans);
                    continue;
                }

                List<PendingSampleUpdate>? updates = null;

                for (int i = 0; i < node.Samples.Count; i++)
                {
                    var sample = node.Samples[i];
                    var updated = sample.With(newSuffix: setIndex >= 2 ? setIndex.ToString() : null, newUseBeatmapSamples: setIndex >= 1);
                    if (!hasSampleChanges(sample, updated))
                        continue;

                    updates ??= [];
                    updates.Add(new PendingSampleUpdate(i, updated));
                }

                if (updates != null)
                    plans.Add(new PendingNodeUpdate(node, updates));
            }

            raiseSamplesEdited(commit(plans));
        }

        /// <summary>
        /// Restores the given nodes to the editor's default hit sample (normal hit sound, default bank, default set, volume 100).
        /// </summary>
        /// <remarks>
        /// Only sample data is replaced; the hit object's timing and presence in the beatmap are never touched.
        /// Explicitly resetting a node may replace file samples, which is the documented behaviour of this operation.
        /// </remarks>
        /// <param name="targets">The nodes to reset. Nodes no longer present in the beatmap are ignored.</param>
        public void RestoreDefaults(IEnumerable<HitSoundingNode> targets)
        {
            applyPendingChanges();

            var reset = new List<HitSoundingNode>();

            foreach (var node in enumerateNodes(targets))
            {
                if (!canEditSamples(node) || isDefaultSampleList(node.Samples))
                    continue;

                reset.Add(node);
            }

            if (reset.Count == 0)
                return;

            beatmap.BeginChange();

            foreach (var node in reset)
            {
                IList<HitSampleInfo> samples = node.Samples;

                samples.Clear();
                samples.Add(new HitSampleInfo(HitSampleInfo.HIT_NORMAL));

                beatmap.Update(node.HitObject);
            }

            beatmap.EndChange();
            applyPendingChanges();
            raiseSamplesEdited(reset);
        }

        /// <summary>
        /// Applies a volume change to every sample of the given nodes.
        /// </summary>
        /// <param name="targets">The nodes to change. Nodes no longer present in the beatmap, or whose sample list is not editable, are ignored.</param>
        /// <param name="volume">The target volume, clamped to the same bounds as the sample editing UI.</param>
        /// <remarks>
        /// An editable node without samples materialises its normal sample at the requested volume.
        /// Body / loop nodes and unmaterialised repeat nodes never do so.
        /// </remarks>
        public void SetVolume(IEnumerable<HitSoundingNode> targets, int volume)
        {
            volume = Math.Clamp(volume, DrawableHitObject.MINIMUM_SAMPLE_VOLUME, 100);

            applyPendingChanges();

            var plans = new List<PendingNodeUpdate>();

            foreach (var node in enumerateNodes(targets))
            {
                if (!canEditSamples(node))
                    continue;

                IList<HitSampleInfo> samples = node.Samples;

                if (samples.Count == 0)
                {
                    // Editing a node without any samples is only meaningful for nodes which own their sample list (not slider loops),
                    // so materialise the normal sample the edit refers to.
                    if (canPopulateNode(node))
                    {
                        plans.Add(new PendingNodeUpdate(node,
                        [
                            new PendingSampleUpdate(append_index, new HitSampleInfo(HitSampleInfo.HIT_NORMAL, volume: volume))
                        ]));
                    }

                    continue;
                }

                List<PendingSampleUpdate>? updates = null;

                for (int i = 0; i < samples.Count; i++)
                {
                    if (samples[i].Volume == volume)
                        continue;

                    updates ??= [];
                    updates.Add(new PendingSampleUpdate(i, samples[i].With(newVolume: volume)));
                }

                if (updates != null)
                    plans.Add(new PendingNodeUpdate(node, updates));
            }

            raiseSamplesEdited(commit(plans));
        }

        /// <summary>
        /// Toggles the given addition sample on the given nodes as a batch.
        /// </summary>
        /// <remarks>
        /// Matches the editor's existing ternary toggle behaviour: if any applicable node is missing the addition, it is added to
        /// all nodes missing it; only when every applicable node already has it is it removed from all of them.
        /// Only samples in <see cref="HitSampleInfo.ALL_ADDITIONS"/> are supported. Body / loop nodes only support
        /// <see cref="HitSampleInfo.HIT_WHISTLE"/>, as that is the only addition which affects slider loops.
        /// The normal sample is never synthesised to make an addition editable.
        /// </remarks>
        /// <param name="targets">The nodes to toggle the addition on.</param>
        /// <param name="sampleName">The addition sample to toggle.</param>
        public void ToggleAddition(IEnumerable<HitSoundingNode> targets, string sampleName)
        {
            if (string.IsNullOrEmpty(sampleName) || Array.IndexOf(HitSampleInfo.ALL_ADDITIONS, sampleName) < 0)
                return;

            applyPendingChanges();

            var candidates = new List<HitSoundingNode>();

            foreach (var node in enumerateNodes(targets))
            {
                if (!node.CanEditSamples)
                    continue;

                // slider loops only react to the whistle addition (sliderwhistle).
                if (node.IsBody && sampleName != HitSampleInfo.HIT_WHISTLE)
                    continue;

                candidates.Add(node);
            }

            if (candidates.Count == 0)
                return;

            bool add = false;

            foreach (var node in candidates)
            {
                if (containsSample(node.Samples, sampleName))
                    continue;

                add = true;
                break;
            }

            beatmap.BeginChange();

            var edited = new List<HitSoundingNode>();

            foreach (var node in candidates)
            {
                IList<HitSampleInfo> samples = node.Samples;
                bool changed = false;

                if (add)
                {
                    if (!containsSample(samples, sampleName))
                    {
                        samples.Add(createAdditionSample(samples, sampleName));
                        changed = true;
                    }
                }
                else
                {
                    for (int i = samples.Count - 1; i >= 0; i--)
                    {
                        if (samples[i].Name != sampleName)
                            continue;

                        samples.RemoveAt(i);
                        changed = true;
                    }
                }

                if (changed)
                    edited.Add(node);

                beatmap.Update(node.HitObject);
            }

            beatmap.EndChange();
            applyPendingChanges();
            raiseSamplesEdited(edited);
        }

        /// <summary>
        /// Enumerates the given nodes, removing duplicates and nodes which no longer belong to the beatmap.
        /// </summary>
        private IEnumerable<HitSoundingNode> enumerateNodes(IEnumerable<HitSoundingNode> source)
        {
            var seen = new HashSet<HitSoundingNode>();

            foreach (var node in source)
            {
                if (!seen.Add(node))
                    continue;

                if (!nodeCache.ContainsKey(new NodeKey(node.HitObject, node.NodeIndex)))
                    continue;

                yield return node;
            }
        }

        private static void planBankChange(HitSoundingNode node, string bank, int setIndex, bool additionsOnly, List<PendingNodeUpdate> plans)
        {
            if (!node.CanEditSamples)
                return;

            IList<HitSampleInfo> samples = node.Samples;

            if (samples.Count == 0)
            {
                // Editing a node without any samples is only meaningful for nodes which own their sample list (not slider loops),
                // so materialise the normal sample the normal bank or set edit refers to. Addition edits never materialise a node,
                // as there is no addition to configure.
                if (!additionsOnly && canPopulateNode(node))
                {
                    string? suffix = setIndex >= 2 ? setIndex.ToString() : null;
                    string sampleBank = bank == HitObjectComposer.HIT_BANK_AUTO ? HitSampleInfo.BANK_NORMAL : bank;

                    plans.Add(new PendingNodeUpdate(node,
                    [
                        new PendingSampleUpdate(append_index, new HitSampleInfo(HitSampleInfo.HIT_NORMAL, sampleBank, suffix, volume: 100, editorAutoBank: true,
                            useBeatmapSamples: setIndex >= 1))
                    ]));
                }

                return;
            }

            // Configuring the addition bank requires at least one addition; an addition edit never creates samples.
            if (additionsOnly && !hasAnyAddition(samples))
                return;

            List<PendingSampleUpdate>? updates = null;

            for (int i = 0; i < samples.Count; i++)
            {
                HitSampleInfo sample = samples[i];
                bool isNormal = sample.Name == HitSampleInfo.HIT_NORMAL;

                bool bankChanged = false;
                string targetBank = sample.Bank;
                bool targetEditorAutoBank = sample.EditorAutoBank;

                if (!additionsOnly)
                {
                    // The normal sample always moves, as do addition samples which automatically follow the normal bank.
                    // Explicitly-banked additions stay independent of the normal bank.
                    if ((isNormal || sample.EditorAutoBank) && !string.Equals(sample.Bank, bank, StringComparison.Ordinal))
                    {
                        targetBank = bank;
                        bankChanged = true;
                    }
                }
                else if (!isNormal)
                {
                    if (bank == HitObjectComposer.HIT_BANK_AUTO)
                    {
                        string normalBank = getNormalBank(samples);

                        if (!string.Equals(sample.Bank, normalBank, StringComparison.Ordinal) || !sample.EditorAutoBank)
                        {
                            targetBank = normalBank;
                            targetEditorAutoBank = true;
                            bankChanged = true;
                        }
                    }
                    else if (!string.Equals(sample.Bank, bank, StringComparison.Ordinal) || sample.EditorAutoBank)
                    {
                        targetBank = bank;
                        targetEditorAutoBank = false;
                        bankChanged = true;
                    }
                }

                HitSampleInfo? updated = null;

                if (bankChanged)
                    updated = sample.With(newBank: targetBank, newEditorAutoBank: targetEditorAutoBank);

                if (setIndex >= 0)
                {
                    string? targetSuffix = setIndex >= 2 ? setIndex.ToString() : null;
                    bool targetUseBeatmapSamples = setIndex >= 1;

                    if (!string.Equals(sample.Suffix, targetSuffix, StringComparison.Ordinal) || sample.UseBeatmapSamples != targetUseBeatmapSamples)
                        updated = (updated ?? sample).With(newSuffix: targetSuffix, newUseBeatmapSamples: targetUseBeatmapSamples);
                }

                if (updated != null && hasSampleChanges(sample, updated))
                {
                    updates ??= [];
                    updates.Add(new PendingSampleUpdate(i, updated));
                }
            }

            if (updates != null)
                plans.Add(new PendingNodeUpdate(node, updates));
        }

        /// <summary>
        /// Applies prepared sample updates to the beatmap within a single reversible transaction.
        /// </summary>
        /// <returns>
        /// The nodes whose samples were actually changed, or <c>null</c> when no update could be applied.
        /// </returns>
        private List<HitSoundingNode>? commit(List<PendingNodeUpdate> plans)
        {
            if (plans.Count == 0)
                return null;

            List<HitSoundingNode>? edited = null;

            beatmap.BeginChange();

            foreach (var plan in plans)
            {
                IList<HitSampleInfo> samples = plan.Node.Samples;
                bool applied = false;

                foreach (var update in plan.Updates)
                {
                    if (update.Index == append_index)
                    {
                        samples.Add(update.Sample);
                        applied = true;
                    }
                    else if (update.Index < samples.Count)
                    {
                        samples[update.Index] = update.Sample;
                        applied = true;
                    }
                }

                if (applied)
                    (edited ??= []).Add(plan.Node);

                beatmap.Update(plan.Node.HitObject);
            }

            beatmap.EndChange();
            applyPendingChanges();

            return edited;
        }

        /// <summary>
        /// Raises <see cref="SamplesEdited"/> when a sample mutation actually changed at least one node.
        /// </summary>
        private void raiseSamplesEdited(List<HitSoundingNode>? edited)
        {
            if (edited == null || edited.Count == 0)
                return;

            SamplesEdited?.Invoke(edited);
        }

        private static string getNormalBank(IList<HitSampleInfo> samples)
        {
            for (int i = 0; i < samples.Count; i++)
            {
                if (samples[i].Name == HitSampleInfo.HIT_NORMAL)
                    return samples[i].Bank;
            }

            return HitSampleInfo.BANK_SOFT;
        }

        private static bool containsSample(IList<HitSampleInfo> samples, string sampleName)
        {
            for (int i = 0; i < samples.Count; i++)
            {
                if (samples[i].Name == sampleName)
                    return true;
            }

            return false;
        }

        private static bool hasAnyAddition(IList<HitSampleInfo> samples)
        {
            for (int i = 0; i < samples.Count; i++)
            {
                if (Array.IndexOf(HitSampleInfo.ALL_ADDITIONS, samples[i].Name) >= 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Whether an update produced by <see cref="planBankChange"/> actually changes the fields it targets.
        /// </summary>
        /// <remarks>
        /// Some sample types (for example file samples from converted beatmaps) intentionally ignore bank and suffix changes.
        /// Treating those as changes would open a beatmap transaction which does not alter the encoded beatmap.
        /// </remarks>
        private static bool hasSampleChanges(HitSampleInfo current, HitSampleInfo updated)
            => !string.Equals(current.Bank, updated.Bank, StringComparison.Ordinal)
               || current.EditorAutoBank != updated.EditorAutoBank
               || !string.Equals(current.Suffix, updated.Suffix, StringComparison.Ordinal)
               || current.UseBeatmapSamples != updated.UseBeatmapSamples;

        /// <summary>
        /// Whether a sample can be materialised into this node's own sample list.
        /// </summary>
        /// <remarks>
        /// Body / loop nodes are excluded, as are repeat nodes whose sample list has not been populated yet
        /// (resolving to the object samples, which drive the loop sound).
        /// </remarks>
        private static bool canPopulateNode(HitSoundingNode node)
        {
            if (node.IsBody)
                return false;

            return canEditSamples(node);
        }

        /// <summary>
        /// Whether the node's sample list is owned by the node and can be edited directly.
        /// </summary>
        /// <remarks>
        /// A repeat node whose <see cref="IHasRepeats.NodeSamples"/> entry has not been materialised resolves to the object's
        /// own sample list. Editing that node would silently retarget the object (and its body) instead of the repeat, so such
        /// nodes are left untouched until the repeat samples are populated.
        /// </remarks>
        private static bool canEditSamples(HitSoundingNode node)
        {
            if (node.NodeIndex < 0)
                return true;

            return node.HitObject is IHasRepeats hasRepeats && node.NodeIndex < hasRepeats.NodeSamples.Count;
        }

        /// <summary>
        /// Whether the sample list already consists solely of the editor's default normal hit sample.
        /// </summary>
        private static bool isDefaultSampleList(IList<HitSampleInfo> samples)
        {
            if (samples.Count != 1)
                return false;

            HitSampleInfo sample = samples[0];

            return sample.Name == HitSampleInfo.HIT_NORMAL
                   && string.Equals(sample.Bank, HitSampleInfo.BANK_NORMAL, StringComparison.Ordinal)
                   && sample.Suffix == null
                   && sample.Volume == 100
                   && !sample.UseBeatmapSamples;
        }

        /// <summary>
        /// Creates an addition sample inheriting the settings of the node's existing samples.
        /// </summary>
        /// <remarks>
        /// The sample is constructed directly rather than through <see cref="HitObject.CreateHitSampleInfo"/> so that specialised samples
        /// (for example file samples from converted beatmaps) are never silently turned into a normal sample.
        /// </remarks>
        private static HitSampleInfo createAdditionSample(IList<HitSampleInfo> samples, string sampleName)
        {
            // Prefer inheriting from an existing addition sample, matching the editor's existing behaviour.
            for (int i = 0; i < samples.Count; i++)
            {
                HitSampleInfo sample = samples[i];

                if (sample.Name == HitSampleInfo.HIT_NORMAL || sample.Name == sampleName)
                    continue;

                HitSampleInfo inherited = sample.With(newName: sampleName);

                if (inherited.Name == sampleName)
                    return inherited;
            }

            for (int i = 0; i < samples.Count; i++)
            {
                HitSampleInfo sample = samples[i];

                if (sample.Name == HitSampleInfo.HIT_NORMAL)
                    return new HitSampleInfo(sampleName, sample.Bank, sample.Suffix, sample.Volume, sample.EditorAutoBank, sample.UseBeatmapSamples);
            }

            return new HitSampleInfo(sampleName);
        }

        private void onHitObjectChanged(HitObject hitObject) => invalidate();

        private void onSelectedNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (applying)
                return;

            synchroniseSelection();
        }

        private void scheduleDefaultSampleSetUpdate() => Schedule(updateDefaultSampleSet);

        private void updateDefaultSampleSet()
        {
            int index = beatmap.BeatmapSkin?.GetAvailableSampleSets().Any(set => set.Filenames.Count > 0) == true ? 1 : 0;
            if (DefaultSampleSetIndex == index)
                return;

            DefaultSampleSetIndex = index;
            Changed?.Invoke();
        }

        private void invalidate()
        {
            dirty = true;

            if (applying)
                return;

            if (!IsLoaded)
            {
                applyPendingChanges();
                return;
            }

            if (applyQueued)
                return;

            applyQueued = true;
            Schedule(() =>
            {
                applyQueued = false;
                applyPendingChanges();
            });
        }

        private void applyPendingChanges()
        {
            if (!dirty)
                return;

            rebuild();
        }

        /// <summary>
        /// Rebuilds the node list, preserving node references for hit objects and nodes which still exist.
        /// </summary>
        private void rebuild()
        {
            if (applying)
                return;

            dirty = false;
            applying = true;

            bool selectionPruned;

            try
            {
                var newNodes = new List<HitSoundingNode>();
                var newCache = new Dictionary<NodeKey, HitSoundingNode>();

                foreach (var hitObject in beatmap.HitObjects)
                {
                    addNode(hitObject, -1, newNodes, newCache);

                    if (hitObject is IHasRepeats hasRepeats)
                    {
                        // NodeSamples has one entry per node, including the head (0) and the tail (RepeatCount + 1).
                        int nodeCount = hasRepeats.SpanCount() + 1;

                        for (int i = 0; i < nodeCount; i++)
                            addNode(hitObject, i, newNodes, newCache);
                    }
                }

                newNodes.Sort(compareNodes);

                nodes.Clear();
                nodes.AddRange(newNodes);

                nodeCache.Clear();

                foreach (var pair in newCache)
                    nodeCache.Add(pair.Key, pair.Value);

                selectionPruned = pruneSelection();
            }
            finally
            {
                applying = false;
            }

            if (selectionPruned)
                synchroniseSelection();

            Changed?.Invoke();
        }

        private void addNode(HitObject hitObject, int nodeIndex, List<HitSoundingNode> target, Dictionary<NodeKey, HitSoundingNode> cache)
        {
            var key = new NodeKey(hitObject, nodeIndex);

            if (!nodeCache.TryGetValue(key, out var node))
                node = new HitSoundingNode(hitObject, nodeIndex);

            target.Add(node);
            cache[key] = node;
        }

        private static int compareNodes(HitSoundingNode a, HitSoundingNode b)
        {
            int result = a.Time.CompareTo(b.Time);

            if (result != 0)
                return result;

            result = a.NodeIndex.CompareTo(b.NodeIndex);

            if (result != 0)
                return result;

            // Equal-time nodes of different objects need a stable, per-instance tiebreak so refreshes don't reorder them.
            return RuntimeHelpers.GetHashCode(a.HitObject).CompareTo(RuntimeHelpers.GetHashCode(b.HitObject));
        }

        /// <summary>
        /// Removes nodes which no longer exist from the selection.
        /// </summary>
        /// <returns>Whether any node was removed.</returns>
        private bool pruneSelection()
        {
            bool pruned = false;

            for (int i = SelectedNodes.Count - 1; i >= 0; i--)
            {
                HitSoundingNode node = SelectedNodes[i];

                if (nodeCache.ContainsKey(new NodeKey(node.HitObject, node.NodeIndex)))
                    continue;

                SelectedNodes.RemoveAt(i);
                pruned = true;
            }

            return pruned;
        }

        /// <summary>
        /// Mirrors <see cref="SelectedNodes"/> to <see cref="EditorBeatmap.SelectedHitObjects"/> for integration with the rest of the editor.
        /// </summary>
        private void synchroniseSelection()
        {
            if (synchronisingSelection)
                return;

            synchronisingSelection = true;

            try
            {
                var selectedObjects = new HashSet<HitObject>();

                foreach (var node in SelectedNodes)
                {
                    if (nodeCache.ContainsKey(new NodeKey(node.HitObject, node.NodeIndex)))
                        selectedObjects.Add(node.HitObject);
                }

                bool unchanged = selectedObjects.Count == beatmap.SelectedHitObjects.Count;

                if (unchanged)
                {
                    foreach (var hitObject in beatmap.SelectedHitObjects)
                    {
                        if (selectedObjects.Contains(hitObject))
                            continue;

                        unchanged = false;
                        break;
                    }
                }

                if (unchanged)
                    return;

                beatmap.SelectedHitObjects.Clear();
                beatmap.SelectedHitObjects.AddRange(selectedObjects);
            }
            finally
            {
                synchronisingSelection = false;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            beatmap.HitObjectAdded -= onHitObjectChanged;
            beatmap.HitObjectRemoved -= onHitObjectChanged;
            beatmap.HitObjectUpdated -= onHitObjectChanged;
            beatmap.BeatmapReprocessed -= invalidate;
            if (beatmap.BeatmapSkin != null)
                beatmap.BeatmapSkin.BeatmapSkinChanged -= scheduleDefaultSampleSetUpdate;

            SelectedNodes.CollectionChanged -= onSelectedNodesChanged;
        }

        private readonly struct NodeKey : IEquatable<NodeKey>
        {
            private readonly HitObject hitObject;
            private readonly int nodeIndex;

            public NodeKey(HitObject hitObject, int nodeIndex)
            {
                this.hitObject = hitObject;
                this.nodeIndex = nodeIndex;
            }

            public bool Equals(NodeKey other) => nodeIndex == other.nodeIndex && ReferenceEquals(hitObject, other.hitObject);

            public override bool Equals(object? obj) => obj is NodeKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(hitObject), nodeIndex);
        }

        private readonly struct PendingSampleUpdate
        {
            public readonly int Index;
            public readonly HitSampleInfo Sample;

            public PendingSampleUpdate(int index, HitSampleInfo sample)
            {
                Index = index;
                Sample = sample;
            }
        }

        private readonly struct PendingNodeUpdate
        {
            public readonly HitSoundingNode Node;
            public readonly List<PendingSampleUpdate> Updates;

            public PendingNodeUpdate(HitSoundingNode node, List<PendingSampleUpdate> updates)
            {
                Node = node;
                Updates = updates;
            }
        }
    }
}
