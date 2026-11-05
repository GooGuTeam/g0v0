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
using osu.Game.Rulesets.Objects.Legacy;
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
    /// Four playback paths are handled independently:
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
    /// <item><description>
    /// <see cref="StartSetPreview"/> auditions a sample set over a time range through the normal clock path: it seeks the editor
    /// clock to the range start, plays through the range, and stops at its end. Only the supplied target nodes are played with an
    /// overridden sample set; every other node keeps its own samples, and the beatmap is never modified. The override is cleared
    /// as soon as the preview ends or is cancelled by a user seek, pause, scrub, screen change, edit or model change.
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
        /// The sample set index currently being auditioned by <see cref="StartSetPreview"/>, or <c>null</c> when no bounded set
        /// preview is running.
        /// </summary>
        /// <remarks>
        /// Becomes <c>null</c> as soon as the preview stops for any reason: reaching the end of its range, an explicit
        /// <see cref="StopSetPreview"/>, a user seek or pause, the screen being hidden, an explicit edit or any other beatmap
        /// change. This is the state a set preview card should use for its playing indicator.
        /// </remarks>
        public int? PreviewSetIndex { get; private set; }

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

        private readonly HashSet<HitSoundingNode> setPreviewTargets = [];
        private readonly Dictionary<HitSoundingNode, ISampleInfo[]> setPreviewSampleCache = [];
        private readonly Dictionary<HitSoundingNode, ISampleInfo[]> setPreviewLoopSampleCache = [];

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

        private bool setPreviewActive;
        private bool setPreviewStarting;
        private double setPreviewStartTime;
        private double setPreviewEndTime;

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

            // Seeks and pauses are reported by the clock itself, so a user navigation ends a bounded preview even when the
            // resulting jump is too small to look like a discontinuity.
            editorClock.SeekingOrStopped.ValueChanged += onClockSeekingOrStopped;

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
        /// Auditions a sample set over a time range through the normal editor clock.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The clock is started at <paramref name="startTime"/>, so the range is heard in its musical context exactly like normal
        /// playback. The playhead is stopped at <paramref name="endTime"/> (both ends clamped to the track length), at which point
        /// the override is cleared again.
        /// </para>
        /// <para>
        /// Only the supplied <paramref name="targets"/> have their sample set overridden. Each of their samples is copied with the
        /// requested set (<c>0</c> user skin, <c>1</c> beatmap skin, <c>&gt;= 2</c> custom set) while its bank, volume and
        /// additions are retained, so the preview sounds exactly like applying that set would. Slider body loop samples are
        /// overridden as well. Converted file samples keep their filename lookup, as the editor cannot express a set for them.
        /// Every other node, and the beatmap itself, is never touched.
        /// </para>
        /// <para>
        /// The preview ends without touching the playhead when the user pauses, seeks or scrubs, when the screen is hidden, when a
        /// node is edited, or when the beatmap changes for any other reason. Call <see cref="StopSetPreview"/> to stop it
        /// explicitly while leaving the playhead where it stopped.
        /// </para>
        /// </remarks>
        /// <param name="setIndex">The sample set to audition.</param>
        /// <param name="startTime">The time to start playback from, in milliseconds.</param>
        /// <param name="endTime">The time to stop playback at, in milliseconds.</param>
        /// <param name="targets">The nodes whose samples should use the set. Nodes not in this list keep their own samples.</param>
        public void StartSetPreview(int setIndex, double startTime, double endTime, IReadOnlyList<HitSoundingNode> targets)
        {
            if (!IsLoaded || !IsPresent || !screenVisible || beatmap.UpdateInProgress.Value)
            {
                StopSetPreview();
                return;
            }

            if (setIndex < 0 || targets.Count == 0 || double.IsNaN(startTime) || double.IsNaN(endTime))
            {
                StopSetPreview();
                return;
            }

            double trackLength = editorClock.TrackLength;
            double start = Math.Clamp(startTime, 0, trackLength);
            double end = Math.Clamp(endTime, 0, trackLength);

            if (end <= start)
            {
                StopSetPreview();
                return;
            }

            // A new preview completely replaces the previous one; no sound from before the seek may linger.
            stopAudition();
            stopLoops();
            stopHitPlayers();

            beginSetPreview(setIndex, targets);

            setPreviewActive = true;
            setPreviewStarting = true;
            setPreviewStartTime = start;
            setPreviewEndTime = end;
            PreviewSetIndex = setIndex;

            // The scrub cursor is unrelated to a bounded preview; starting playback must not replay a previous scrub session.
            previewActive = false;
            previewRequested = false;
            previewPosition = start;
            lastPlaybackTime = start;
            localDisabler.Set(false);

            // Starting first makes the seek authoritative: Start() rewinds a clock which previously played to the end.
            editorClock.Start();
            editorClock.Seek(start);
        }

        /// <summary>
        /// Stops a bounded set preview started by <see cref="StartSetPreview"/>, leaving the playhead where it stopped.
        /// </summary>
        /// <remarks>
        /// Does nothing when no set preview is running, so calling it unconditionally never interrupts normal editor playback.
        /// </remarks>
        public void StopSetPreview()
        {
            if (!setPreviewActive)
                return;

            endSetPreview(stopClock: true, stopSounds: true);
        }

        /// <summary>
        /// Clears all playback state and stops every preview and edit-audition sound.
        /// </summary>
        /// <remarks>
        /// Automatically invoked when the owning <see cref="EditorScreen"/> is hidden or shown (including editor mode switches), so
        /// callers normally do not need to invoke this. It is public so a host can force a resync after moving the playhead without
        /// scrubbing. A running bounded set preview is ended, but the clock is never started or stopped here.
        /// </remarks>
        public void ResetPlayback()
        {
            cancelSetPreview();

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
                // A hidden screen or a beatmap transaction ends the bounded preview, but the clock is left alone.
                if (setPreviewActive)
                    cancelSetPreview();

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

            if (setPreviewActive)
            {
                if (setPreviewStarting)
                {
                    // The seek performed by StartSetPreview() is not user navigation and must not end the preview. Keep the
                    // baseline at the requested start so the first audible frame covers the whole untracked interval.
                    if (editorClock.SeekingOrStopped.Value || editorClock.IsSeeking)
                    {
                        lastPlaybackTime = setPreviewStartTime;
                        previewRequested = false;
                        previewActive = false;
                        return;
                    }

                    setPreviewStarting = false;

                    // The audio track can report a position a fraction behind the requested start right after the seek. Taking
                    // the lower value keeps a node sitting exactly on the start from being skipped by the exclusive interval.
                    lastPlaybackTime = Math.Min(current, setPreviewStartTime);
                    previewRequested = false;
                    previewActive = false;
                }

                if (!playing)
                {
                    // The user paused, seeked, or the clock already ran into the track end. An explicit seek is normally handled
                    // by the clock callback; this also covers the automatic stop at the end of the track. Leave the playhead alone.
                    endSetPreview(stopClock: false, stopSounds: true);
                }
            }

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
                // A bounded preview is not cancelled here: a genuine user seek is reported by the clock callback, and the audio
                // track settling after the preview's own seek must not end it.
                lastPlaybackTime = current;
                stopLoops();
                updateLoops(current, reportStarts: false);
                return;
            }

            // A bounded preview never plays past its requested end, so the final partial frame is clamped to it.
            double playhead = setPreviewActive ? Math.Min(current, setPreviewEndTime) : current;

            crossings.Clear();
            HitSoundingSampleCrossings.Collect(crossingNodes, lastPlaybackTime, playhead, crossings);

            for (int i = 0; i < crossings.Count; i++)
            {
                HitSoundingNode node = crossings[i];

                if (playNode(node))
                    NodePlayed?.Invoke(node);
            }

            lastPlaybackTime = current;

            if (setPreviewActive && current >= setPreviewEndTime)
            {
                finishSetPreview();
                return;
            }

            updateLoops(playhead, reportStarts: true);
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
            if (setPreviewActive && setPreviewTargets.Contains(node) && setPreviewSampleCache.TryGetValue(node, out ISampleInfo[]? overridden))
                return overridden;

            if (!sampleCache.TryGetValue(node, out ISampleInfo[]? samples))
            {
                samples = copySamples(node.Samples);
                sampleCache.Add(node, samples);
            }

            return samples;
        }

        private ISampleInfo[] getLoopSamples(HitSoundingNode node)
        {
            if (setPreviewActive && setPreviewTargets.Contains(node) && setPreviewLoopSampleCache.TryGetValue(node, out ISampleInfo[]? overridden))
                return overridden;

            if (!loopSampleCache.TryGetValue(node, out ISampleInfo[]? samples))
            {
                samples = copySamples(node.HitObject.CreateSlidingSamples());
                loopSampleCache.Add(node, samples);
            }

            return samples;
        }

        /// <summary>
        /// Prepares the set override of a bounded preview. Every target resolves through its own cache entry so the override can
        /// never leak into the original sample caches, and dropping both caches at the end cannot leave a stale override behind.
        /// </summary>
        private void beginSetPreview(int setIndex, IReadOnlyList<HitSoundingNode> targets)
        {
            setPreviewTargets.Clear();
            setPreviewSampleCache.Clear();
            setPreviewLoopSampleCache.Clear();

            for (int i = 0; i < targets.Count; i++)
            {
                HitSoundingNode node = targets[i];

                setPreviewTargets.Add(node);

                if (node.IsBody)
                    setPreviewLoopSampleCache[node] = createSetOverrideSamples(node.HitObject.CreateSlidingSamples(), setIndex);
                else
                    setPreviewSampleCache[node] = createSetOverrideSamples(node.Samples, setIndex);
            }
        }

        /// <summary>
        /// Copies a sample list with the given sample set applied, retaining each sample's bank, volume and addition name.
        /// </summary>
        /// <remarks>
        /// <paramref name="setIndex"/> follows the editor convention: <c>0</c> is the user skin, <c>1</c> the beatmap skin and
        /// <c>&gt;= 2</c> a numeric custom set. Converted file samples are kept as-is: their lookup is a filename which cannot
        /// express a set, and the editor treats them as uneditable for the same reason.
        /// </remarks>
        private static ISampleInfo[] createSetOverrideSamples(IList<HitSampleInfo> samples, int setIndex)
        {
            if (samples.Count == 0)
                return Array.Empty<ISampleInfo>();

            string? suffix = setIndex >= 2 ? setIndex.ToString() : null;

            var copy = new ISampleInfo[samples.Count];

            for (int i = 0; i < samples.Count; i++)
            {
                HitSampleInfo sample = samples[i];

                copy[i] = sample is ConvertHitObjectParser.FileHitSampleInfo
                    ? sample
                    : sample.With(newSuffix: suffix, newUseBeatmapSamples: setIndex >= 1);
            }

            return copy;
        }

        /// <summary>
        /// Ends a bounded preview because its range was played through, leaving the clock stopped exactly on the range end.
        /// </summary>
        private void finishSetPreview()
        {
            double end = setPreviewEndTime;

            endSetPreview(stopClock: false, stopSounds: false);

            // The frame which crossed the end overshoots it, so land the stopped playhead on the exact boundary instead.
            editorClock.Stop();
            editorClock.Seek(end);
        }

        /// <summary>
        /// Ends a bounded preview. The override is dropped before any player is stopped, so no later frame can start a sound with it.
        /// </summary>
        /// <param name="stopClock">Whether to stop the editor clock (an explicit stop) or leave it to the user's action.</param>
        /// <param name="stopSounds">Whether to cut currently sounding preview hits as well as slider loops.</param>
        private void endSetPreview(bool stopClock, bool stopSounds)
        {
            bool wasActive = setPreviewActive;

            cancelSetPreview();

            stopLoops();

            if (stopSounds)
                stopHitPlayers();

            if (stopClock && wasActive)
                editorClock.Stop();
        }

        /// <summary>
        /// Drops the bounded preview state and every override sample. The clock and players are left alone; callers decide what to stop.
        /// </summary>
        private void cancelSetPreview()
        {
            setPreviewActive = false;
            setPreviewStarting = false;
            PreviewSetIndex = null;
            setPreviewTargets.Clear();
            setPreviewSampleCache.Clear();
            setPreviewLoopSampleCache.Clear();
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
            // Any beatmap change (an explicit edit, undo, reprocessing, ...) invalidates the previewed nodes and their samples.
            cancelSetPreview();

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

        /// <summary>
        /// Ends a bounded preview when the clock is stopped or seeked by anyone but <see cref="StartSetPreview"/> itself.
        /// </summary>
        /// <remarks>
        /// The clock reports a seek through this bindable before any of its own update logic runs, so even a small scrub which
        /// would not look like a discontinuity ends the preview. The playhead is deliberately left where the user moved it.
        /// </remarks>
        private void onClockSeekingOrStopped(ValueChangedEvent<bool> state)
        {
            if (!state.NewValue || !setPreviewActive || setPreviewStarting)
                return;

            endSetPreview(stopClock: false, stopSounds: true);
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

            if (IsLoaded)
                editorClock.SeekingOrStopped.ValueChanged -= onClockSeekingOrStopped;

            cancelSetPreview();

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
