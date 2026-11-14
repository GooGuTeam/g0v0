// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// The sample gallery: every sample set as an always-expanded tree on the left, and the beatmap's sample files
    /// as draggable tiles on the right.
    /// </summary>
    /// <remarks>
    /// A tile dragged out of the file library is carried by this drawable (rather than by the tile itself) so the
    /// floating visual survives a rebuild of the file tiles and stays above both halves of the gallery.
    /// </remarks>
    public partial class HitSoundingSampleGallery : CompositeDrawable, IHasPopover
    {
        /// <summary>
        /// The horizontal gap between the sample set tree and the file library.
        /// </summary>
        public const float PANEL_GAP = 20;

        private const float drag_proxy_offset = 14;

        private Container dragLayer = null!;
        private DragProxy? dragProxy;
        private Popover? pendingPopover;
        private bool wasVisible = true;

        [Resolved]
        private HitSoundingSampleLibrary samples { get; set; } = null!;

        /// <summary>
        /// The sample set tree on the left of the gallery.
        /// </summary>
        public HitSoundingSampleSetsTree Tree { get; private set; } = null!;

        /// <summary>
        /// The sample file library on the right of the gallery.
        /// </summary>
        public HitSoundingSampleFileLibrary FileLibrary { get; private set; } = null!;

        /// <summary>
        /// Whether the gallery and all of its ancestors are currently faded in. A hidden gallery stops polling the
        /// sample library.
        /// </summary>
        public bool IsVisibleInHierarchy { get; private set; } = true;

        /// <summary>
        /// Whether a file tile is currently being dragged onto the tree.
        /// </summary>
        public bool IsDraggingFile => DraggingFilename != null;

        /// <summary>
        /// The file being dragged out of the library, or <c>null</c> when nothing is being dragged.
        /// </summary>
        public string? DraggingFilename { get; private set; }

        public HitSoundingSampleGallery()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Width = 0.5f,
                    Padding = new MarginPadding { Right = PANEL_GAP / 2 },
                    Child = Tree = new HitSoundingSampleSetsTree(this),
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Width = 0.5f,
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Padding = new MarginPadding { Left = PANEL_GAP / 2 },
                    Child = FileLibrary = new HitSoundingSampleFileLibrary(this),
                },
                dragLayer = new Container
                {
                    Name = "Sample drag layer",
                    RelativeSizeAxes = Axes.Both,
                    Depth = float.MinValue,
                },
            };
        }

        protected override void Update()
        {
            base.Update();

            if (DraggingFilename != null && GetContainingInputManager() is { } inputManager)
                updateDrag(inputManager.CurrentState.Mouse.Position);

            bool visible = isVisibleInHierarchy();
            IsVisibleInHierarchy = visible;

            if (wasVisible && !visible)
                samples.StopPreview();

            wasVisible = visible;
        }

        /// <summary>
        /// Stops any audition started from this gallery.
        /// </summary>
        public void StopPreview() => samples.StopPreview();

        /// <summary>
        /// The name a file is displayed under: its alias when one is set, otherwise the original filename.
        /// </summary>
        public string GetDisplayName(string filename)
        {
            string alias = samples.GetAlias(filename);
            return string.IsNullOrEmpty(alias) ? Path.GetFileName(filename) : alias;
        }

        /// <summary>
        /// Starts dragging <paramref name="filename"/> out of the file library.
        /// </summary>
        public void BeginFileDrag(string filename)
        {
            if (DraggingFilename != null)
                return;

            DraggingFilename = filename;
            FileLibrary.RefreshDragging();

            dragLayer.Add(dragProxy = new DragProxy { Alpha = 0 });
            dragProxy.FadeIn(120, Easing.OutQuint);
        }

        /// <summary>
        /// Releases the dragged file, assigning it to the slot underneath the pointer when there is one.
        /// </summary>
        public void EndFileDrag(Vector2 screenSpacePosition)
        {
            if (DraggingFilename == null)
                return;

            HitSoundingSampleSlot? target = Tree.GetSlotAt(screenSpacePosition);
            string filename = DraggingFilename;

            cancelDrag();

            if (target != null)
                samples.Assign(target.SetIndex, target.Bank, target.Sample, filename);
        }

        /// <summary>
        /// Abandons an in-progress drag without assigning anything.
        /// </summary>
        public void CancelFileDrag()
        {
            if (DraggingFilename != null)
                cancelDrag();
        }

        /// <summary>
        /// Imports sample files into the beatmap set that this gallery belongs to. The chooser stays open after each
        /// selection so several files can be imported in a row.
        /// </summary>
        public void AddSampleFiles()
        {
            pendingPopover = new SampleFileChooserPopover(path => samples.ImportFiles(new[] { path }));
            this.ShowPopover();
        }

        /// <summary>
        /// Opens a file chooser and assigns the chosen sample file to a single slot.
        /// </summary>
        public void AssignFromFilePicker(SampleSlotReference slot)
        {
            pendingPopover = new SampleFileChooserPopover(path => importForSlot(path, slot));
            this.ShowPopover();
        }

        public Popover? GetPopover()
        {
            Popover? popover = pendingPopover;
            pendingPopover = null;
            return popover;
        }

        internal void BeginAliasEdit() => FileLibrary.BeginAliasEdit();

        internal void EndAliasEdit() => FileLibrary.EndAliasEdit();

        private void updateDrag(Vector2 screenSpacePosition)
        {
            dragProxy?.Position = ToLocalSpace(screenSpacePosition) + new Vector2(drag_proxy_offset);

            Tree.UpdateDragHover(screenSpacePosition);
        }

        private void cancelDrag()
        {
            DraggingFilename = null;

            dragProxy?.Expire();
            dragProxy = null;

            Tree.UpdateDragHover(null);
            FileLibrary.RefreshDragging();
        }

        private void importForSlot(string path, SampleSlotReference slot)
        {
            var before = new HashSet<string>(samples.Files, StringComparer.Ordinal);

            samples.ImportFiles(new[] { path });

            if (findImported(path, before) is string imported)
                samples.Assign(slot.Set, slot.Bank, slot.Sample, imported);
        }

        /// <summary>
        /// Finds the beatmap resource the library created for an imported file. The library keeps the filename (lower
        /// casing the extension) and appends <c>-1</c>, <c>-2</c>… on collision, so an exact name match wins and a
        /// shared stem is used as the fallback.
        /// </summary>
        private string? findImported(string sourcePath, HashSet<string> before)
        {
            string sourceName = Path.GetFileName(sourcePath);
            string sourceStem = Path.GetFileNameWithoutExtension(sourcePath);

            string? exact = null;
            string? stem = null;

            foreach (string file in samples.Files)
            {
                if (before.Contains(file))
                    continue;

                string name = Path.GetFileName(file);

                if (string.Equals(name, sourceName, StringComparison.OrdinalIgnoreCase))
                    exact ??= file;
                else if (Path.GetFileNameWithoutExtension(name).StartsWith(sourceStem, StringComparison.OrdinalIgnoreCase))
                    stem ??= file;
            }

            return exact ?? stem;
        }

        private bool isVisibleInHierarchy()
        {
            for (Drawable? drawable = this; drawable != null; drawable = drawable.Parent)
            {
                if (drawable.Alpha <= 0)
                    return false;
            }

            return true;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && IsLoaded)
                samples.StopPreview();

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// The floating audio file icon that follows the pointer while a file tile is dragged.
        /// </summary>
        private partial class DragProxy : CompositeDrawable
        {
            private const float icon_size = 40;

            private readonly Container iconContainer;
            private readonly SpriteIcon icon;

            public DragProxy()
            {
                Name = "Sample drag preview";
                Size = new Vector2(icon_size);

                InternalChild = iconContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    Child = icon = new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Icon = FontAwesome.Regular.FileAudio,
                        Size = new Vector2(icon_size),
                    },
                };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                icon.Colour = colours.Content1;
                iconContainer.EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Colour = new Color4(0, 0, 0, 0.55f),
                    Radius = 8,
                    Offset = new Vector2(0, 2),
                };
            }
        }

        /// <summary>
        /// The in-game file chooser used by "Add files" and by an empty slot. Reuses the framework's file browser,
        /// which falls back to the native system selector on platforms that provide one.
        /// </summary>
        private partial class SampleFileChooserPopover : FormFileSelector.FileChooserPopover
        {
            private readonly Action<string> chosen;

            public SampleFileChooserPopover(Action<string> chosen)
                : base(SupportedExtensions.AUDIO_EXTENSIONS, new Bindable<FileInfo?>(), null, false)
            {
                this.chosen = chosen;
            }

            protected override void OnFileSelected(FileInfo? file)
            {
                // Deliberately does not close the popover, so several files can be imported in a row.
                if (file != null)
                    chosen(file.FullName);
            }
        }
    }

    /// <summary>
    /// Identifies one assignable sample slot: a set index, a bank and a sample type.
    /// </summary>
    public readonly struct SampleSlotReference : IEquatable<SampleSlotReference>
    {
        public readonly int Set;
        public readonly string Bank;
        public readonly string Sample;

        public SampleSlotReference(int set, string bank, string sample)
        {
            Set = set;
            Bank = bank;
            Sample = sample;
        }

        public bool Equals(SampleSlotReference other) => Set == other.Set && Bank == other.Bank && Sample == other.Sample;
        public override bool Equals(object? obj) => obj is SampleSlotReference other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Set, Bank, Sample);
        public override string ToString() => $"{Set}/{Bank}/{Sample}";
    }
}
