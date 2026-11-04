// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Input;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Rulesets.Objects;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Edit.HitSounding
{
    public partial class HitSoundingRoll : CompositeDrawable, IBlockGlobalAltScrollVolume
    {
        private const float header_width = 200;
        private const float set_top = 180;
        private readonly HitSoundingEditModel model;
        public HitSoundingSetLayout SetLayout { get; } = new();

        private readonly Dictionary<int, HitSoundingLaneHeader> laneHeaders = [];
        private readonly Dictionary<int, Container[]> laneBackgrounds = [];
        private readonly Dictionary<HitSoundingNode, Circle> collapsedPoints = [];
        private readonly Dictionary<HitSoundingNode, List<HitSoundingSamplePoint>> points = [];
        private readonly Dictionary<HitObject, HitSoundingObject> objects = [];
        private readonly List<Box> columnBoxes = [];
        private readonly Stack<HitObject> concurrentObjects = new();
        private Container surface = null!;
        private Container headers = null!;
        private Container lanes = null!;
        private Container topLanes = null!;
        private Container backgrounds = null!;
        private Container markers = null!;
        private Container collapsedMarkers = null!;
        private Container objectLayer = null!;
        private Container selectedColumns = null!;
        private HitSoundingSetScrollContainer setScroll = null!;
        private HitSoundingSetManagementButton setManagement = null!;
        private Color4 sampleColour;
        private HitSoundingBeatGrid grid = null!;
        private HitSoundingBeatGrid topGrid = null!;
        private HitSoundingVolumeTrack volume = null!;
        private Box selectionBox = null!;
        private Vector2? selectionStart;
        private double visibleDuration = 6000;
        private double startTime;
        private bool invalid = true;
        private bool previewingSeek;
        private double lastPreviewTime;
        private double lastTime = double.NaN;
        private double lastDuration;
        private float lastWidth;
        private int lastDivisor;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private EditorBeatmap beatmap { get; set; } = null!;

        [Resolved]
        private BindableBeatDivisor beatDivisor { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colours { get; set; } = null!;

        [Resolved]
        private HitSoundingSamplePlayback samplePlayback { get; set; } = null!;

        public HitSoundingRoll(HitSoundingEditModel model)
        {
            this.model = model;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour osuColour)
        {
            sampleColour = osuColour.Pink1;
            InternalChildren = new Drawable[]
            {
                createHeader(HitSoundingStrings.Objects, 0, 70, true),
                createHeader(EditorStrings.SampleVolume, 75, 100, false),
                new Container
                {
                    RelativeSizeAxes = Axes.X, Height = 175,
                    Padding = new MarginPadding { Left = header_width + 1 },
                    Child = topLanes = new Container
                    {
                        RelativeSizeAxes = Axes.Both, Masking = true,
                        Children = new Drawable[]
                        {
                            createBackground(0, 70),
                            createBackground(75, 100),
                            topGrid = [],
                            objectLayer = new Container { RelativeSizeAxes = Axes.X, Height = 70 },
                            volume = new HitSoundingVolumeTrack(model) { Y = 75 },
                            createPlayhead(),
                            new Box { Anchor = Anchor.TopCentre, Origin = Anchor.TopCentre, Width = 12, Height = 6, Colour = colours.Highlight1 }
                        }
                    }
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both, Padding = new MarginPadding { Top = set_top },
                    Child = setScroll = new HitSoundingSetScrollContainer
                    {
                        RelativeSizeAxes = Axes.Both, ScrollbarOverlapsContent = true,
                        Child = surface = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Children = new Drawable[]
                            {
                                headers = new Container { Width = header_width, RelativeSizeAxes = Axes.Y },
                                setManagement = new HitSoundingSetManagementButton(SetLayout),
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Left = header_width + 1, Bottom = 41 },
                                    Child = lanes = new Container
                                    {
                                        RelativeSizeAxes = Axes.Both, Masking = true,
                                        Children = new Drawable[]
                                        {
                                            backgrounds = new Container { RelativeSizeAxes = Axes.Both },
                                            grid = [],
                                            selectedColumns = new Container { RelativeSizeAxes = Axes.Both },
                                            markers = new Container { RelativeSizeAxes = Axes.Both },
                                            collapsedMarkers = new Container { RelativeSizeAxes = Axes.Both },
                                            createPlayhead(),
                                            selectionBox = new Box { Colour = colours.Highlight1, Alpha = 0 }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
            model.Changed += invalidate;
            SetLayout.Changed += layoutChanged;
            model.SelectedNodes.CollectionChanged += selectionChanged;
            beatmap.ControlPointInfo.ControlPointsChanged += invalidate;
            if (beatmap.BeatmapSkin != null)
                beatmap.BeatmapSkin.BeatmapSkinChanged += skinChanged;
            rebuildLanes();
        }

        private Box createPlayhead() => new()
        {
            RelativeSizeAxes = Axes.Y,
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Width = 2,
            Colour = colours.Highlight1
        };

        private void skinChanged() => Schedule(() => { rebuildLanes(); invalidate(); });
        private void invalidate() => invalid = true;
        private void selectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => lastTime = double.NaN;

        private void layoutChanged()
        {
            rebuildLanes();
            invalidate();
        }

        private void rebuildLanes()
        {
            SetLayout.Include(new[] { 0, 1 });
            if (beatmap.BeatmapSkin != null)
                SetLayout.Include(beatmap.BeatmapSkin.GetAvailableSampleSets().Select(s => s.SampleSetIndex));
            SetLayout.Include(model.Nodes.Select(n => n.SetIndex));

            foreach (int set in SetLayout.DisplayOrder)
            {
                float y = SetLayout.RowY(set)!.Value;
                bool collapsed = SetLayout.IsCollapsed(set);
                bool existing = laneHeaders.ContainsKey(set);
                if (!existing)
                {
                    var header = new HitSoundingLaneHeader(set, collapsed, () => SetLayout.ToggleCollapsed(set)) { Y = y };
                    laneHeaders.Add(set, header);
                    headers.Add(header);
                    var bankBackgrounds = new Container[3];
                    for (int bank = 0; bank < bankBackgrounds.Length; bank++)
                    {
                        bankBackgrounds[bank] = createBackground(y + bank * HitSoundingLayout.BANK_PITCH, HitSoundingLayout.BANK_HEIGHT);
                        backgrounds.Add(bankBackgrounds[bank]);
                    }
                    laneBackgrounds.Add(set, bankBackgrounds);
                }

                var laneHeader = laneHeaders[set];
                double duration = existing ? HitSoundingLaneHeader.TRANSITION_DURATION : 0;
                laneHeader.MoveToY(y, duration, Easing.OutQuint);
                if (laneHeader.IsCollapsed != collapsed)
                    laneHeader.SetCollapsed(collapsed);
                for (int bank = 0; bank < 3; bank++)
                {
                    var background = laneBackgrounds[set][bank];
                    background.MoveToY(y + (collapsed ? 0 : bank * HitSoundingLayout.BANK_PITCH), duration, Easing.OutQuint);
                    background.ResizeHeightTo(collapsed ? HitSoundingLayout.COLLAPSED_SET_HEIGHT : HitSoundingLayout.BANK_HEIGHT, duration, Easing.OutQuint);
                    background.FadeTo(collapsed && bank > 0 ? 0 : 1, duration, Easing.OutQuint);
                }
            }
            foreach (var (node, dot) in collapsedPoints)
                if (SetLayout.IsCollapsed(node.SetIndex))
                    dot.MoveToY(SetLayout.RowY(node.SetIndex)!.Value + HitSoundingLayout.COLLAPSED_SET_HEIGHT / 2,
                        HitSoundingLaneHeader.TRANSITION_DURATION, Easing.OutQuint);
            setManagement.MoveToY(SetLayout.TotalHeight, HitSoundingLaneHeader.TRANSITION_DURATION, Easing.OutQuint);
            surface.ResizeHeightTo(SetLayout.TotalHeight + 41, HitSoundingLaneHeader.TRANSITION_DURATION, Easing.OutQuint);
        }

        private Container createHeader(LocalisableString text, float y, float height, bool centred) => new()
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

        private Container createBackground(float y, float height) => new()
        {
            RelativeSizeAxes = Axes.X,
            Y = y,
            Height = height,
            Masking = true,
            CornerRadius = 5,
            Child = new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background5 }
        };

        protected override void Update()
        {
            base.Update();
            if (previewingSeek)
            {
                samplePlayback.PreviewSeek(lastPreviewTime, editorClock.CurrentTime);
                lastPreviewTime = editorClock.CurrentTime;
                previewingSeek = editorClock.IsSeeking;
            }
            if (!invalid && lastTime == editorClock.CurrentTime && lastDuration == visibleDuration && lastWidth == topLanes.DrawWidth && lastDivisor == beatDivisor.Value)
                return;
            lastTime = editorClock.CurrentTime;
            lastDuration = visibleDuration;
            lastWidth = topLanes.DrawWidth;
            lastDivisor = beatDivisor.Value;
            startTime = editorClock.CurrentTime - visibleDuration / 2;
            double endTime = startTime + visibleDuration;
            if (invalid)
            {
                refreshPoints();
                invalid = false;
            }

            foreach (var node in model.Nodes)
            {
                bool visible = node.Time >= startTime - 200 && node.Time <= endTime + 200;
                bool collapsed = SetLayout.IsCollapsed(node.SetIndex);
                if (!visible || collapsed)
                {
                    if (points.Remove(node, out var removed))
                        foreach (var point in removed)
                            point.FadeOut(120).Expire();
                }
                if (!visible || !collapsed)
                {
                    if (collapsedPoints.Remove(node, out var removed))
                        removed.FadeOut(120).Expire();
                }
                if (!visible)
                    continue;
                if (collapsed)
                {
                    if (!collapsedPoints.TryGetValue(node, out var dot))
                    {
                        dot = new Circle
                        {
                            Name = "collapsed sample",
                            Size = new Vector2(HitSoundingLayout.COLLAPSED_SAMPLE_SIZE),
                            Y = laneHeaders[node.SetIndex].Y + HitSoundingLayout.COLLAPSED_SET_HEIGHT / 2,
                            Alpha = 0,
                            Origin = Anchor.Centre,
                            Colour = sampleColour
                        };
                        collapsedPoints.Add(node, dot);
                        collapsedMarkers.Add(dot);
                        dot.FadeIn(HitSoundingLaneHeader.TRANSITION_DURATION);
                        dot.MoveToY(SetLayout.RowY(node.SetIndex)!.Value + HitSoundingLayout.COLLAPSED_SET_HEIGHT / 2,
                            HitSoundingLaneHeader.TRANSITION_DURATION, Easing.OutQuint);
                    }
                    dot.X = timeToX(node.Time);
                    continue;
                }
                if (!points.TryGetValue(node, out var nodePoints))
                {
                    points.Add(node, nodePoints = []);
                    string normalBank = node.Samples.FirstOrDefault(s => s.Name == HitSampleInfo.HIT_NORMAL)?.Bank ?? HitSampleInfo.BANK_NORMAL;
                    foreach (string bank in node.Samples.Select(s => s.Bank).DefaultIfEmpty(normalBank).Distinct())
                    {
                        var point = new HitSoundingSamplePoint(node, model, bank, normalBank != bank, dropPoint);
                        nodePoints.Add(point);
                        markers.Add(point);
                        point.FadeInFromZero(120);
                    }
                }
                float cellWidth = (float)(beatmap.ControlPointInfo.TimingPointAt(node.Time).BeatLength / beatDivisor.Value / visibleDuration * lanes.DrawWidth);
                foreach (var point in nodePoints)
                {
                    point.UpdatePosition(new Vector2(timeToX(node.Time), laneY(node.SetIndex, point.Bank) + HitSoundingLayout.BANK_HEIGHT / 2));
                    point.UpdateLayout(cellWidth);
                }
            }

            objectLayer.RelativeChildSize = new Vector2((float)visibleDuration, 1);
            objectLayer.RelativeChildOffset = new Vector2((float)startTime, 0);
            concurrentObjects.Clear();
            foreach (var hitObject in beatmap.HitObjects)
            {
                if (hitObject.GetEndTime() < startTime - 200 || hitObject.StartTime > endTime + 200)
                {
                    if (objects.Remove(hitObject, out var removed))
                        objectLayer.Remove(removed, true);
                    continue;
                }
                if (!objects.TryGetValue(hitObject, out HitSoundingObject? drawable))
                {
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
            grid.Refresh(startTime, endTime);
            topGrid.Refresh(startTime, endTime);
            updateSelectionColumns(endTime);
            volume.VisibleStartTime = startTime;
            volume.VisibleEndTime = endTime;
            volume.Refresh();
        }

        private void refreshPoints()
        {
            if (model.Nodes.Any(n => !SetLayout.Contains(n.SetIndex)))
                rebuildLanes();
            foreach (var hitObject in objects.Keys.Where(h => !beatmap.HitObjects.Contains(h)).ToArray())
            {
                objectLayer.Remove(objects[hitObject], true);
                objects.Remove(hitObject);
            }
            foreach (var node in points.Keys.Where(n => !model.Nodes.Contains(n)).ToArray())
            {
                foreach (var point in points[node])
                    markers.Remove(point, true);
                points.Remove(node);
            }
            foreach (var node in collapsedPoints.Keys.Where(n => !model.Nodes.Contains(n)).ToArray())
            {
                collapsedMarkers.Remove(collapsedPoints[node], true);
                collapsedPoints.Remove(node);
            }
            foreach (var (node, nodePoints) in points)
            {
                foreach (var point in nodePoints)
                {
                    point.Bank = node.Samples.FirstOrDefault(s => point.AdditionsOnly ? s.Name != HitSampleInfo.HIT_NORMAL : s.Name == HitSampleInfo.HIT_NORMAL)?.Bank
                                 ?? node.Samples.FirstOrDefault()?.Bank ?? HitSampleInfo.BANK_NORMAL;
                    point.Refresh();
                }
                foreach (string bank in node.Samples.Select(s => s.Bank).Distinct())
                {
                    if (nodePoints.Any(p => p.Bank == bank))
                        continue;
                    var point = new HitSoundingSamplePoint(node, model, bank, true, dropPoint);
                    nodePoints.Add(point);
                    markers.Add(point);
                }
                for (int i = nodePoints.Count - 1; i >= 0; i--)
                {
                    var point = nodePoints[i];
                    if (point.AdditionsOnly && (!node.Samples.Any(s => s.Name != HitSampleInfo.HIT_NORMAL)
                        || nodePoints.Any(p => p != point && !p.AdditionsOnly && p.Bank == point.Bank)))
                    {
                        markers.Remove(point, true);
                        nodePoints.RemoveAt(i);
                    }
                }
            }
        }

        private void updateSelectionColumns(double endTime)
        {
            int selectedCount = 0;
            foreach (var node in model.SelectedNodes)
            {
                if (node.Time < startTime || node.Time > endTime)
                    continue;
                if (selectedCount == columnBoxes.Count)
                {
                    var column = new Box { RelativeSizeAxes = Axes.Y, Width = HitSoundingLayout.SAMPLE_HEIGHT, Colour = colours.Highlight1 };
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

        private float laneY(int set, string bank)
            => (SetLayout.RowY(set) ?? 0) + Math.Max(0, Array.IndexOf(HitSampleInfo.ALL_BANKS, bank)) * HitSoundingLayout.BANK_PITCH;

        private void selectObject(HitObject hitObject)
        {
            model.SelectedNodes.Clear();
            foreach (var node in model.Nodes)
                if (node.HitObject == hitObject)
                    model.SelectedNodes.Add(node);
        }

        private void dropPoint(HitSoundingSamplePoint point, Vector2 screenPosition)
        {
            if (!setScroll.Contains(screenPosition))
                return;
            Vector2 position = lanes.ToLocalSpace(screenPosition);
            if (position.X < 0 || position.X > lanes.DrawWidth || !SetLayout.TryGetSetAt(position.Y, out int set, out float offset)
                || SetLayout.IsCollapsed(set))
                return;
            int bank = (int)(offset / HitSoundingLayout.BANK_PITCH);
            if (bank > 2 || offset % HitSoundingLayout.BANK_PITCH >= HitSoundingLayout.BANK_HEIGHT)
                return;
            var targets = model.SelectedNodes.Contains(point.Node) ? model.SelectedNodes.ToArray() : new[] { point.Node };
            model.SetBank(targets, HitSampleInfo.ALL_BANKS[bank], set, point.AdditionsOnly);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != MouseButton.Right)
                return false;
            if (!setScroll.Contains(e.ScreenSpaceMousePosition))
                return false;
            Vector2 position = lanes.ToLocalSpace(e.ScreenSpaceMousePosition);
            if (position.X < 0 || position.X > lanes.DrawWidth || position.Y < 0 || position.Y > lanes.DrawHeight)
                return false;

            foreach (var nodePoints in points.Values)
                foreach (var point in nodePoints)
                    if (point.IsPresent && point.ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
                        return false;

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
                        if (!setScroll.Contains(point.ScreenSpaceDrawQuad.Centre))
                            continue;
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
                visibleDuration = Math.Clamp(visibleDuration * Math.Pow(0.9, e.ScrollDelta.Y), 500, 30000);
                return true;
            }
            if (e.ShiftPressed)
            {
                setScroll.ScrollSets(e);
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
            if (e.Button != MouseButton.Left || !setScroll.Contains(e.ScreenSpaceMousePosition))
                return false;

            // a drag which started on a marker belongs to that marker, not to box selection.
            foreach (var nodePoints in points.Values)
                foreach (var point in nodePoints)
                    if (point.IsPresent && point.ReceivePositionalInputAt(e.ScreenSpaceMouseDownPosition))
                        return false;

            Vector2 position = lanes.ToLocalSpace(e.ScreenSpaceMousePosition);
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
                if (nodePoints.Any(p => p.X >= selectionBox.X && p.X <= selectionBox.X + selectionBox.Width && p.Y >= selectionBox.Y && p.Y <= selectionBox.Y + selectionBox.Height)
                    && !model.SelectedNodes.Contains(node))
                    model.SelectedNodes.Add(node);
            selectionStart = null;
            selectionBox.Alpha = 0;
        }

        protected override void Dispose(bool isDisposing)
        {
            model.Changed -= invalidate;
            SetLayout.Changed -= layoutChanged;
            model.SelectedNodes.CollectionChanged -= selectionChanged;
            if (beatmap != null)
            {
                beatmap.ControlPointInfo.ControlPointsChanged -= invalidate;
                if (beatmap.BeatmapSkin != null)
                    beatmap.BeatmapSkin.BeatmapSkinChanged -= skinChanged;
            }
            base.Dispose(isDisposing);
        }
    }
}
