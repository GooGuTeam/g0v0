// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osuTK;
using osuTK.Input;

namespace osu.Game.Screens.Edit.HitSounding
{
    public partial class HitSoundingRoll : CompositeDrawable, IBlockGlobalAltScrollVolume
    {
        private const float header_width = 200;
        private readonly HitSoundingEditModel model;
        private readonly Dictionary<HitSoundingNode, HitSoundingSamplePoint[]> points = [];
        private readonly Dictionary<HitSoundingSamplePoint[], Container> pointGroups = [];
        private readonly Dictionary<HitObject, HitSoundingObject> objects = [];
        private readonly Dictionary<HitObject, HitSoundingSliderPlaybackFlash[]> sliderFlashes = [];
        private readonly Stack<HitSoundingSamplePoint[]> pointPool = [];
        private readonly Stack<HitSoundingSamplePoint[]> bodyPointPool = [];
        private readonly Stack<HitSoundingSliderPlaybackFlash[]> flashPool = [];
        private readonly Stack<HitSoundingObject> objectPool = [];
        private readonly List<Box> columnBoxes = [];
        private readonly Stack<HitObject> concurrentObjects = new Stack<HitObject>();
        private readonly TimeRangeIndex nodeRange = new TimeRangeIndex();
        private readonly TimeRangeIndex objectRange = new TimeRangeIndex();
        private readonly List<HitSoundingNode> removedNodes = [];
        private readonly List<HitObject> removedObjects = [];
        private readonly HashSet<HitSoundingNode> currentNodes = [];
        private readonly HashSet<HitObject> currentObjects = [];
        private Container lanes = null!;
        private Container topLanes = null!;
        private Container backgrounds = null!;
        private Container laneSection = null!;
        private Container laneHeaders = null!;
        private Container volumeSection = null!;
        private Container volumeHeader = null!;
        private Container rangeContainer = null!;
        private Container markers = null!;
        private Container objectLayer = null!;
        private TimelineArea timelineArea = null!;
        private TimelineTickDisplay timingRuler = null!;
        private TimelineTickDisplay volumeTimingRuler = null!;
        private Box topPlayhead = null!;
        private Box samplePlayhead = null!;
        private Container selectedColumns = null!;
        private HitSoundingVolumeTrack volume = null!;
        private Box selectionBox = null!;
        private Container previewRange = null!;
        private Box previewStart = null!;
        private Box previewEnd = null!;

        [Resolved]
        private HitSoundingPreviewState previewState { get; set; } = null!;

