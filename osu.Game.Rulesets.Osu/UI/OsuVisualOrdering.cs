// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;

namespace osu.Game.Rulesets.Osu.UI
{
    /// <summary>
    /// Orders visual proxies while their sources retain input, transforms and pooling ownership.
    /// </summary>
    public partial class OsuVisualOrdering : CompositeDrawable
    {
        private readonly OrderedProxyContainer ordered = new OrderedProxyContainer { RelativeSizeAxes = Axes.Both };
        private readonly List<VisualEntry> entries = new List<VisualEntry>();
        private readonly Dictionary<Drawable, VisualEntry> sources = new Dictionary<Drawable, VisualEntry>();
        private readonly Dictionary<HitObject, int> declarationOrder = new Dictionary<HitObject, int>();
        private readonly Bindable<bool> stableRendering = new Bindable<bool>();
        private readonly Bindable<bool> showHiddenSliders = new Bindable<bool>();
        private IBeatmap? beatmap;
        private bool orderDirty;
        private ConcurrentQueue<Drawable>? removals = new ConcurrentQueue<Drawable>();

        public bool IsActive => stableRendering.Value;
        public bool IsStableRendering => IsActive;

        /// <summary>
        /// Whether each proxy's visibility is checked against the lifetimes of its owner and parent object every frame (see <see cref="LifetimeViolations"/>).
        /// Enabled with the <c>G0V0_CHECK_VISUAL_LIFETIMES</c> environment variable set to 1, or in tests via <see cref="CheckLifetimes"/>.
        /// </summary>
        public bool CheckLifetimes { get; set; } = Environment.GetEnvironmentVariable(@"G0V0_CHECK_VISUAL_LIFETIMES") == @"1";

        /// <summary>
        /// Descriptions of proxies found visible outside their objects' lifetimes, or hidden within them. Capped at <see cref="max_recorded_violations"/>.
        /// </summary>
        public IReadOnlyList<string> LifetimeViolations => lifetimeViolations;

        private const int max_recorded_violations = 100;
        private const int max_logged_violations = 20;

        /// <summary>
        /// One frame at 60 Hz, the frame-stable gameplay rate: how long visibility may trail a lifetime boundary.
        /// </summary>
        private const double lifetime_tolerance = 1000 / 60.0;

        private readonly List<string> lifetimeViolations = new List<string>();
        private readonly HashSet<(Drawable, string)> reportedViolations = new HashSet<(Drawable, string)>();

        public void Unregister(Drawable source) => Volatile.Read(ref removals)?.Enqueue(source);

        public OsuVisualOrdering()
        {
            RelativeSizeAxes = Axes.Both;
            AlwaysPresent = true;
            InternalChild = ordered;
        }

        [BackgroundDependencyLoader]
        private void load(OsuRulesetConfigManager? config, IBeatmap? beatmap)
        {
            config?.BindWith(OsuRulesetSetting.KeepOverlapCirclesVisible, stableRendering);
            config?.BindWith(OsuRulesetSetting.ShowHiddenSliders, showHiddenSliders);
            this.beatmap = beatmap;
            RefreshBeatmap();
        }

        public void RefreshBeatmap()
        {
            declarationOrder.Clear();
            if (beatmap == null)
                return;

            for (int i = 0; i < beatmap.HitObjects.Count; i++)
                declarationOrder[beatmap.HitObjects[i]] = i;

            orderDirty = true;
        }

        public void Register(Drawable source, DrawableOsuHitObject owner, OsuVisualKind kind, Func<Container?>? fallback = null,
                             float localDepth = 0, bool overlayAboveNumber = true, Drawable? existingProxy = null,
                             Action<Drawable>? addFallback = null, Action<Drawable>? removeFallback = null,
                             Action<Drawable, float>? changeFallbackDepth = null)
        {
            if (!IsActive)
                return;

            ObjectDisposedException.ThrowIf(Volatile.Read(ref removals) == null, this);
            ObjectDisposedException.ThrowIf(owner.IsVisualSourceDisposed, owner);

            // A source can only create one proxy, even after that proxy has been disposed.
            if (sources.ContainsKey(source))
                return;

            var entry = new VisualEntry(source, owner, kind, fallback, overlayAboveNumber, addFallback, removeFallback, changeFallbackDepth)
            {
                Gate = new VisualProxy
                {
                    RelativeSizeAxes = Axes.Both,
                    Depth = localDepth,
                    Child = existingProxy ?? source.CreateProxy(),
                },
            };
            entries.Add(entry);
            sources.Add(source, entry);
            refresh(entry);
            orderDirty = true;
        }

