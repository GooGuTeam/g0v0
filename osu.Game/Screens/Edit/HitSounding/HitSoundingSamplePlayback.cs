// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Threading;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Skinning;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// Plays the hitsounds of a <see cref="HitSoundingEditModel"/> while the editor is playing or while the user scrubs the roll.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The host places this drawable under an <see cref="EditorSkinProvidingContainer"/> (the hitsounding screen already does), so
    /// beatmap sample sets resolve exactly as they do in the sample point popover. Internally the component caches a local
    /// <see cref="ISamplePlaybackDisabler"/> so the editor-wide disabler (which is permanently enabled outside the compose screen)
    /// does not silence hitsounding previews. The global editor state is never modified.
    /// </para>
    /// <para>
    /// Three playback paths are handled independently:
    /// <list type="bullet">
    /// <item><description>
    /// <see cref="Update"/> tracks <see cref="EditorClock"/> and fires the discrete hit nodes crossed by normal playback.
    /// Slider body nodes are never treated as hits; their loop samples are started while the playhead is inside the slider span
    /// and stopped when it leaves it.
    /// </description></item>
    /// <item><description>
    /// <see cref="PreviewSeek"/> fires the discrete hit nodes crossed by a wheel scrub. It is intended to be called once per frame
    /// while the roll is scrubbing; the component keeps its own cursor, so overlapping or repeated frames do not double-fire a node.
    /// Slider loop samples are not started while scrubbing, as they have no discrete hit time and would either be a fake hit or
    /// linger after the scrub ends.
    /// </description></item>
    /// <item><description>
    /// <see cref="HitSoundingEditModel.SamplesEdited"/> auditions an explicit sample edit without moving the playhead. Discrete
    /// nodes are played once through the hit pool, with identical sample lists collapsed so a batch edit does not stack the same
    /// sound. Body nodes start their actual sliding samples as a short bounded loop which is always stopped afterwards.
    /// </description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <see cref="NodePlayed"/> reports the nodes heard by actual playhead playback only: discrete hits crossed by <see cref="Update"/>
    /// and body loops started by it. Wheel scrubs and edit auditions deliberately do not report, so callers can use it to flash
    /// points which the playhead is passing rather than points the user just edited.
    /// </para>
    /// <para>
    /// Playback is suppressed while the owning <see cref="EditorScreen"/> is hidden, while the screen is not present, and while
    /// <see cref="EditorBeatmap.UpdateInProgress"/> is set (undo and reprocessing never emit a burst of sounds; an explicit edit is
    /// auditioned after its beatmap transaction has completed).
    /// All samples are resolved through the current skin and are played by a lazily grown pool of reusable
    /// <see cref="PausableSkinnableSound"/>s: idle players are reused and new players are only created for the actual number of
    /// simultaneous sounds, so no drawables or sample lookups are created per frame. No chord or overlap is silently truncated.
    /// </para>
    /// <para>
    /// Integration: add this drawable inside the screen's <see cref="EditorSkinProvidingContainer"/>, as a sibling of the roll, and
    /// cache the instance so the roll can resolve and call <see cref="PreviewSeek"/>. Order does not affect correctness; placing it
    /// after the roll only removes one frame of sample latency. It observes the owning <see cref="EditorScreen"/> visibility itself, so
    /// hidden cached screens stay silent without host plumbing.
    /// </para>
    /// </remarks>
    public partial class HitSoundingSamplePlayback : CompositeDrawable
    {
        /// <summary>
        /// Invoked when uninterrupted playhead playback plays a discrete hit node, or starts a slider body loop.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Raised once per node when its samples are actually played: discrete nodes crossed by <see cref="Update"/>, and body
        /// loops when the playhead enters their slider span. Nodes without any playable samples never raise it.
        /// </para>
        /// <para>
        /// Wheel scrubs (<see cref="PreviewSeek"/>), seeks, discontinuities and explicit sample-edit auditions deliberately do not
        /// raise it, so callers can flash the points the playhead is passing rather than everything that made a sound.
        /// Invoked on the update thread.
        /// </para>
        /// </remarks>
        public event Action<HitSoundingNode>? NodePlayed;

        /// <summary>
        /// The largest clock step treated as continuous playback. Larger steps are seeks / tab-out jumps and are not replayed.
        /// </summary>
        private const double maximum_continuous_step = 250;

        /// <summary>
        /// The bounded duration of a slider body edit audition.
        /// </summary>
        private const double edit_audition_loop_duration = 300;

        private readonly HitSoundingEditModel model;
        private readonly SamplePlaybackDisabler localDisabler = new SamplePlaybackDisabler();
        private readonly List<SamplePlayer> hitPlayers = [];
        private readonly List<SamplePlayer> loopPlayers = [];
        private readonly Dictionary<HitSoundingNode, ISampleInfo[]> sampleCache = [];
        private readonly Dictionary<HitSoundingNode, ISampleInfo[]> loopSampleCache = [];
        private readonly Dictionary<HitSoundingNode, SamplePlayer> activeLoops = [];
        private readonly List<HitSoundingNode> crossings = [];
        private readonly List<ISampleInfo[]> auditionGroups = [];

        private readonly List<HitSoundingNode> crossingNodes = [];
        private readonly List<HitSoundingNode> bodyNodes = [];

        private Container hitContainer = null!;
        private Container loopContainer = null!;

        private SamplePlayer? auditionLoopPlayer;
        private ScheduledDelegate? auditionStop;

        private bool nodesDirty = true;

        private double lastPlaybackTime = double.NaN;
        private double previewPosition;
        private bool previewActive;
        private bool previewRequested;
        private bool screenVisible = true;
        private Bindable<Visibility>? screenState;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private EditorBeatmap beatmap { get; set; } = null!;

        public HitSoundingSamplePlayback(HitSoundingEditModel model)
        {
            this.model = model;

            RelativeSizeAxes = Axes.Both;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<ISamplePlaybackDisabler>(localDisabler);
            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            model.Changed += onModelChanged;
            model.SamplesEdited += onSamplesEdited;

            AddInternal(hitContainer = new Container { RelativeSizeAxes = Axes.Both });
            AddInternal(loopContainer = new Container { RelativeSizeAxes = Axes.Both });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            screenState = this.FindClosestParent<EditorScreen>()?.State;

            if (screenState != null)
            {
                screenVisible = screenState.Value == Visibility.Visible;
                screenState.ValueChanged += onScreenStateChanged;
            }

            ResetPlayback();
        }

        /// <summary>
        /// Plays the discrete hit nodes crossed while scrubbing from <paramref name="from"/> to <paramref name="to"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Call this once per frame during a roll wheel scrub. The forward interval is <c>(from, to]</c>; the backward interval is
        /// <c>[to, from)</c>. The component tracks the previous preview position itself, so overlapping calls (or repeating the same
        /// call) cannot fire a node twice. When the caller's interval does not contain the previously tracked position, the gap is
        /// skipped rather than replayed, which keeps animation-driven smooth seeks and direct wheel seeks equally deterministic.
        /// </para>
        /// <para>
        /// Slider body / loop nodes are intentionally excluded: they have no discrete hit time, and firing their normal sample at
        /// the body marker would produce a fake extra circle. <see cref="NodePlayed"/> is not raised for these preview hits.
        /// </para>
        /// </remarks>
        /// <param name="from">The previously displayed clock position.</param>
        /// <param name="to">The currently displayed clock position.</param>
        public void PreviewSeek(double from, double to)
        {
            previewRequested = true;

            if (!IsLoaded || !IsPresent || !screenVisible || beatmap.UpdateInProgress.Value)
            {
                lastPlaybackTime = double.NaN;
                previewActive = false;
                previewRequested = false;
                stopLoops();
                stopHitPlayers();
                return;
            }

            ensureNodes();
            localDisabler.Set(false);

            if (double.IsNaN(from))
                from = previewActive ? previewPosition : to;

            double trackedPosition = previewActive ? previewPosition : from;

            if (double.IsNaN(trackedPosition) || double.IsNaN(to))
            {
                previewPosition = to;
                previewActive = true;
                lastPlaybackTime = double.NaN;
                return;
            }

            double start = HitSoundingSampleCrossings.GetPreviewStart(from, trackedPosition, to);

            if (start == to)
            {
                previewPosition = to;
                previewActive = true;
                lastPlaybackTime = double.NaN;
                return;
            }

            crossings.Clear();
            HitSoundingSampleCrossings.Collect(crossingNodes, start, to, crossings);

            for (int i = 0; i < crossings.Count; i++)
                playNode(crossings[i]);

            previewPosition = to;
            previewActive = true;
            lastPlaybackTime = double.NaN;

            // Regular playback is about to resume from the new position; loops are restarted by Update once the clock moves again.
            stopLoops();
        }

        /// <summary>
        /// Clears all playback state and stops every preview and edit-audition sound.
        /// </summary>
        /// <remarks>
        /// Automatically invoked when the owning <see cref="EditorScreen"/> is hidden or shown (including editor mode switches), so
        /// callers normally do not need to invoke this. It is public so a host can force a resync after moving the playhead without
        /// scrubbing.
        /// </remarks>
        public void ResetPlayback()
        {
            lastPlaybackTime = double.NaN;
            previewPosition = 0;
            previewActive = false;
            previewRequested = false;

            stopLoops();
            stopHitPlayers();
            stopAudition();

            localDisabler.Set(!screenVisible || beatmap.UpdateInProgress.Value);
        }

        protected override void Update()
        {
            base.Update();

            if (!IsLoaded)
                return;

            ensureNodes();

            bool blocked = !IsPresent || !screenVisible || beatmap.UpdateInProgress.Value;
            localDisabler.Set(blocked);

            if (blocked)
            {
                lastPlaybackTime = double.NaN;
                previewActive = false;
                previewRequested = false;
                stopLoops();
                stopHitPlayers();
                stopAudition();
                return;
            }

            double current = editorClock.CurrentTime;
            bool playing = editorClock.IsRunning && !editorClock.SeekingOrStopped.Value && !editorClock.IsSeeking;

            if (previewRequested || !playing)
            {
                // A scrub or seek owns playback for this frame. Do not also fire the same range from the regular clock path;
                // only resync the baseline so playback resumes without replaying anything crossed during the seek.
                lastPlaybackTime = current;
                previewRequested = false;
                stopLoops();
                return;
            }

            previewActive = false;

            if (double.IsNaN(lastPlaybackTime))
            {
                lastPlaybackTime = current;
                updateLoops(current, reportStarts: true);
                return;
            }

            double elapsed = current - lastPlaybackTime;

            if (elapsed == 0)
            {
                // No clock movement. Nothing was crossed and any loop restart is left to the first moving frame, which reports it
                // as a normal loop start.
                return;
            }

            if (elapsed < 0 || elapsed > maximum_continuous_step)
            {
                // Discontinuity (seek, mode switch, or a stalled frame). Resync rather than firing everything in between.
                lastPlaybackTime = current;
                stopLoops();
                updateLoops(current, reportStarts: false);
                return;
            }

            crossings.Clear();
            HitSoundingSampleCrossings.Collect(crossingNodes, lastPlaybackTime, current, crossings);

            for (int i = 0; i < crossings.Count; i++)
            {
                HitSoundingNode node = crossings[i];

                if (playNode(node))
                    NodePlayed?.Invoke(node);
            }

            lastPlaybackTime = current;
            updateLoops(current, reportStarts: true);
        }

        /// <summary>
        /// Starts / stops slider loop samples for the current playhead position.
        /// </summary>
        /// <param name="current">The current playhead position.</param>
        /// <param name="reportStarts">
        /// Whether newly started loops raise <see cref="NodePlayed"/>. Discontinuities resync the loop state without reporting,
        /// as their start was caused by a seek rather than by continuous playback.
        /// </param>
        private void updateLoops(double current, bool reportStarts)
        {
            for (int i = 0; i < bodyNodes.Count; i++)
            {
                HitSoundingNode node = bodyNodes[i];

                if (node.HitObject is not IHasRepeats hasRepeats)
                    continue;

                if (current >= node.HitObject.StartTime && current < hasRepeats.EndTime)
                {
                    if (startLoop(node) && reportStarts)
                        NodePlayed?.Invoke(node);
                }
                else
                    stopLoop(node);
            }
        }

        /// <summary>
        /// Starts the loop samples of a body node, if it is not already sounding.
        /// </summary>
        /// <returns>Whether a new loop was started (and therefore produced sound).</returns>
        private bool startLoop(HitSoundingNode node)
        {
            if (activeLoops.ContainsKey(node))
                return false;

            ISampleInfo[] samples = getLoopSamples(node);

            if (samples.Length == 0)
                return false;

            SamplePlayer player = getLoopPlayer();
            player.Reserved = true;
            player.Samples = samples;
            activeLoops.Add(node, player);
            player.QueuePlay();
            return true;
        }

        /// <summary>
        /// Returns an unused loop player, growing the pool to the number of overlapping sliders when necessary.
        /// </summary>
        private SamplePlayer getLoopPlayer()
        {
            for (int i = 0; i < loopPlayers.Count; i++)
            {
                if (!loopPlayers[i].Reserved)
                    return loopPlayers[i];
            }

            var player = new SamplePlayer(looping: true);
            loopPlayers.Add(player);
            loopContainer.Add(player);
            return player;
        }

        private void stopLoop(HitSoundingNode node)
        {
            if (!activeLoops.Remove(node, out SamplePlayer? player))
                return;

            player.Stop();
            player.Reserved = false;
        }

        private void stopLoops()
        {
            foreach (var player in activeLoops.Values)
            {
                player.Stop();
                player.Reserved = false;
            }

            activeLoops.Clear();
        }

        private void stopHitPlayers()
        {
            for (int i = 0; i < hitPlayers.Count; i++)
                hitPlayers[i].Stop();
        }

        /// <summary>
        /// Plays the discrete samples of a node.
        /// </summary>
        /// <returns>Whether a sound was queued.</returns>
        private bool playNode(HitSoundingNode node)
        {
            // Body nodes are handled as continuous loops by updateLoops(), never as discrete hits.
            if (node.IsBody)
                return false;

            ISampleInfo[] samples = getSamples(node);

            if (samples.Length == 0)
                return false;

            playSamples(samples);
            return true;
        }

        /// <summary>
        /// Plays a prepared sample list from the next free player in the hit pool.
        /// </summary>
        private void playSamples(ISampleInfo[] samples)
        {
            SamplePlayer player = getHitPlayer();
            player.Samples = samples;
            player.QueuePlay();
        }

        private SamplePlayer getHitPlayer()
        {
            for (int i = 0; i < hitPlayers.Count; i++)
            {
                if (!hitPlayers[i].Busy)
                    return hitPlayers[i];
            }

            // All existing players are still sounding. Grow the pool rather than cutting off a simultaneous hit.
            var player = new SamplePlayer();
            hitPlayers.Add(player);
            hitContainer.Add(player);
            return player;
        }

        private ISampleInfo[] getSamples(HitSoundingNode node)
        {
            if (!sampleCache.TryGetValue(node, out ISampleInfo[]? samples))
            {
                samples = copySamples(node.Samples);
                sampleCache.Add(node, samples);
            }

            return samples;
        }

        private ISampleInfo[] getLoopSamples(HitSoundingNode node)
        {
            if (!loopSampleCache.TryGetValue(node, out ISampleInfo[]? samples))
            {
                samples = copySamples(node.HitObject.CreateSlidingSamples());
                loopSampleCache.Add(node, samples);
            }

            return samples;
        }

        /// <summary>
        /// Copies a live sample list into the array required by <see cref="SkinnableSound.Samples"/>.
        /// </summary>
        private static ISampleInfo[] copySamples(IList<HitSampleInfo> samples)
        {
            if (samples.Count == 0)
                return Array.Empty<ISampleInfo>();

            var copy = new ISampleInfo[samples.Count];

            for (int i = 0; i < samples.Count; i++)
                copy[i] = samples[i];

            return copy;
        }

        /// <summary>
        /// Auditions the nodes of an explicit sample edit without moving the playhead.
        /// </summary>
        /// <remarks>
        /// Only sound is produced: neither the clock nor any playback cursor is touched, <see cref="NodePlayed"/> is not raised, and no
        /// loop is left running. Identical discrete sample lists are collapsed so a batch edit which gives every node the same sound is
        /// heard once, and only the first edited body node is looped, as all edited bodies normally share the new configuration.
        /// </remarks>
        private void onSamplesEdited(IReadOnlyList<HitSoundingNode> nodes)
        {
            if (!IsLoaded || !IsPresent || !screenVisible || beatmap.UpdateInProgress.Value)
                return;

            ensureNodes();
            localDisabler.Set(false);

            auditionHits(nodes);
            auditionLoop(nodes);
        }

        private void auditionHits(IReadOnlyList<HitSoundingNode> nodes)
        {
            auditionGroups.Clear();

            for (int i = 0; i < nodes.Count; i++)
            {
                HitSoundingNode node = nodes[i];

                if (node.IsBody)
                    continue;

                ISampleInfo[] samples = getSamples(node);

                if (samples.Length == 0 || containsAuditionGroup(samples))
                    continue;

                auditionGroups.Add(samples);
                playSamples(samples);
            }
        }

        private bool containsAuditionGroup(ISampleInfo[] samples)
        {
            for (int i = 0; i < auditionGroups.Count; i++)
            {
                if (samplesEqual(auditionGroups[i], samples))
                    return true;
            }

            return false;
        }

        private void auditionLoop(IReadOnlyList<HitSoundingNode> nodes)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                HitSoundingNode node = nodes[i];

                if (!node.IsBody)
                    continue;

                ISampleInfo[] samples = getLoopSamples(node);

                if (samples.Length == 0)
                    continue;

                startAuditionLoop(samples);
                break;
            }
        }

        /// <summary>
        /// Plays sliding samples as a loop which is stopped after <see cref="edit_audition_loop_duration"/> milliseconds.
        /// </summary>
        private void startAuditionLoop(ISampleInfo[] samples)
        {
            auditionStop?.Cancel();
            auditionStop = null;

            // Reuse the loop pool; the player is only held (reserved) for the bounded audition duration.
            auditionLoopPlayer ??= getLoopPlayer();

            // Restart from the new samples rather than stacking a second loop; only one edit audition is ever active.
            auditionLoopPlayer.Reserved = true;
            auditionLoopPlayer.Stop();
            auditionLoopPlayer.Samples = samples;
            auditionLoopPlayer.QueuePlay();

            auditionStop = Scheduler.AddDelayed(stopAuditionLoop, edit_audition_loop_duration);
        }

        private void stopAuditionLoop()
        {
            auditionStop = null;
            releaseAuditionLoop();
        }

        private void stopAudition()
        {
            auditionStop?.Cancel();
            auditionStop = null;
            releaseAuditionLoop();
        }

        /// <summary>
        /// Stops the audition loop and returns its player to the loop pool.
        /// </summary>
        private void releaseAuditionLoop()
        {
            if (auditionLoopPlayer == null)
                return;

            auditionLoopPlayer.Stop();
            auditionLoopPlayer.Reserved = false;
            auditionLoopPlayer = null;
        }

        /// <summary>
        /// Compares two sample lists for audibly identical content.
        /// </summary>
        /// <remarks>
        /// <see cref="HitSampleInfo"/> equality deliberately ignores the volume, which is exactly the field a volume edit changes, so
        /// the audible fields are compared explicitly here.
        /// </remarks>
        private static bool samplesEqual(ISampleInfo[] a, ISampleInfo[] b)
        {
            if (a.Length != b.Length)
                return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] is not HitSampleInfo left || b[i] is not HitSampleInfo right)
                {
                    if (!ReferenceEquals(a[i], b[i]))
                        return false;

                    continue;
                }

                if (!string.Equals(left.Name, right.Name, StringComparison.Ordinal)
                    || !string.Equals(left.Bank, right.Bank, StringComparison.Ordinal)
                    || !string.Equals(left.Suffix, right.Suffix, StringComparison.Ordinal)
                    || left.Volume != right.Volume
                    || left.UseBeatmapSamples != right.UseBeatmapSamples)
                    return false;
            }

            return true;
        }

        private void ensureNodes()
        {
            if (!nodesDirty)
                return;

            IReadOnlyList<HitSoundingNode> nodes = model.Nodes;

            crossingNodes.Clear();
            bodyNodes.Clear();

            foreach (var node in nodes)
            {
                if (node.IsBody)
                    bodyNodes.Add(node);
                else
                    crossingNodes.Add(node);
            }

            nodesDirty = false;
        }

        private void onModelChanged()
        {
            nodesDirty = true;
            sampleCache.Clear();
            loopSampleCache.Clear();

            stopLoops();
            stopHitPlayers();
            stopAudition();
        }

        private void onScreenStateChanged(ValueChangedEvent<Visibility> state)
        {
            screenVisible = state.NewValue == Visibility.Visible;
            ResetPlayback();
        }

        protected override void Dispose(bool isDisposing)
        {
            model.Changed -= onModelChanged;
            model.SamplesEdited -= onSamplesEdited;

            if (screenState != null)
            {
                screenState.ValueChanged -= onScreenStateChanged;
                screenState = null;
            }

            stopLoops();
            stopHitPlayers();
            stopAudition();

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// A reusable skinnable sound that defers one playback request until its samples have been loaded for the current skin.
        /// </summary>
        /// <remarks>
        /// Assigning <see cref="SkinnableSound.Samples"/> creates new pooled sample drawables. Those drawables resolve the skin sample
        /// during their first update, which happens after this player's own update. Playing from <see cref="UpdateAfterChildren"/>
        /// therefore guarantees the first hit is audible instead of silently playing an empty sample container.
        /// </remarks>
        private partial class SamplePlayer : PausableSkinnableSound
        {
            /// <summary>
            /// Whether the player is unavailable for reuse.
            /// </summary>
            public bool Busy => playPending || IsPlaying;

            /// <summary>
            /// Whether this player is held by an active slider loop.
            /// </summary>
            public bool Reserved;

            private bool playPending;

            public SamplePlayer(bool looping = false)
            {
                Looping = looping;
            }

            public void QueuePlay()
            {
                playPending = true;
            }

            public override void Stop()
            {
                playPending = false;
                base.Stop();
            }

            protected override void UpdateAfterChildren()
            {
                base.UpdateAfterChildren();

                if (!playPending)
                    return;

                playPending = false;
                Play();
            }
        }

        /// <summary>
        /// A local sample playback switch. The editor-wide switch is always enabled outside compose mode, so hitsounding uses its own
        /// to allow previews without unmuting the hidden compose screen.
        /// </summary>
        private sealed class SamplePlaybackDisabler : ISamplePlaybackDisabler
        {
            private readonly BindableBool disabled = new BindableBool(true);

            public IBindable<bool> SamplePlaybackDisabled => disabled;

            public void Set(bool value) => disabled.Value = value;
        }
    }
}
