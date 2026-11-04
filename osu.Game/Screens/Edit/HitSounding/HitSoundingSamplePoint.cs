// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.IO.Stores;
using osu.Framework.Localisation;
using osu.Framework.Text;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// A marker representing the samples of a single bank on a <see cref="HitSoundingNode"/>.
    /// </summary>
    /// <remarks>
    /// Left-clicking the marker opens the sample edit popover, while right-clicking exposes the sample operations
    /// through the editor's standard context menu. Control-clicking toggles the node's selection without opening anything,
    /// so a multi-selection can be built up before using either editing surface.
    /// </remarks>
    public partial class HitSoundingSamplePoint : HitObjectPointPiece, IHasPopover, IHasTooltip, IHasContextMenu
    {
        public readonly HitSoundingNode Node;
        public readonly bool AdditionsOnly;
        private readonly HitSoundingEditModel model;
        private readonly Action<HitSoundingSamplePoint, Vector2> dropped;

        /// <summary>
        /// The bank this marker displays.
        /// </summary>
        public string Bank { get; set; }

        [Resolved]
        private FontStore fontStore { get; set; } = null!;

        private string labelText = string.Empty;
        private float gridCellWidth;
        private bool selected;
        private Vector2 lanePosition;
        private bool hasLanePosition;
        private Vector2 dragPosition;
        private Vector2 dragOffset;
        private bool dragging;

        /// <summary>
        /// The offset of the displayed badge from its lane position, animated back to zero after the badge changes lane.
        /// </summary>
        /// <remarks>
        /// Animating an offset rather than the absolute position keeps the badge locked to its time column while the
        /// timeline scrolls underneath it during the animation.
        /// </remarks>
        private Vector2 displacement { get; set; }

        /// <summary>
        /// The time it takes a badge to settle into its lane after a drop or a set/bank change.
        /// </summary>
        public const double SNAP_DURATION = 300;

        /// <summary>
        /// Sets the position of the lane slot this marker belongs to, in the coordinate space of its parent.
        /// </summary>
        /// <remarks>
        /// Horizontal changes (timeline scrolling, zoom) apply immediately. A vertical change means the marker moved to a
        /// different set or bank lane, so the badge animates from where it is currently displayed into the new lane.
        /// </remarks>
        public void UpdatePosition(Vector2 position)
        {
            if (hasLanePosition && !dragging && position.Y != lanePosition.Y)
                snapFrom(displacement + new Vector2(0, lanePosition.Y - position.Y));

            lanePosition = position;
            hasLanePosition = true;
        }

        private void snapFrom(Vector2 offset)
        {
            ClearTransforms(false, nameof(displacement));
            displacement = offset;
            this.TransformTo(nameof(displacement), Vector2.Zero, SNAP_DURATION, Easing.OutQuint);
        }

        public HitSoundingSamplePoint(HitSoundingNode node, HitSoundingEditModel model, string bank, bool additionsOnly,
                                      Action<HitSoundingSamplePoint, Vector2> dropped)
        {
            Node = node;
            this.model = model;
            Bank = bank;
            AdditionsOnly = additionsOnly;
            this.dropped = dropped;
            Origin = Anchor.Centre;

            // the base piece is a circular container; the badge itself defines its own rounded rectangle shape,
            // so the ancestor must never mask the label to a circle.
            Masking = false;
            CornerRadius = 0;
        }

        public LocalisableString TooltipText => LocalisableString.Interpolate($"{HitSoundingStrings.SamplePoint(string.Join(" + ", Node.Samples.Where(s => s.Bank == Bank).Select(s => s.Name)), Bank, Node.SetIndex)}\n{HitSoundingStrings.EditHint}");

        public MenuItem[] ContextMenuItems
        {
            get
            {
                HitSoundingNode[] targets = model.SelectedNodes.Contains(Node) ? model.SelectedNodes.ToArray() : new[] { Node };

                bool canMoveUp = model.CanMoveSet(targets, -1);
                bool canMoveDown = model.CanMoveSet(targets, 1);

                Action? moveUp = canMoveUp ? new Action(() => model.MoveSet(targets, -1)) : null;
                Action? moveDown = canMoveDown ? new Action(() => model.MoveSet(targets, 1)) : null;

                return new MenuItem[]
                {
                    new OsuMenuItem(HitSoundingStrings.RestoreDefaults, MenuItemType.Standard, () => model.RestoreDefaults(targets)),
                    new OsuMenuItem(HitSoundingStrings.MoveUpOneSet, MenuItemType.Standard, moveUp),
                    new OsuMenuItem(HitSoundingStrings.MoveDownOneSet, MenuItemType.Standard, moveDown),
                };
            }
        }

        protected override Color4 GetRepresentingColour(OsuColour colours) => colours.Pink1;

        [BackgroundDependencyLoader]
        private void load()
        {
            Label.AllowMultiline = false;
            Refresh();
        }

        /// <summary>
        /// Rebuilds the marker label from the node's current samples and reapplies its layout.
        /// </summary>
        public void Refresh()
        {
            if (Label == null)
                return;

            labelText = buildLabelText();

            Label.Text = labelText;
            applyLayout();
        }

        /// <summary>
        /// Updates the maximum width available to this marker.
        /// </summary>
        /// <param name="gridCellWidth">
        /// The width of one beat-divisor cell in screen pixels. The badge never grows past a single cell; when the cell
        /// is narrower than the label, the label font is scaled down proportionally instead of being clipped.
        /// Pass <c>0</c> to leave the badge uncapped.
        /// </param>
        public void UpdateLayout(float gridCellWidth)
        {
            if (gridCellWidth < 0 || float.IsNaN(gridCellWidth) || float.IsInfinity(gridCellWidth))
                gridCellWidth = 0;

            if (Math.Abs(this.gridCellWidth - gridCellWidth) < 0.1f)
                return;

            this.gridCellWidth = gridCellWidth;
            applyLayout();
        }

        protected override void Update()
        {
            base.Update();

            Position = dragging ? dragPosition : lanePosition + displacement;

            bool isSelected = model.SelectedNodes.Contains(Node);

            if (isSelected == selected)
                return;

            selected = isSelected;
            LabelContainer.BorderThickness = selected ? 2 : 0;
            LabelContainer.BorderColour = Color4.White;
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            // right-clicking must never open the sample popover; let the editor's context menu handle the press
            // while closing any popover which was left open by an earlier left-click edit.
            if (e.Button == MouseButton.Right)
            {
                this.HidePopover();
                return false;
            }

            return true;
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (e.ControlPressed)
            {
                if (!model.SelectedNodes.Remove(Node))
                    model.SelectedNodes.Add(Node);

                return true;
            }

            if (!model.SelectedNodes.Contains(Node))
            {
                model.SelectedNodes.Clear();
                model.SelectedNodes.Add(Node);
            }

            this.ShowPopover();
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (e.Button != MouseButton.Left)
                return false;

            this.HidePopover();
            ClearTransforms(false, nameof(displacement));
            dragging = true;
            dragOffset = Position - Parent!.ToLocalSpace(e.ScreenSpaceMouseDownPosition);
            dragPosition = Parent.ToLocalSpace(e.ScreenSpaceMousePosition) + dragOffset;
            Position = dragPosition;
            this.FadeTo(0.85f, 80);
            return true;
        }

        protected override void OnDrag(DragEvent e)
            => dragPosition = Parent!.ToLocalSpace(e.ScreenSpaceMousePosition) + dragOffset;

        protected override void OnDragEnd(DragEndEvent e)
        {
            dragging = false;
            this.FadeIn(120);

            // settle from the release point. if the drop changes this marker's lane, the next lane update re-bases the
            // offset onto the new lane so the badge flies there; an invalid drop simply returns to the original lane.
            snapFrom(dragPosition - lanePosition);
            dropped(this, e.ScreenSpaceMousePosition);
        }

        public Popover GetPopover() => new HitSoundingSamplePopover(Node, model);

        private string buildLabelText()
        {
            string label = string.Join(string.Empty, Node.Samples.Where(s => s.Bank == Bank).Select(s => s.Name switch
            {
                HitSampleInfo.HIT_NORMAL => Node.IsBody ? "S" : "N",
                HitSampleInfo.HIT_WHISTLE => Node.IsBody ? "SW" : "W",
                HitSampleInfo.HIT_FINISH => "F",
                HitSampleInfo.HIT_CLAP => "C",
                _ => s.Name
            }));

            return label.Length == 0 ? (Node.IsBody ? "S" : "N") : label;
        }

        private void applyLayout()
        {
            if (Label == null || fontStore == null)
                return;

            Label.AllowMultiline = false;

            LabelContainer.AutoSizeAxes = Axes.None;
            LabelContainer.Height = HitSoundingLayout.SAMPLE_HEIGHT;
            LabelContainer.CornerRadius = HitSoundingLayout.SAMPLE_CORNER_RADIUS;

            float maximumWidth = gridCellWidth > 0 ? gridCellWidth : float.PositiveInfinity;

            // at extreme zoom levels a cell can be narrower than the usual horizontal padding on its own.
            // reduce the padding rather than clipping the label, while keeping the standard 6px whenever it fits.
            float horizontalPadding = HitSoundingLayout.SAMPLE_HORIZONTAL_PADDING;

            if (maximumWidth < horizontalPadding * 2 + 1)
                horizontalPadding = Math.Max(0, (maximumWidth - 1) / 2);

            Label.Padding = new MarginPadding { Horizontal = horizontalPadding };

            float fontSize = HitSoundingLayout.SAMPLE_FONT_SIZE;
            FontUsage font = OsuFont.Default.With(size: fontSize, weight: FontWeight.SemiBold);
            float textWidth = measureText(labelText, font);

            float desiredWidth = Math.Max(HitSoundingLayout.SAMPLE_HEIGHT, textWidth + horizontalPadding * 2);

            if (desiredWidth > maximumWidth)
            {
                float availableTextWidth = maximumWidth - horizontalPadding * 2;
                float scale = textWidth > 0 ? Math.Clamp(availableTextWidth / textWidth, 0.01f, 1) : 1;

                font = OsuFont.Default.With(size: Math.Max(0.01f, fontSize * scale), weight: FontWeight.SemiBold);
                textWidth = measureText(labelText, font);

                desiredWidth = Math.Min(maximumWidth, Math.Max(HitSoundingLayout.SAMPLE_HEIGHT, textWidth + horizontalPadding * 2));
            }

            Label.Font = font;
            LabelContainer.Width = desiredWidth;
        }

        private float measureText(string text, FontUsage font)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            var builder = new TextBuilder(fontStore, font);
            builder.AddText(text);
            return builder.Bounds.X;
        }
    }
}