        protected override void Update()
        {
            base.Update();

            if (!IsActive)
                return;

            bool sortRequired = drainRemovals();
            sortRequired |= cleanDeadEntries();
            sortRequired |= orderDirty;
            orderDirty = false;

            for (int i = 0; i < entries.Count; i++)
                sortRequired |= refresh(entries[i]);

            if (sortRequired)
                ordered.RefreshOrder();
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            if (!IsActive)
                return;

            if (drainRemovals())
                ordered.RefreshOrder();
        }

        private bool drainRemovals()
        {
            bool removedAny = false;
            var pendingRemovals = Volatile.Read(ref removals);

            while (pendingRemovals != null && pendingRemovals.TryDequeue(out var source))
            {
                if (!sources.Remove(source, out var removed))
                    continue;

                detach(removed.Gate, removed);
                removed.Gate.Dispose();

                entries.Remove(removed);
                removedAny = true;
            }

            return removedAny;
        }

        private bool cleanDeadEntries()
        {
            bool removedAny = false;

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];

                if (entry.Owner.IsVisualSourceDisposed || entry.Gate.IsProxyDisposed)
                {
                    detach(entry.Gate, entry);
                    entry.Gate.Dispose();

                    sources.Remove(entry.Source);
                    entries.RemoveAt(i);
                    removedAny = true;
                }
            }

