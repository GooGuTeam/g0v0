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
    public partial class HitSoundingVolumeTrack : CompositeDrawable, IHasTooltip
    {
        private static readonly LocalisableString ctrl_split_hint = HitSoundingStrings.CtrlSplitHint;

        private readonly HitSoundingEditModel model;

        private readonly List<VolumeRun> runs = [];
        private readonly List<HitSoundingVolumeSegment> segments = [];
        private readonly HashSet<HitSoundingNode> selectedNodes = [];

        private Container<HitSoundingVolumeSegment> segmentLayer = null!;
        private CircularContainer previewDot = null!;
        private PopoverAnchor popoverAnchor = null!;
        private InputManager inputManager = null!;

        private bool dirty = true;
        private bool updatingSelection;
        private bool controlHeld;
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
        /// The currently displayed runs, ordered by time.
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
                        BorderColour = colours.Pink1,
                        Alpha = 0,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Alpha = 0,
                            AlwaysPresent = true,
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

        public LocalisableString TooltipText => previewDot != null && previewDot.Alpha > 0 ? ctrl_split_hint : default;

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

            resetDrag();
            hoveredRun = null;

            runs.Clear();
            segments.Clear();
            segmentLayer.Clear();

            foreach (var node in model.Nodes)
            {
                VolumeRun? run = runs.Count > 0 ? runs[runs.Count - 1] : null;

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

            for (int i = 0; i < runs.Count; i++)
            {
                VolumeRun run = runs[i];

                run.Segment = new HitSoundingVolumeSegment(run.Nodes, run.Volume, i > 0);
                run.Segment.SetSelected(isRunSelected(run));

                segments.Add(run.Segment);
                segmentLayer.Add(run.Segment);
            }
        }

        private void layout()
        {
            if (DrawWidth <= 0 || VisibleEndTime <= VisibleStartTime)
                return;

            float previousY = 0;

            for (int i = 0; i < runs.Count; i++)
            {
                VolumeRun run = runs[i];
                float y = HitSoundingVolumeSegment.VolumeToY(run.DisplayVolume);

                run.Segment.UpdateLayout(TimeToX(run.StartTime), TimeToX(run.EndTime), y, previousY, i > 0, DrawWidth);

                previousY = y;
            }
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
        private VolumeRun? findRun(Vector2 position)
        {
            if (runs.Count == 0)
                return null;

            double time = xToTime(position.X);

            VolumeRun? best = null;
            double bestDistance = double.MaxValue;

            for (int i = 0; i < runs.Count; i++)
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
            foreach (var run in runs)
                run.Segment.SetSelected(isRunSelected(run));
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

            hoveredRun?.Segment.SetGripVisible(false);
            hoveredRun = run;
            hoveredRun?.Segment.SetGripVisible(true);
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
            if (previewDot != null)
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
            dragRun.Segment.SetDisplayVolume(volume);

            // the following run's connector starts at this run's line.
            if (dragIndex + 1 < runs.Count)
                runs[dragIndex + 1].Segment.SetPreviousLineY(HitSoundingVolumeSegment.VolumeToY(volume));
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
            base.Dispose(isDisposing);

            model.Changed -= onModelChanged;
            model.SelectedNodes.CollectionChanged -= onSelectionCollectionChanged;
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
            public HitSoundingVolumeSegment Segment = null!;

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
