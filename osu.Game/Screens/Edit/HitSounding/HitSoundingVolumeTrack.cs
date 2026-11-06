// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Rulesets.Objects.Drawables;
using osuTK;
using osuTK.Input;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// Draws the volume of every <see cref="HitSoundingNode"/> over time as a step graph, and edits it by dragging,
    /// double-clicking or Ctrl-clicking a run of nodes sharing the same volume.
    /// </summary>
    /// <remarks>
    /// Only the runs inside the visible window (plus <see cref="attach_margin"/>) own a segment; every other run
    /// keeps its nodes and volume for hit-testing and editing but costs nothing to display. Segments of runs leaving
    /// the window are pooled and rebound to the runs entering it, so scrolling a long map neither creates nor disposes
    /// controls.
    /// </remarks>
    public partial class HitSoundingVolumeTrack : CompositeDrawable, IHasCustomTooltip<LocalisableString>
    {
        /// <summary>
        /// How far outside the visible window, in milliseconds, runs are materialised ahead of time. A newly attached
        /// segment only becomes drawable on the following update, so this keeps one update of slack; it is also what
        /// bounds how many segments exist at once on a long map.
        /// </summary>
        private const double attach_margin = 200;

        private readonly HitSoundingEditModel model;

        private readonly List<VolumeRun> runs = [];

        /// <summary>
        /// The segments currently holding a place in the draw tree, ordered by time. Only the runs inside the
        /// visible window (plus the prefetch margin) own a segment, so this is not the whole map.
        /// </summary>
        private readonly List<HitSoundingVolumeSegment> segments = [];

        /// <summary>
        /// Segments detached from the draw tree, waiting to be bound to a run which enters the visible window.
        /// </summary>
        private readonly Stack<HitSoundingVolumeSegment> segmentPool = [];

        private readonly HashSet<HitSoundingNode> selectedNodes = [];

        private Container<HitSoundingVolumeSegment> segmentLayer = null!;
        private CircularContainer previewDot = null!;
        private PopoverAnchor popoverAnchor = null!;
        private InputManager inputManager = null!;

        private bool dirty = true;
        private bool layoutPending;
        private bool updatingSelection;
        private bool controlHeld;
        private double laidOutStartTime;
        private double laidOutEndTime;
        private float laidOutWidth = -1;

        /// <summary>
        /// The half-open range of <see cref="runs"/> which currently owns a segment, in run order.
        /// </summary>
        private int materialisedFirst;
        private int materialisedEnd;
        private VolumeRun? hoveredRun;
        private VolumeRun? dragRun;
        private HitSoundingNode[]? dragNodes;
        private int dragIndex = -1;
        private int dragInitialVolume;
        private int previewVolume;

        /// <summary>
        /// The left edge of the time window displayed by the parent editor.
        /// </summary>
        public double VisibleStartTime { get; set; }

        /// <summary>
        /// The right edge of the time window displayed by the parent editor.
        /// </summary>
        public double VisibleEndTime { get; set; }

        /// <summary>
        /// The segments currently materialised for the visible window, ordered by time. Runs outside of the window
        /// and its prefetch margin keep their nodes and volume for hit-testing and editing, but own no drawable and
        /// therefore do not appear here.
        /// </summary>
        public IReadOnlyList<HitSoundingVolumeSegment> Segments => segments;

        public HitSoundingVolumeTrack(HitSoundingEditModel model)
        {
            this.model = model;

            RelativeSizeAxes = Axes.X;
            Height = 100;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, OverlayColourProvider colourProvider)
        {
            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.X, Height = 1, Y = HitSoundingVolumeSegment.VolumeToY(100), Colour = colourProvider.Background4 },
                new Box { RelativeSizeAxes = Axes.X, Height = 1, Y = HitSoundingVolumeSegment.VolumeToY(50), Colour = colourProvider.Background4 },
                segmentLayer = new Container<HitSoundingVolumeSegment>
                {
                    RelativeSizeAxes = Axes.Both,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = previewDot = new CircularContainer
                    {
                        Size = new Vector2(12),
                        Masking = true,
                        BorderThickness = 2,
                        BorderColour = Colour4.White,
                        Alpha = 0,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colours.Pink1,
                        }
                    }
                },
                popoverAnchor = new PopoverAnchor(),
            };

            model.Changed += onModelChanged;
            model.SelectedNodes.CollectionChanged += onSelectionCollectionChanged;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            inputManager = GetContainingInputManager()!;
            Refresh();
        }

        /// <summary>
        /// Rebuilds the graph if the beatmap changed, then repositions it for the current visible time window.
        /// Call this whenever <see cref="VisibleStartTime"/> or <see cref="VisibleEndTime"/> changes.
        /// </summary>
        public void Refresh()
        {
            if (!IsLoaded)
            {
                dirty = true;
                return;
            }

            if (dirty)
                rebuild();

            layout();
        }

        /// <summary>
        /// Maps a beatmap time to the horizontal coordinate used by this drawable.
        /// </summary>
        public float TimeToX(double time)
        {
            double span = VisibleEndTime - VisibleStartTime;

            if (span <= 0)
                return 0;

            return (float)((time - VisibleStartTime) / span * DrawWidth);
        }

        /// <summary>
        /// Maps a volume to the vertical coordinate used by this drawable.
        /// </summary>
        public float YForVolume(int volume) => HitSoundingVolumeSegment.VolumeToY(volume);

        /// <summary>
        /// The shortcuts to show while hovering a run of the volume graph.
        /// </summary>
        public LocalisableString TooltipContent
        {
            get
            {
                if (hoveredRun == null)
                    return default;

                return previewVisible ? HitSoundingStrings.CtrlSplitHint : HitSoundingStrings.VolumeHint;
            }
        }

        public ITooltip<LocalisableString> GetCustomTooltip() => new HitSoundingTooltip(previewVisible ? HitSoundingTooltipKind.VolumeSplit : HitSoundingTooltipKind.Volume);

        private bool previewVisible => previewDot.Alpha > 0;

        private void onModelChanged()
        {
            dirty = true;

            if (IsLoaded)
                Refresh();
        }

        private void onSelectionCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (updatingSelection)
                return;

            updateSelectionCache();
            updateSelectionVisuals();
        }

        private void rebuild()
        {
            dirty = false;
            layoutPending = true;

            resetDrag();
            setHovered(null);

            // the runs are rebuilt from scratch, so every segment goes back to the pool; layout() binds the ones the
            // current window needs and hands the rest to whatever enters the window next.
            releaseRuns();

            runs.Clear();

            foreach (var node in model.Nodes)
            {
                VolumeRun? run = runs.Count > 0 ? runs[^1] : null;

                if (run == null || run.Volume != node.Volume)
                {
                    run = new VolumeRun(node.Time, node.Volume);
                    runs.Add(run);
                }

                run.Nodes.Add(node);

                // the last run ends at its final node, earlier runs are extended to the start of the next one below.
                run.EndTime = node.Time;
            }

            for (int i = 0; i + 1 < runs.Count; i++)
                runs[i].EndTime = runs[i + 1].StartTime;

            updateSelectionCache();
        }

        private void layout()
        {
            float width = DrawWidth;

            if (width <= 0 || VisibleEndTime <= VisibleStartTime)
                return;

            // a refresh can arrive without the window changing (a control point edit, a vertical resize), so only
            // reposition the graph when the mapping it uses actually changed or the runs were rebuilt.
            if (!layoutPending && width == laidOutWidth && VisibleStartTime == laidOutStartTime && VisibleEndTime == laidOutEndTime)
                return;

            layoutPending = false;
            laidOutWidth = width;
            laidOutStartTime = VisibleStartTime;
            laidOutEndTime = VisibleEndTime;

            double visibleStart = VisibleStartTime;
            double visibleEnd = VisibleEndTime;

            // the mapping is affine and identical for every run, so derive its scale once instead of dividing per run.
            double scale = width / (visibleEnd - visibleStart);

            // only the runs around the window own a segment; the margin materialises them ahead of the moment they
            // can be drawn, so they are already alive by the time they enter the window.
            double attachStart = visibleStart - attach_margin;
            double attachEnd = visibleEnd + attach_margin;

            // run bounds are sorted, so the runs which own a segment form a contiguous slice that a binary search
            // finds in a handful of steps instead of scanning every run of the map on each scroll.
            int first = firstRunEndingAtOrAfter(attachStart);
            int end = firstRunStartingAtOrAfter(attachEnd);

            releaseRunsOutside(first, end);
            materialiseRuns(first, end);

            segments.Clear();

            // the run before the window still anchors the first materialised run's connector, so start from its line
            // rather than from the left edge of the graph.
            float previousY = first > 0 ? HitSoundingVolumeSegment.VolumeToY(runs[first - 1].DisplayVolume) : 0;

            for (int i = first; i < end; i++)
            {
                VolumeRun run = runs[i];
                HitSoundingVolumeSegment segment = run.Segment!;
                float y = HitSoundingVolumeSegment.VolumeToY(run.DisplayVolume);

                segment.UpdateLayout((float)((run.StartTime - visibleStart) * scale), (float)((run.EndTime - visibleStart) * scale),
                    y, previousY, i > 0, width);

                segments.Add(segment);
                previousY = y;
            }
        }

        /// <summary>
        /// Returns the segment of every run which is no longer wanted to the pool, so entering runs can take it over.
        /// </summary>
        private void releaseRunsOutside(int first, int end)
        {
            for (int i = materialisedFirst; i < materialisedEnd; i++)
            {
                if (i < first || i >= end)
                    detach(runs[i]);
            }

            materialisedFirst = first;
            materialisedEnd = end;
        }

        /// <summary>
        /// Returns every materialised segment to the pool, leaving nothing in the draw tree.
        /// </summary>
        private void releaseRuns()
        {
            for (int i = materialisedFirst; i < materialisedEnd; i++)
                detach(runs[i]);

            materialisedFirst = 0;
            materialisedEnd = 0;
            segments.Clear();
        }

        /// <summary>
        /// Binds a pooled segment to every run inside the window which does not own one yet, so runs entering the
        /// window take over the controls of the runs which left it instead of creating new ones.
        /// </summary>
        private void materialiseRuns(int first, int end)
        {
            for (int i = first; i < end; i++)
            {
                VolumeRun run = runs[i];

                if (run.Segment != null)
                    continue;

                HitSoundingVolumeSegment segment = segmentPool.Count > 0
                    ? segmentPool.Pop()
                    : new HitSoundingVolumeSegment();

                // depth may only be assigned while the segment is outside of a parent, and gives later runs priority.
                segment.Depth = -i;

                segment.Bind(run.Nodes, run.Volume, isRunSelected(run), i > 0);
                segment.SetDisplayVolume(run.DisplayVolume);

                run.Segment = segment;
                segmentLayer.Add(segment);
            }
        }

        /// <summary>
        /// Removes a run's segment from the draw tree and hands it to the pool.
        /// </summary>
        private void detach(VolumeRun run)
        {
            HitSoundingVolumeSegment? segment = run.Segment;

            if (segment == null)
                return;

            run.Segment = null;

            if (ReferenceEquals(hoveredRun, run))
                hoveredRun = null;

            segmentLayer.Remove(segment, false);
            segmentPool.Push(segment);
        }

        /// <summary>
        /// Finds the first run which ends at or after <paramref name="time"/>. End times are sorted, so the runs
        /// before it end before the time and cannot be displayed. The bound is inclusive so a zero-length run lying
        /// exactly on it is still materialised; it stays transparent until the window reaches it.
        /// </summary>
        private int firstRunEndingAtOrAfter(double time)
        {
            int low = 0;
            int high = runs.Count;

            while (low < high)
            {
                int mid = low + (high - low) / 2;

                if (runs[mid].EndTime < time)
                    low = mid + 1;
                else
                    high = mid;
            }

            return low;
        }

        /// <summary>
        /// Finds the first run whose start is at or after <paramref name="time"/>. Start times are sorted, so every
        /// run from here on begins after the time and cannot be displayed.
        /// </summary>
        private int firstRunStartingAtOrAfter(double time)
        {
            int low = 0;
            int high = runs.Count;

            while (low < high)
            {
                int mid = low + (high - low) / 2;

                if (runs[mid].StartTime >= time)
                    high = mid;
                else
                    low = mid + 1;
            }

            return low;
        }

        private double xToTime(float x)
        {
            double span = VisibleEndTime - VisibleStartTime;

            if (span <= 0 || DrawWidth <= 0)
                return VisibleStartTime;

            return VisibleStartTime + x / DrawWidth * span;
        }

        /// <summary>
        /// Finds the run under a position. Runs which are completely overlapped in time (multiple nodes at the same
        /// time with different volumes) are told apart by the vertical distance to their step line.
        /// </summary>
        /// <remarks>
        /// Only the runs owning a segment are considered: the track is only hit while the cursor is inside it, and
        /// every run which can be under the cursor there is inside the window and therefore materialised. That keeps
        /// hovering bounded by the visible window rather than by the length of the map.
        /// </remarks>
        private VolumeRun? findRun(Vector2 position)
        {
            if (runs.Count == 0)
                return null;

            double time = xToTime(position.X);

            VolumeRun? best = null;
            double bestDistance = double.MaxValue;

            for (int i = materialisedFirst; i < materialisedEnd; i++)
            {
                VolumeRun run = runs[i];

                bool inside = time >= run.StartTime && (time < run.EndTime || (i == runs.Count - 1 && time <= run.EndTime));
                bool nearStart = Math.Abs(position.X - TimeToX(run.StartTime)) <= 6;

                if (!inside && !nearStart)
                    continue;

                double distance = Math.Abs(position.Y - HitSoundingVolumeSegment.VolumeToY(run.DisplayVolume));

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = run;
                }
            }

            return best;
        }

        private HitSoundingNode? findNearestNode(VolumeRun run, float x)
        {
            HitSoundingNode? best = null;
            float bestDistance = float.MaxValue;

            foreach (var node in run.Nodes)
            {
                float distance = Math.Abs(TimeToX(node.Time) - x);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = node;
                }
            }

            return best;
        }

        private int indexOfNode(VolumeRun run, HitSoundingNode node)
        {
            for (int i = 0; i < run.Nodes.Count; i++)
            {
                if (ReferenceEquals(run.Nodes[i], node))
                    return i;
            }

            return -1;
        }

        private void selectRun(VolumeRun run)
        {
            if (model.SelectedNodes.Count == run.Nodes.Count)
            {
                bool alreadySelected = true;

                foreach (var node in run.Nodes)
                {
                    if (model.SelectedNodes.Contains(node))
                        continue;

                    alreadySelected = false;
                    break;
                }

                if (alreadySelected)
                    return;
            }

            updatingSelection = true;

            model.SelectedNodes.Clear();

            foreach (var node in run.Nodes)
                model.SelectedNodes.Add(node);

            updatingSelection = false;

            updateSelectionCache();
            updateSelectionVisuals();
        }

        private void updateSelectionCache()
        {
            selectedNodes.Clear();

            foreach (var node in model.SelectedNodes)
                selectedNodes.Add(node);
        }

        private void updateSelectionVisuals()
        {
            for (int i = materialisedFirst; i < materialisedEnd; i++)
            {
                VolumeRun run = runs[i];
                run.Segment?.SetSelected(isRunSelected(run));
            }
        }

        private bool isRunSelected(VolumeRun run)
        {
            foreach (var node in run.Nodes)
            {
                if (selectedNodes.Contains(node))
                    return true;
            }

            return false;
        }

        private void setHovered(VolumeRun? run)
        {
            if (hoveredRun == run)
                return;

            hoveredRun?.Segment?.SetGripVisible(false);
            hoveredRun = run;
            hoveredRun?.Segment?.SetGripVisible(true);
        }

        private void resetDrag()
        {
            dragRun = null;
            dragNodes = null;
            dragIndex = -1;
        }

        private void updatePreview(Vector2 position)
        {
            VolumeRun? run = findRun(position);

            if (run == null)
            {
                hidePreview();
                return;
            }

            HitSoundingNode? node = findNearestNode(run, position.X);

            if (node == null)
            {
                hidePreview();
                return;
            }

            previewDot.Position = new Vector2(TimeToX(node.Time) - previewDot.Size.X / 2, HitSoundingVolumeSegment.VolumeToY(run.DisplayVolume) - previewDot.Size.Y / 2);
            previewDot.Alpha = 1;
        }

        private void hidePreview()
        {
            previewDot.Alpha = 0;
        }

        private void openPopover(Vector2 position, HitSoundingVolumePopover popover)
        {
            resetDrag();
            hidePreview();

            popoverAnchor.Position = position;
            popoverAnchor.CreatePopover = () => popover;
            popoverAnchor.ShowPopover();

            // the container has taken ownership of the popover at this point.
            popoverAnchor.CreatePopover = null;
        }

        private void openRunPopover(VolumeRun run, float x)
        {
            HitSoundingNode[] targets = run.Nodes.ToArray();

            openPopover(new Vector2(x, HitSoundingVolumeSegment.VolumeToY(run.DisplayVolume)),
                new HitSoundingVolumePopover(model, targets, run.StartTime, run.EndTime));
        }

        private void openSplitPopover(VolumeRun run, HitSoundingNode node)
        {
            int index = indexOfNode(run, node);

            if (index < 0)
                return;

            var targets = new HitSoundingNode[run.Nodes.Count - index];

            for (int i = 0; i < targets.Length; i++)
                targets[i] = run.Nodes[index + i];

            openPopover(new Vector2(TimeToX(node.Time), HitSoundingVolumeSegment.VolumeToY(run.DisplayVolume)),
                new HitSoundingVolumePopover(model, targets, node.Time, run.EndTime));
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            Vector2 position = ToLocalSpace(e.ScreenSpaceMousePosition);
            VolumeRun? run = findRun(position);

            if (run == null)
                return false;

            if (e.Button == MouseButton.Right)
            {
                openRunPopover(run, position.X);
                return true;
            }

            if (e.Button != MouseButton.Left)
                return false;

            if (e.ControlPressed)
            {
                HitSoundingNode? node = findNearestNode(run, position.X);

                if (node != null)
                    openSplitPopover(run, node);

                hidePreview();
                return true;
            }

            selectRun(run);

            // freeze the edit targets before the model can change underneath us.
            dragRun = run;
            dragIndex = runs.IndexOf(run);
            dragNodes = run.Nodes.ToArray();
            dragInitialVolume = run.DisplayVolume;
            previewVolume = dragInitialVolume;

            return true;
        }

        protected override bool OnClick(ClickEvent e)
        {
            // Selection is handled on mouse down. Handling the click is what allows the input manager to route a
            // following double-click back to this drawable.
            return e.Button == MouseButton.Left;
        }

        protected override bool OnDoubleClick(DoubleClickEvent e)
        {
            if (e.Button != MouseButton.Left)
                return false;

            Vector2 position = ToLocalSpace(e.ScreenSpaceMousePosition);
            VolumeRun? run = findRun(position);

            if (run == null)
                return false;

            openRunPopover(run, position.X);
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e) => dragRun != null;

        protected override void OnDrag(DragEvent e)
        {
            if (dragRun == null || dragIndex < 0)
            {
                base.OnDrag(e);
                return;
            }

            int volume = Math.Clamp(HitSoundingVolumeSegment.YToVolume(ToLocalSpace(e.ScreenSpaceMousePosition).Y), DrawableHitObject.MINIMUM_SAMPLE_VOLUME, 100);

            if (volume == previewVolume)
                return;

            previewVolume = volume;
            dragRun.DisplayVolume = volume;
            dragRun.Segment?.SetDisplayVolume(volume);

            // the following run's connector starts at this run's line.
            if (dragIndex + 1 < runs.Count)
                runs[dragIndex + 1].Segment?.SetPreviousLineY(HitSoundingVolumeSegment.VolumeToY(volume));
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            if (dragRun == null)
            {
                base.OnDragEnd(e);
                return;
            }

            HitSoundingNode[]? nodes = dragNodes;
            int volume = previewVolume;
            int initialVolume = dragInitialVolume;

            resetDrag();

            if (nodes != null && volume != initialVolume)
                model.SetVolume(nodes, volume);

            // the model may not have considered the change to be a change at all, so always reconcile.
            dirty = true;
            Refresh();
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            Vector2 position = ToLocalSpace(e.ScreenSpaceMousePosition);

            setHovered(findRun(position));

            if (e.ControlPressed)
                updatePreview(position);
            else
                hidePreview();

            return base.OnMouseMove(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            setHovered(null);
            hidePreview();
            base.OnHoverLost(e);
        }

        protected override void Update()
        {
            base.Update();

            if (!IsLoaded)
                return;

            bool control = inputManager.CurrentState.Keyboard.ControlPressed;

            if (control == controlHeld)
                return;

            controlHeld = control;

            if (control && Contains(inputManager.CurrentState.Mouse.Position))
                updatePreview(ToLocalSpace(inputManager.CurrentState.Mouse.Position));
            else
                hidePreview();
        }

        protected override void Dispose(bool isDisposing)
        {
            model.Changed -= onModelChanged;
            model.SelectedNodes.CollectionChanged -= onSelectionCollectionChanged;

            if (isDisposing)
            {
                // Attached segments are disposed by the base implementation. Pooled segments have no owner in the
                // draw tree, so dispose only those here without mutating children during host shutdown.
                foreach (var segment in segmentPool)
                    segment.Dispose();

                segmentPool.Clear();
                segments.Clear();
            }

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// A single run of consecutive nodes sharing the same volume.
        /// </summary>
        private sealed class VolumeRun
        {
            public readonly List<HitSoundingNode> Nodes = [];
            public readonly int Volume;
            public readonly double StartTime;
            public double EndTime;
            public int DisplayVolume;

            /// <summary>
            /// The segment currently showing this run, or <c>null</c> while the run is outside of the window.
            /// </summary>
            public HitSoundingVolumeSegment? Segment;

            public VolumeRun(double startTime, int volume)
            {
                StartTime = startTime;
                EndTime = startTime;
                Volume = volume;
                DisplayVolume = volume;
            }
        }

        /// <summary>
        /// A zero-sized drawable positioned at the click site, used to anchor a popover to that exact position.
        /// </summary>
        private partial class PopoverAnchor : CompositeDrawable, IHasPopover
        {
            public Func<Popover>? CreatePopover { get; set; }

            public PopoverAnchor()
            {
                Size = new Vector2(1);
                Alpha = 0;
                AlwaysPresent = true;
            }

            public Popover? GetPopover() => CreatePopover?.Invoke();
        }
    }
}