        private Vector2? selectionStart;
        private double visibleDuration = 6000;
        private double startTime;
        private bool invalid = true;
        private bool previewingSeek;
        private double lastPreviewTime;
        private double lastTime = double.NaN;
        private double lastDuration;
        private float lastWidth;
        private float lastHeight;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private EditorBeatmap beatmap { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colours { get; set; } = null!;

        [Resolved]
        private HitSoundingSamplePlayback samplePlayback { get; set; } = null!;

        internal HitSoundingNode? HoveredNode
        {
            get
            {
                foreach (var (node, nodePoints) in points)
                {
                    foreach (var point in nodePoints)
                    {
                        if (point.IsHovered && point.IsEnabled)
                            return node;
                    }
                }

                return null;
            }
        }

        private bool cycleHoveredSet(GlobalAction action)
        {
            if (action is not (GlobalAction.EditorCycleNextBeatSnapDivisor or GlobalAction.EditorCyclePreviousBeatSnapDivisor))
                return false;

            var mouse = GetContainingInputManager()!.CurrentState.Mouse.Position;

            foreach (var nodePoints in points.Values)
            {
                foreach (var point in nodePoints)
                {
                    if (!point.IsEnabled || point.Lane is not (HitSoundingLane.SampleSet or HitSoundingLane.Additions))
                        continue;

                    if (!point.ReceivePositionalInputAt(mouse))
                        continue;

                    float direction = action == GlobalAction.EditorCycleNextBeatSnapDivisor ? 1 : -1;

                    if (GetContainingInputManager()!.CurrentState.Keyboard.ShiftPressed)
                        point.CycleBank(direction);
                    else
                        point.CycleSet(direction);

                    return true;
                }
            }

            return false;
        }

        public HitSoundingRoll(HitSoundingEditModel model)
        {
            this.model = model;
            RelativeSizeAxes = Axes.X;
            Height = HitSoundingLayout.ROLL_HEIGHT;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                createHeader(HitSoundingStrings.Objects, 0, HitSoundingLayout.OBJECTS_HEIGHT, true),
                volumeHeader = createHeader(EditorStrings.SampleVolume, HitSoundingLayout.OBJECTS_HEIGHT, HitSoundingLayout.VOLUME_HEIGHT, false),
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Left = header_width + 1 },
                    Child = timelineArea = new TimelineArea(objectLayer = new Container { RelativeSizeAxes = Axes.Both })
                    {
                        HandleBeatDivisorAction = cycleHoveredSet
                    }
                },
                volumeSection = new Container
                {
                    RelativeSizeAxes = Axes.X, Y = HitSoundingLayout.OBJECTS_HEIGHT, Height = HitSoundingLayout.VOLUME_HEIGHT,
                    Padding = new MarginPadding { Left = header_width + 1 },
                    Child = topLanes = new Container
                    {
                        RelativeSizeAxes = Axes.Both, Masking = true,
                        Children = new Drawable[]
                        {
                            createBackground(0, 1).With(b => b.RelativeSizeAxes = Axes.Both),
                            volumeTimingRuler = new TimelineTickDisplay(fullHeight: true) { RelativeSizeAxes = Axes.Y },
                            volume = new HitSoundingVolumeTrack(model),
                            topPlayhead = createPlayhead()
                        }
                    }
                },
                laneSection = new Container
                {
                    RelativeSizeAxes = Axes.X, Y = HitSoundingLayout.LANE_TOP,
                    Height = HitSoundingLayout.LANE_COUNT * HitSoundingLayout.LANE_HEIGHT,
                    Children = new Drawable[]
                    {
                        laneHeaders = new Container { Width = header_width, RelativeSizeAxes = Axes.Y },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both, Padding = new MarginPadding { Left = header_width + 1 },
                            Child = lanes = new Container
                            {
                                RelativeSizeAxes = Axes.Both, Masking = true,
                                Children = new Drawable[]
                                {
                                    backgrounds = new Container { RelativeSizeAxes = Axes.Both },
                                    timingRuler = new TimelineTickDisplay(fullHeight: true) { RelativeSizeAxes = Axes.Y },
                                    selectedColumns = new Container { RelativeSizeAxes = Axes.Both },
                                    markers = new Container { RelativeSizeAxes = Axes.Both },
                                    samplePlayhead = createPlayhead(),
                                    selectionBox = new Box { Colour = colours.Highlight1, Alpha = 0 }
                                }
                            }
                        }
                    }
                },
                rangeContainer = new Container
                {
                    RelativeSizeAxes = Axes.X, Height = HitSoundingLayout.ROLL_HEIGHT,
                    Padding = new MarginPadding { Left = header_width + 1 }, Masking = true,
                    Child = previewRange = new Container
                    {
                        RelativeSizeAxes = Axes.Y,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Highlight1, Alpha = 0.065f },
                            previewStart = new Box { RelativeSizeAxes = Axes.Y, Width = 1, Colour = colours.Highlight1, Alpha = 0.35f },
                            previewEnd = new Box { RelativeSizeAxes = Axes.Y, Anchor = Anchor.TopRight, Origin = Anchor.TopRight, Width = 1, Colour = colours.Highlight1, Alpha = 0.35f }
                        }
                    }
                }
            };

            for (int i = 0; i < HitSoundingLayout.LANE_COUNT; i++)
            {
                laneHeaders.Add(new HitSoundingLaneHeader((HitSoundingLane)i));
                backgrounds.Add(new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = HitSoundingLayout.LANE_HEIGHT,
                    Y = i * HitSoundingLayout.LANE_HEIGHT,
                    Padding = new MarginPadding { Bottom = HitSoundingLayout.LANE_SPACING },
                    Child = createBackground(0, 1).With(b => b.RelativeSizeAxes = Axes.Both)
                });
            }

            model.Changed += invalidate;
            model.SelectedNodes.CollectionChanged += selectionChanged;
            beatmap.ControlPointInfo.ControlPointsChanged += invalidate;
            samplePlayback.NodePlayed += flashNode;
        }

        private Box createPlayhead() => new Box
        {
            RelativeSizeAxes = Axes.Y,
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Width = 2,
            Depth = -2,
            Colour = colours.Highlight1
        };

        private void invalidate() => invalid = true;

        private void flashNode(HitSoundingNode node)
        {
            if (node.HitObject is IHasRepeats)
            {
                if (!sliderFlashes.TryGetValue(node.HitObject, out var flashes))
                    return;

                for (int i = 0; i < flashes.Length; i++)
                {
                    if (points.TryGetValue(node, out var sliderPoints))
                    {
                        var point = sliderPoints[i];
                        bool visibleBody = node.IsBody && i == (int)HitSoundingLane.SampleSet;
                        if (point.IsEnabled && (!point.IsEmpty || visibleBody))
                            flashes[i].Trigger(node);
                    }
                }

                return;
            }

            if (points.TryGetValue(node, out var nodePoints))
            {
                foreach (var point in nodePoints)
                    point.FlashPlayback();
            }
        }

        private void selectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => lastTime = double.NaN;

        private Container createHeader(LocalisableString text, float y, float height, bool centred) => new Container
        {
            Y = y,
            Width = header_width,
            Height = height,
            Masking = true,
            CornerRadius = 5,
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background5 },
                new OsuSpriteText
                {
                    X = 10, Y = centred ? 0 : 10,
                    Anchor = centred ? Anchor.CentreLeft : Anchor.TopLeft,
                    Origin = centred ? Anchor.CentreLeft : Anchor.TopLeft,
                    Text = text.ToUpper(), Font = OsuFont.Default.With(size: 17, weight: FontWeight.Bold), Colour = colours.Light3
                }
            }
        };

        private Container createBackground(float y, float height) => new Container
        {
            RelativeSizeAxes = Axes.X,
            Y = y,
            Height = height,
            Masking = true,
            CornerRadius = 5,
            Child = new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background5 }
        };

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();
            float volumeHeight = Math.Clamp(Math.Min(HitSoundingLayout.VOLUME_HEIGHT + (DrawHeight - HitSoundingLayout.ROLL_HEIGHT) * 0.25f,
                DrawHeight - HitSoundingLayout.OBJECTS_HEIGHT - 5 - HitSoundingLayout.LANE_COUNT * (HitSoundingLayout.SAMPLE_HEIGHT + HitSoundingLayout.LANE_SPACING)), 40, 120);
            float laneTop = HitSoundingLayout.OBJECTS_HEIGHT + volumeHeight + 5;
            float laneHeight = Math.Max(HitSoundingLayout.SAMPLE_HEIGHT + HitSoundingLayout.LANE_SPACING, (DrawHeight - laneTop) / HitSoundingLayout.LANE_COUNT);
            volumeHeader.Height = volumeSection.Height = volumeHeight;
            laneSection.Y = laneTop;
            laneSection.Height = laneHeight * HitSoundingLayout.LANE_COUNT;
            rangeContainer.Height = DrawHeight;

            for (int i = 0; i < HitSoundingLayout.LANE_COUNT; i++)
            {
                laneHeaders[i].Y = backgrounds[i].Y = i * laneHeight;
                laneHeaders[i].Height = backgrounds[i].Height = laneHeight;
            }

            var timeline = timelineArea.Timeline;
            if (!timeline.IsLoaded || timeline.DrawWidth <= 0 || editorClock.TrackLength <= 0)
                return;

            double visibleStart = timeline.TimeAtScreenSpacePosition(lanes.ScreenSpaceDrawQuad.TopLeft);
            double visibleEnd = timeline.TimeAtScreenSpacePosition(lanes.ScreenSpaceDrawQuad.TopRight);
            visibleDuration = visibleEnd - visibleStart;
            if (visibleDuration <= 0)
                return;

            startTime = visibleStart;
            updatePreviewRange();
            float playheadX = (float)((editorClock.CurrentTime - visibleStart) / visibleDuration * lanes.DrawWidth);
            topPlayhead.Anchor = samplePlayhead.Anchor = Anchor.TopLeft;
            topPlayhead.X = samplePlayhead.X = playheadX;

            timingRuler.Width = volumeTimingRuler.Width = timeline.PositionAtTime(editorClock.TrackLength);
            timingRuler.X = volumeTimingRuler.X = -(float)(visibleStart / visibleDuration * lanes.DrawWidth);
            timingRuler.SetVisibleRange((float)visibleStart, (float)visibleEnd);
            volumeTimingRuler.SetVisibleRange((float)visibleStart, (float)visibleEnd);

            if (previewingSeek)
            {
                samplePlayback.PreviewSeek(lastPreviewTime, editorClock.CurrentTime);
                lastPreviewTime = editorClock.CurrentTime;
                previewingSeek = editorClock.IsSeeking;
            }

            if (!invalid && lastTime == visibleStart && lastDuration == visibleDuration && lastWidth == topLanes.DrawWidth
                && lastHeight == lanes.DrawHeight)
                return;

            lastTime = visibleStart;
            lastDuration = visibleDuration;
            lastWidth = topLanes.DrawWidth;
            lastHeight = lanes.DrawHeight;
            startTime = visibleStart;
            double endTime = startTime + visibleDuration;

            var nodes = model.Nodes;
            var hitObjects = beatmap.HitObjects;

            if (invalid)
            {
                refreshPoints();
                nodeRange.Rebuild(nodes.Count, i =>
                {
                    var node = nodes[i];
                    return node.IsBody ? (node.HitObject.StartTime, node.HitObject.GetEndTime()) : (node.Time, node.Time);
                });
                objectRange.Rebuild(hitObjects.Count, i => (hitObjects[i].StartTime, hitObjects[i].GetEndTime()));
                invalid = false;
            }

            double rangeStart = startTime - 200;
            double rangeEnd = endTime + 200;
            removedNodes.Clear();

            foreach (var node in points.Keys)
            {
                double nodeStart = node.IsBody ? node.HitObject.StartTime : node.Time;
                double nodeEnd = node.IsBody ? node.HitObject.GetEndTime() : node.Time;
                if (nodeEnd < rangeStart || nodeStart > rangeEnd)
                    removedNodes.Add(node);
            }

            foreach (var node in removedNodes)
            {
                recyclePoints(points[node]);
                points.Remove(node);

                if (node.IsBody && sliderFlashes.Remove(node.HitObject, out var flashes))
                {
                    recycleFlashes(flashes);
                }
            }

            var (firstNode, lastNode) = nodeRange.Find(rangeStart, rangeEnd);

            for (int nodeIndex = firstNode; nodeIndex < lastNode; nodeIndex++)
            {
                var node = nodes[nodeIndex];
                double nodeStart = node.IsBody ? node.HitObject.StartTime : node.Time;
                double nodeEnd = node.IsBody ? node.HitObject.GetEndTime() : node.Time;
                if (nodeEnd < rangeStart || nodeStart > rangeEnd)
                    continue;

                if (!points.TryGetValue(node, out var nodePoints))
                {
                    var pool = node.IsBody ? bodyPointPool : pointPool;

                    if (pool.TryPop(out nodePoints))
                    {
                        foreach (var point in nodePoints)
                            point.BindNode(node);
                    }
                    else
                    {
                        nodePoints = new HitSoundingSamplePoint[HitSoundingLayout.LANE_COUNT];
                        for (int i = 0; i < nodePoints.Length; i++)
                            nodePoints[i] = new HitSoundingSamplePoint(node, model, (HitSoundingLane)i) { Depth = node.IsBody ? 1 : 0 };

                        if (!node.IsBody)
                        {
                            // Only commute glows across the other lanes of this node. Their contents are disjoint
                            // even at the minimum lane pitch (40px content + 4px glow), unlike neighbouring nodes.
                            var group = new Container { RelativeSizeAxes = Axes.Both };
                            foreach (var point in nodePoints)
                                group.Add(point.CreateGlowProxy());
                            foreach (var point in nodePoints)
                                group.Add(point);
                            // Parent is assigned only once a drawable loads; retain ownership before then as well.
                            pointGroups.Add(nodePoints, group);
                        }
                    }

                    points.Add(node, nodePoints);

                    if (node.IsBody)
                    {
                        foreach (var point in nodePoints)
                            markers.Add(point);
                    }
                    else
                        markers.Add(pointGroups[nodePoints]);
                }

                foreach (var point in nodePoints)
                {
                    point.Position = new Vector2(timeToX((nodeStart + nodeEnd) / 2), ((float)point.Lane + 0.5f) * laneHeight - HitSoundingLayout.LANE_SPACING / 2);

                    if (node.IsBody)
                        point.UpdateBodyLayout(timeToX(nodeEnd) - timeToX(nodeStart), timeToX(node.Time) - timeToX(nodeStart));
                }

                if (node.IsBody)
                {
                    if (!sliderFlashes.TryGetValue(node.HitObject, out var flashes))
                    {
                        if (!flashPool.TryPop(out flashes))
                        {
                            flashes = new HitSoundingSliderPlaybackFlash[HitSoundingLayout.LANE_COUNT];
                            for (int i = 0; i < flashes.Length; i++)
                                flashes[i] = new HitSoundingSliderPlaybackFlash { Depth = -1 };
                        }

                        sliderFlashes.Add(node.HitObject, flashes);
                        foreach (var flash in flashes)
                            markers.Add(flash);
                    }

                    for (int i = 0; i < flashes.Length; i++)
                    {
                        bool hasBody = nodePoints[i].IsEnabled && (!nodePoints[i].IsEmpty || i == (int)HitSoundingLane.SampleSet);
                        flashes[i].HasBody = hasBody;
                        flashes[i].HasTail = false;
                        flashes[i].Position = nodePoints[i].Position;
                        flashes[i].Width = nodePoints[i].Width + HitSoundingLayout.SAMPLE_WIDTH;
                    }

                    foreach (var (related, relatedPoints) in points)
                    {
                        if (related.HitObject != node.HitObject || related.IsBody || related.Time < nodeEnd)
                            continue;

                        for (int i = 0; i < flashes.Length; i++)
                            flashes[i].HasTail = relatedPoints[i].IsEnabled && !relatedPoints[i].IsEmpty;
                    }
                }
            }

            objectLayer.RelativeChildSize = new Vector2((float)editorClock.TrackLength, 1);
            concurrentObjects.Clear();

            removedObjects.Clear();

            foreach (var hitObject in objects.Keys)
            {
                if (hitObject.GetEndTime() < rangeStart || hitObject.StartTime > rangeEnd)
                    removedObjects.Add(hitObject);
            }

            foreach (var hitObject in removedObjects)
            {
                objectLayer.Remove(objects[hitObject], false);
                objectPool.Push(objects[hitObject]);
                objects.Remove(hitObject);
            }

            var (firstObject, lastObject) = objectRange.Find(rangeStart, rangeEnd);

            for (int objectIndex = firstObject; objectIndex < lastObject; objectIndex++)
            {
                var hitObject = hitObjects[objectIndex];
                if (hitObject.GetEndTime() < rangeStart || hitObject.StartTime > rangeEnd)
                    continue;

                if (!objects.TryGetValue(hitObject, out HitSoundingObject? drawable))
                {
                    if (objectPool.TryPop(out drawable))
                        drawable.BindObject(hitObject);
                    else
                        drawable = new HitSoundingObject(hitObject, selectObject);
                    objects.Add(hitObject, drawable);
                    objectLayer.Add(drawable);
                }

                while (concurrentObjects.TryPeek(out HitObject? previous) && previous.GetEndTime() < hitObject.StartTime + 1)
                    concurrentObjects.Pop();

                if (concurrentObjects.TryPeek(out HitObject? last) && Math.Abs(last.StartTime - hitObject.StartTime) > 1 && concurrentObjects.Count >= 3)
                    concurrentObjects.Clear();

                drawable.Y = -5 * concurrentObjects.Count;
                concurrentObjects.Push(hitObject);
            }

            updateSelectionColumns(endTime);
            volume.VisibleStartTime = startTime;
            volume.VisibleEndTime = endTime;
            volume.Refresh();
        }

        private void updatePreviewRange()
        {
            previewRange.Alpha = 0;
            if (!previewState.ShowHighlight.Value || previewState.StartTime is not double rangeStart || previewState.EndTime is not double rangeEnd)
                return;

            double visibleStart = startTime;
            double visibleEnd = visibleStart + visibleDuration;
            rangeStart = Math.Max(0, rangeStart);
            rangeEnd = Math.Min(editorClock.TrackLength, rangeEnd);
            if (rangeEnd <= visibleStart || rangeStart >= visibleEnd)
                return;

            previewRange.Alpha = 1;
            previewRange.X = (float)((Math.Max(rangeStart, visibleStart) - visibleStart) / visibleDuration * topLanes.DrawWidth);
            previewRange.Width = (float)((Math.Min(rangeEnd, visibleEnd) - Math.Max(rangeStart, visibleStart)) / visibleDuration * topLanes.DrawWidth);
            previewStart.Alpha = rangeStart >= visibleStart ? 0.35f : 0;
            previewEnd.Alpha = rangeEnd <= visibleEnd ? 0.35f : 0;
        }

        private void refreshPoints()
        {
            currentNodes.Clear();
            currentNodes.UnionWith(model.Nodes);
            currentObjects.Clear();
            currentObjects.UnionWith(beatmap.HitObjects);

            foreach (var hitObject in objects.Keys.Where(h => !currentObjects.Contains(h)).ToArray())
            {
                objectLayer.Remove(objects[hitObject], false);
                objectPool.Push(objects[hitObject]);
                objects.Remove(hitObject);
            }

            foreach (var node in points.Keys.Where(n => !currentNodes.Contains(n)).ToArray())
            {
                recyclePoints(points[node]);
                points.Remove(node);
            }

            foreach (var hitObject in sliderFlashes.Keys.Where(h => !currentObjects.Contains(h)).ToArray())
            {
                recycleFlashes(sliderFlashes[hitObject]);
                sliderFlashes.Remove(hitObject);
            }

            foreach (var nodePoints in points.Values)
            {
                foreach (var point in nodePoints)
                    point.Refresh();
            }
        }

        private void recyclePoints(HitSoundingSamplePoint[] nodePoints)
        {
            foreach (var point in nodePoints)
            {
                point.ResetForReuse();
                if (point.Node.IsBody)
                    markers.Remove(point, false);
            }

            if (!nodePoints[0].Node.IsBody)
                markers.Remove(pointGroups[nodePoints], false);

            (nodePoints[0].Node.IsBody ? bodyPointPool : pointPool).Push(nodePoints);
        }

        private void recycleFlashes(HitSoundingSliderPlaybackFlash[] flashes)
        {
            foreach (var flash in flashes)
            {
                flash.ResetForReuse();
                markers.Remove(flash, false);
            }

            flashPool.Push(flashes);
        }

        private void updateSelectionColumns(double endTime)
        {
            int selectedCount = 0;

            foreach (var node in model.SelectedNodes)
            {
                if (node.IsBody || node.Time < startTime || node.Time > endTime)
                    continue;

                if (selectedCount == columnBoxes.Count)
                {
                    var column = new Box { RelativeSizeAxes = Axes.Y, Width = HitSoundingLayout.SAMPLE_WIDTH, Colour = colours.Highlight1 };
                    columnBoxes.Add(column);
                    selectedColumns.Add(column);
                }

                var box = columnBoxes[selectedCount++];
                box.X = timeToX(node.Time) - box.Width / 2;
                box.Alpha = 0.08f;
            }

            for (int i = selectedCount; i < columnBoxes.Count; i++)
                columnBoxes[i].Alpha = 0;
        }

        private float timeToX(double time) => (float)((time - startTime) / visibleDuration * lanes.DrawWidth);

        // Prefix ends and suffix starts are monotonic even for overlapping sliders and body nodes
        // ordered by their sample time. Keep the original order for stacking and endpoint highlights.
        private sealed class TimeRangeIndex
        {
            private double[] ends = [];
            private double[] starts = [];

            public void Rebuild(int count, Func<int, (double Start, double End)> getRange)
            {
                if (ends.Length != count)
                {
                    ends = new double[count];
                    starts = new double[count];
                }

                double end = double.NegativeInfinity;

                for (int i = 0; i < count; i++)
                {
                    var range = getRange(i);
                    starts[i] = range.Start;
                    ends[i] = end = Math.Max(end, range.End);
                }

                for (int i = count - 2; i >= 0; i--)
                    starts[i] = Math.Min(starts[i], starts[i + 1]);
            }

            public (int Start, int End) Find(double start, double end)
            {
                int low = 0;
                int high = ends.Length;

                while (low < high)
                {
                    int mid = low + (high - low) / 2;
                    if (ends[mid] < start)
                        low = mid + 1;
                    else
                        high = mid;
                }

                int first = low;
                high = starts.Length;

                while (low < high)
                {
                    int mid = low + (high - low) / 2;
                    if (starts[mid] <= end)
                        low = mid + 1;
                    else
                        high = mid;
                }

                return (first, low);
            }
        }

        private void selectObject(HitObject hitObject)
        {
            model.SelectedNodes.Clear();

            foreach (var node in model.Nodes)
            {
                if (node.HitObject == hitObject)
                    model.SelectedNodes.Add(node);
            }
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != MouseButton.Right)
                return false;

            Vector2 position = lanes.ToLocalSpace(e.ScreenSpaceMousePosition);
            if (position.X < 0 || position.X > lanes.DrawWidth || position.Y < 0 || position.Y > lanes.DrawHeight)
                return false;

            foreach (var nodePoints in points.Values)
            {
                foreach (var point in nodePoints)
                {
                    if (point.IsPresent && point.ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
                        return false;
                }
            }

            this.HidePopover();

            if (model.SelectedNodes.Count > 1)
                model.SelectedNodes.Clear();
            else
            {
                HitSoundingNode? nearest = null;
                float nearestDistance = float.MaxValue;

                foreach (var (node, nodePoints) in points)
                {
                    foreach (var point in nodePoints)
                    {
                        float distance = (point.Position - position).LengthSquared;

                        if (distance < nearestDistance)
                        {
                            nearest = node;
                            nearestDistance = distance;
                        }
                    }
                }

                model.SelectedNodes.Clear();

                if (nearest != null)
                    model.SelectedNodes.Add(nearest);
            }

            return true;
        }

        public void CancelSeekPreview() => previewingSeek = false;

        protected override bool OnScroll(ScrollEvent e)
        {
            if (e.AltPressed)
            {
                timelineArea.Timeline.AdjustZoomRelatively(e.ScrollDelta.Y);
                return true;
            }

            // Only scrubbing a playing track previews samples; while paused the wheel just moves the cursor silently.
            if (editorClock.IsRunning)
            {
                if (!previewingSeek)
                    lastPreviewTime = editorClock.CurrentTime;
                previewingSeek = true;
            }
            else
                previewingSeek = false;

            editorClock.SeekSmoothlyTo(snappedScrollTarget(e.ScrollDelta.Y));
            return true;
        }

        /// <summary>
        /// Moves by a zoom-proportional distance, then lands on the closest sound point in the direction of travel.
        /// A notch always advances at least one point. If there is no point ahead, the clamped destination is used.
        /// </summary>
        private double snappedScrollTarget(float scrollDelta)
        {
            double current = editorClock.CurrentTimeAccurate;
            double target = Math.Clamp(current - scrollDelta * visibleDuration / 20, 0, editorClock.TrackLength);
            if (scrollDelta == 0)
                return target;

            bool movingForward = scrollDelta < 0;
            double snapped = target;
            double nearestDistance = double.MaxValue;

            foreach (var node in model.Nodes)
            {
                double time = node.Time;
                if (time < 0 || time > editorClock.TrackLength || (movingForward ? time <= current : time >= current))
                    continue;

                double distance = Math.Abs(time - target);
                if (distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                snapped = time;
            }

            return snapped;
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (e.Button != MouseButton.Left)
                return false;

            Vector2 position = lanes.ToLocalSpace(e.ScreenSpaceMouseDownPosition);
            if (position.X < 0 || position.X > lanes.DrawWidth || position.Y < 0 || position.Y > lanes.DrawHeight)
                return false;

            selectionStart = position;
            if (!e.ControlPressed)
                model.SelectedNodes.Clear();
            selectionBox.Position = position;
            selectionBox.Size = Vector2.Zero;
            selectionBox.Alpha = 0.15f;
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            if (selectionStart is not Vector2 start)
                return;

            Vector2 end = lanes.ToLocalSpace(e.ScreenSpaceMousePosition);
            selectionBox.Position = Vector2.ComponentMin(start, end);
            selectionBox.Size = Vector2.ComponentMax(start, end) - selectionBox.Position;
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            if (selectionStart == null)
                return;

            foreach (var (node, nodePoints) in points)
            {
                if (nodePoints.Any(p =>
                        (node.IsBody
                            ? p.X + p.Width / 2 >= selectionBox.X && p.X - p.Width / 2 <= selectionBox.X + selectionBox.Width
                            : p.X >= selectionBox.X && p.X <= selectionBox.X + selectionBox.Width)
                        && p.Y >= selectionBox.Y && p.Y <= selectionBox.Y + selectionBox.Height)
                    && !model.SelectedNodes.Contains(node))
                    model.SelectedNodes.Add(node);
            }

            selectionStart = null;
            selectionBox.Alpha = 0;
        }

        protected override void Dispose(bool isDisposing)
        {
            model.Changed -= invalidate;
            model.SelectedNodes.CollectionChanged -= selectionChanged;
            samplePlayback.NodePlayed -= flashNode;

            if (beatmap.IsNotNull())
                beatmap.ControlPointInfo.ControlPointsChanged -= invalidate;

            if (isDisposing)
            {
                foreach (var pooled in pointPool.Concat(bodyPointPool))
                {
                    if (pooled[0].Node.IsBody)
                    {
                        foreach (var point in pooled)
                            point.Dispose();
                    }
                    else
                        pointGroups[pooled].Dispose();
                }

                foreach (var pooled in flashPool)
                {
                    foreach (var flash in pooled)
                        flash.Dispose();
                }

                foreach (var drawable in objectPool)
                    drawable.Dispose();

                objectPool.Clear();
                pointPool.Clear();
                bodyPointPool.Clear();
                flashPool.Clear();
            }

            base.Dispose(isDisposing);
        }
    }
}