            return removedAny;
        }

        private bool refresh(VisualEntry entry)
        {
            if (entry.Gate.IsProxyDisposed || entry.Owner.IsVisualSourceDisposed)
                return false;

            var owner = entry.Owner;
            var parent = owner.VisualParentHitObject ?? owner;
            var model = owner.HitObject;
            var parentModel = parent.HitObject;
            bool applied = owner.Entry != null && model != null && parentModel != null;
            bool hidden = parentModel is Slider slider && slider.Duration < 1;

            // A nested object keeps its last IsAlive value once its parent leaves the hit object container, as nothing updates it any more.
            bool parentAlive = ReferenceEquals(parent, owner) || parent.IsAlive;
            bool repeatFinished = false;
            if (owner is DrawableSliderRepeat repeat && repeat.AllJudged)
            {
                double animDuration = Math.Min(300, repeat.HitObject.SpanDuration);
                repeatFinished = Time.Current >= repeat.HitStateUpdateTime + animDuration;
            }

            bool approachFinished = entry.Kind == OsuVisualKind.Approach && (owner.Judged || (model != null && Time.Current > model.StartTime + 50));

            bool visible = applied && owner.IsAlive && parentAlive && (showHiddenSliders.Value || !hidden) && !repeatFinished && !approachFinished;

            entry.Gate.Alpha = visible ? 1f : 0;

            if (CheckLifetimes)
                checkLifetime(entry, parent, applied, visible, hidden, repeatFinished, approachFinished);

            if (!applied)
            {
                entry.Model = null;
                entry.ParentModel = null;
                entry.EventOrdinal = 0;
                return false;
            }

            int declaration = declarationOrder.GetValueOrDefault(parentModel!);
            if (entry.Kind == OsuVisualKind.Approach && entry.Gate.Depth != declaration)
            {
                if (entry.Gate.Parent == null)
                    entry.Gate.Depth = declaration;
                else if (entry.ChangeFallbackDepth != null && entry.InFallback)
                    entry.ChangeFallbackDepth(entry.Gate, declaration);
                else if (entry.Gate.Parent is Container container)
                    container.ChangeChildDepth(entry.Gate, declaration);
                else
                    entry.Gate.Depth = declaration;
            }
            bool tail = owner is DrawableSliderTail;
            int role = owner switch
            {
                DrawableSliderTail => 0,
                DrawableSliderHead => 2,
                DrawableSliderRepeat => 3,
                _ => 1,
            };
            double time = entry.Kind == OsuVisualKind.Body ? parentModel!.GetEndTime() : model!.StartTime;
            if (tail)
                time = parentModel!.GetEndTime();

            int phase = entry.Kind switch
            {
                OsuVisualKind.Object => -2,
                OsuVisualKind.Body => 0,
                OsuVisualKind.CircleGroup => 1,
                OsuVisualKind.CircleBase => 1,
                OsuVisualKind.Number => entry.OverlayAboveNumber ? 2 : 3,
                OsuVisualKind.Overlay => entry.OverlayAboveNumber ? 3 : 2,
                OsuVisualKind.Arrow => 4,
                OsuVisualKind.Flash => 4,
                OsuVisualKind.Tick => 5,
                _ => 0,
            };

            if (!ReferenceEquals(entry.Model, model) || !ReferenceEquals(entry.ParentModel, parentModel))
            {
                entry.Model = model;
                entry.ParentModel = parentModel;
                entry.EventOrdinal = 0;
                if (!ReferenceEquals(parent, owner))
                {
                    foreach (var nested in parentModel!.NestedHitObjects)
                    {
                        if (ReferenceEquals(nested, model))
                            break;
                        entry.EventOrdinal++;
                    }
                }
            }

            var key = new VisualKey(
                entry.Kind == OsuVisualKind.Ball ? 1 : 0,
                double.IsFinite(time) ? time : 0,
                phase,
                tail ? -declaration : declaration,
                role,
                entry.EventOrdinal,
                (int)entry.Kind);

            bool changed = !key.Equals(entry.Gate.Key);
            if (changed)
            {
                entry.Gate.Key = key;
                ordered.InvalidateOrder();
            }

            bool global = entry.Kind == OsuVisualKind.Object || stableRendering.Value && entry.Kind != OsuVisualKind.Approach;
            bool moved = false;

            if (!global && entry.AddFallback != null)
            {
                if (!entry.InFallback)
                {
                    detach(entry.Gate, entry);
                    entry.AddFallback(entry.Gate);
                    entry.InFallback = true;
                    moved = true;
                }
            }
            else
            {
                Container? target = global ? ordered : entry.Fallback?.Invoke();
                if (!ReferenceEquals(entry.Gate.Parent, target))
                {
                    detach(entry.Gate, entry);
                    target?.Add(entry.Gate);
                    entry.InFallback = false;
                    moved = true;
                }
            }

            return moved || changed;
        }

        /// <summary>
        /// Compares a proxy's visibility with the lifetimes of its owner and parent object, which the hit object container is expected to apply,
        /// and checks that the proxied source still belongs to its owner.
        /// </summary>
        private void checkLifetime(VisualEntry entry, DrawableOsuHitObject parent, bool applied, bool visible, bool hidden, bool repeatFinished, bool approachFinished = false)
        {
            var owner = entry.Owner;
            double time = Time.Current;

            bool withinLifetime(Drawable drawable) => time >= drawable.LifetimeStart && time < drawable.LifetimeEnd;

            // In the editor, HitObjectContainer extends lifetimes across the timeline visible range (PastLifetimeExtension / FutureLifetimeExtension),
            // keeping drawables alive well outside their normal [LifetimeStart, LifetimeEnd] window.
            if (owner.IsAlive && !withinLifetime(owner))
                return;

            bool expected = applied
                            && withinLifetime(owner)
                            && (ReferenceEquals(parent, owner) || withinLifetime(parent))
                            && (showHiddenSliders.Value || !hidden)
                            && !repeatFinished
                            && !approachFinished;

            // Containers update their children's IsAlive before those children change their own lifetimes (on judgement, or when a seek
            // reverts one), so visibility can trail a lifetime change by one frame, as it does in upstream gameplay. While seeking, the
            // clock can also sit just past a boundary for several frames before the container updates again. Only a mismatch lasting
            // longer than a frame and further than a frame's time from every boundary is visible to the player.
            bool nearBoundary = Math.Abs(time - owner.LifetimeStart) < lifetime_tolerance || Math.Abs(time - owner.LifetimeEnd) < lifetime_tolerance
                                || Math.Abs(time - parent.LifetimeStart) < lifetime_tolerance || Math.Abs(time - parent.LifetimeEnd) < lifetime_tolerance;

            if (visible != expected && !nearBoundary)
                entry.MismatchedFrames++;
            else
                entry.MismatchedFrames = 0;

            if (entry.MismatchedFrames == 2)
                report(entry, visible ? @"visible outside its lifetime" : @"hidden within its lifetime", parent, time);

            if (!visible)
                return;

            if (!ReferenceEquals(parent, owner) && parent.HitObject != null && owner.HitObject != null && !parent.HitObject.NestedHitObjects.Contains(owner.HitObject))
                report(entry, @"shows a nested object that is not part of its parent", parent, time);

            if (!isDescendantOf(entry.Source, owner))
                report(entry, @"proxies a source detached from its owner", parent, time);
        }

        private static bool isDescendantOf(Drawable drawable, Drawable ancestor)
        {
            for (Drawable? current = drawable; current != null; current = current.Parent)
            {
                if (ReferenceEquals(current, ancestor))
                    return true;
            }

            return false;
        }

        private void report(VisualEntry entry, string problem, DrawableOsuHitObject parent, double time)
        {
            // One report per source and problem, so that a lasting fault does not flood the log every frame.
            if (!reportedViolations.Add((entry.Source, problem)))
                return;

            string message = $@"{entry.Kind} proxy of {entry.Owner.GetType().Name} {problem} at {time:0.##} ms: "
                             + $@"owner lifetime {entry.Owner.LifetimeStart:0.##}-{entry.Owner.LifetimeEnd:0.##}, alive {entry.Owner.IsAlive}, "
                             + $@"parent {parent.GetType().Name} lifetime {parent.LifetimeStart:0.##}-{parent.LifetimeEnd:0.##}, alive {parent.IsAlive}, "
                             + $@"object start {entry.Owner.HitObject?.StartTime:0.##}.";

            if (lifetimeViolations.Count < max_recorded_violations)
                lifetimeViolations.Add(message);

            if (lifetimeViolations.Count <= max_logged_violations)
                Logger.Log($@"Visual ordering: {message}", LoggingTarget.Runtime, LogLevel.Debug);
        }

        private void detach(VisualProxy gate, VisualEntry? entry = null)
        {
            if (ReferenceEquals(gate.Parent, ordered))
                ordered.Remove(gate, false);
            else if (entry?.RemoveFallback != null && entry.InFallback)
            {
                entry.RemoveFallback(gate);
                entry.InFallback = false;
            }
            else if (gate.Parent is Container container)
                container.Remove(gate, false);
        }

        protected override void Dispose(bool isDisposing)
        {
            // Skin components may unregister from the asynchronous disposal queue after us.
            // Drop the queue itself so those notifications cannot retain disposed sources.
            Interlocked.Exchange(ref removals, null);

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                // Attached gates are owned by their parent (ordered or a fallback container).
                // Disposal can run off the update thread, where removing children is forbidden.
                if (entry.Gate.Parent == null)
                    entry.Gate.Dispose();
            }

            entries.Clear();
            sources.Clear();
            reportedViolations.Clear();
            declarationOrder.Clear();
            beatmap = null;

            base.Dispose(isDisposing);
        }

        private sealed class VisualEntry
        {
            public readonly Drawable Source;
            public readonly DrawableOsuHitObject Owner;
            public readonly OsuVisualKind Kind;
            public readonly Func<Container?>? Fallback;
            public readonly Action<Drawable>? AddFallback;
            public readonly Action<Drawable>? RemoveFallback;
            public readonly Action<Drawable, float>? ChangeFallbackDepth;
            public readonly bool OverlayAboveNumber;
            public bool InFallback;
            public VisualProxy Gate = null!;
            public HitObject? Model;
            public HitObject? ParentModel;
            public int EventOrdinal;
            public int MismatchedFrames;

            public VisualEntry(Drawable source, DrawableOsuHitObject owner, OsuVisualKind kind, Func<Container?>? fallback, bool overlayAboveNumber,
                               Action<Drawable>? addFallback = null, Action<Drawable>? removeFallback = null, Action<Drawable, float>? changeFallbackDepth = null)
            {
                Source = source;
                Owner = owner;
                Kind = kind;
                Fallback = fallback;
                OverlayAboveNumber = overlayAboveNumber;
                AddFallback = addFallback;
                RemoveFallback = removeFallback;
                ChangeFallbackDepth = changeFallbackDepth;
            }
        }

        private readonly record struct VisualKey(int Group, double Time, int Phase, int Declaration, int Role, int EventOrdinal, int Piece) : IComparable<VisualKey>
        {
            public int CompareTo(VisualKey other)
            {
                int result = Group.CompareTo(other.Group);
                if (result == 0) result = other.Time.CompareTo(Time);
                if (result == 0) result = Phase.CompareTo(other.Phase);
                if (result == 0) result = Declaration.CompareTo(other.Declaration);
                if (result == 0) result = Role.CompareTo(other.Role);
                if (result == 0) result = EventOrdinal.CompareTo(other.EventOrdinal);
                if (result == 0) result = Piece.CompareTo(other.Piece);
                return result;
            }
        }

        private partial class VisualProxy : Container
        {
            public VisualKey Key;
            public override bool RemoveWhenNotAlive => false;
            public bool IsProxyDisposed => IsDisposed;

            protected override bool CheckChildrenLife()
            {
                // Keep the single proxy for the whole registration. Its source may expire and
                // become alive again after a rewind or reuse, and CreateProxy() cannot be repeated.
                if (Child.IsAlive || Child.LoadState < LoadState.Ready)
                    return false;

                MakeChildAlive(Child);
                return true;
            }
        }

        private partial class OrderedProxyContainer : Container
        {
            private bool orderInvalid;

            public void InvalidateOrder() => orderInvalid = true;

            public void RefreshOrder()
            {
                if (!orderInvalid)
                    return;

                SortInternal();
                orderInvalid = false;
            }

            protected override void AddInternal(Drawable drawable)
            {
                RefreshOrder();
                base.AddInternal(drawable);
            }

            protected override bool RemoveInternal(Drawable drawable, bool disposeImmediately)
            {
                // Both child lists use binary search. Keys may have changed earlier in the
                // same update, so restore their order before removing from either list.
                RefreshOrder();
                return base.RemoveInternal(drawable, disposeImmediately);
            }

            protected override int Compare(Drawable x, Drawable y)
            {
                int result = ((VisualProxy)x).Key.CompareTo(((VisualProxy)y).Key);
                return result != 0 ? result : CompareReverseChildID(x, y);
            }
        }
    }
}
