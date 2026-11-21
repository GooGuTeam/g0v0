// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using ManagedBass;
using osu.Framework.Allocation;
using osu.Framework.Audio.Callbacks;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// One sample file of the beatmap, displayed as a draggable audition row with an editable alias.
    /// </summary>
    public partial class HitSoundingSampleFileTile : CompositeDrawable, IHasTooltip
    {
        public const float TILE_HEIGHT = 48;
        public const float CORNER_RADIUS = 5;

        private const double double_click_interval = 400;

        private readonly HitSoundingSampleGallery gallery;

        private OverlayColourProvider colours = null!;
        private AliasTooltipArea aliasArea = null!;
        private SpriteIcon playIcon = null!;
        private TruncatingSpriteText aliasText = null!;
        private AliasEditor aliasEditor = null!;
        private TruncatingSpriteText filenameText = null!;
        private Container labelBlock = null!;
        private OsuSpriteText durationText = null!;

        private string displayedAlias = string.Empty;
        private bool? displayedPlaying;
        private bool dragStarted;
        private bool dragOccurred;
        private double lastAliasClick;

        [Resolved]
        private HitSoundingSampleLibrary samples { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> workingBeatmap { get; set; } = null!;

        /// <summary>
        /// The beatmap resource this tile represents.
        /// </summary>
        public string Filename { get; }

        public LocalisableString TooltipText => HitSoundingStrings.TooltipFileTileDrag;

        /// <summary>
        /// Whether the alias of this tile is currently being edited inline.
        /// </summary>
        public bool IsEditingAlias { get; private set; }

        public HitSoundingSampleFileTile(string filename, HitSoundingSampleGallery gallery)
        {
            Filename = filename;
            this.gallery = gallery;

            Name = $"Sample file: {filename}";
            RelativeSizeAxes = Axes.X;
            Height = TILE_HEIGHT;
            Masking = true;
            CornerRadius = CORNER_RADIUS;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            this.colours = colours;

            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background4 },
                createAudition(),
                createLabelBlock(),
                durationText = new OsuSpriteText
                {
                    Name = $"Sample duration: {Filename}",
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -10,
                    Font = OsuFont.Default.With(size: 12),
                    Colour = colours.Light3,
                    Text = getDuration(),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            refreshAlias();
            refreshPlayback();
        }

        protected override void Update()
        {
            base.Update();

            float labelWidth = Math.Max(0, DrawWidth - 64 - durationText.DrawWidth - 20);
            labelBlock.Width = labelWidth;
            aliasText.MaxWidth = labelWidth;
            filenameText.MaxWidth = labelWidth;

            if (IsEditingAlias || !gallery.IsVisibleInHierarchy)
                return;

            // The alias can only change together with a library change, which is pushed via RefreshFromLibrary(). Only the
            // audition state is polled here, as it ends whenever the sample channel finishes.
            refreshPlayback();
        }

        /// <summary>
        /// Dims this tile while it is the source of a drag.
        /// </summary>
        public void RefreshDragging() => Alpha = gallery.DraggingFilename == Filename ? 0.4f : 1;

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            dragStarted = false;
            dragOccurred = false;

            return true;
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            base.OnDragStart(e);

            if (IsEditingAlias)
                return false;

            dragStarted = true;
            dragOccurred = true;

            gallery.BeginFileDrag(Filename);

            return true;
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            base.OnDragEnd(e);

            if (!dragStarted)
                return;

            dragStarted = false;

            gallery.EndFileDrag(e.ScreenSpaceMousePosition);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            base.OnMouseUp(e);

            if (dragOccurred)
            {
                dragOccurred = false;
                return;
            }

            if (IsEditingAlias)
                return;

            if (aliasArea.ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
            {
                handleAliasClick();
                return;
            }

            samples.TogglePreview(Filename);
        }

        private Drawable createAudition() => playIcon = new SpriteIcon
        {
            Name = $"Sample audition: {Filename}",
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            X = 12,
            Size = new Vector2(20),
            Icon = FontAwesome.Regular.PlayCircle,
            Colour = colours.Content1,
        };

        private Drawable createLabelBlock() => labelBlock = new Container
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            X = 44,
            Height = 36,
            Children = new Drawable[]
            {
                aliasArea = new AliasTooltipArea
                {
                    Name = $"Sample alias: {Filename}",
                    RelativeSizeAxes = Axes.X,
                    Height = 18,
                    Child = aliasText = new TruncatingSpriteText
                    {
                        MaxWidth = 1,
                        ShowTooltip = false,
                        Font = OsuFont.Default.With(size: 14, weight: FontWeight.Medium),
                    },
                },
                filenameText = new TruncatingSpriteText
                {
                    Name = $"Sample filename: {Filename}",
                    Position = new Vector2(0, 20),
                    MaxWidth = 1,
                    Font = OsuFont.Default.With(size: 12),
                    Colour = colours.Light3,
                    Text = Path.GetFileName(Filename),
                },
                aliasEditor = new AliasEditor
                {
                    Name = $"Sample alias editor: {Filename}",
                    RelativeSizeAxes = Axes.X,
                    Height = 18,
                    Alpha = 0,
                    Committed = commitAlias,
                    Cancelled = cancelAliasEdit,
                },
            },
        };

        private string getDuration()
        {
            string? path = workingBeatmap.Value.BeatmapSetInfo.GetPathForFile(Filename);
            if (path == null)
                return "--:--";

            using Stream? stream = workingBeatmap.Value.GetStream(path);
            if (stream == null)
                return "--:--";

            using var callbacks = new FileCallbacks(new DataStreamFileProcedures(stream));
            int decodeStream = Bass.CreateStream(StreamSystem.NoBuffer, BassFlags.Decode | BassFlags.Prescan, callbacks.Callbacks, callbacks.Handle);
            if (decodeStream == 0)
                return "--:--";

            try
            {
                long length = Bass.ChannelGetLength(decodeStream);
                if (length < 0)
                    return "--:--";

                double seconds = Bass.ChannelBytes2Seconds(decodeStream, length);
                if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                    return "--:--";

                long roundedSeconds = (long)Math.Ceiling(seconds);
                return $"{roundedSeconds / 60}:{roundedSeconds % 60:00}";
            }
            finally
            {
                Bass.StreamFree(decodeStream);
            }
        }

        /// <summary>
        /// Re-reads the alias displayed by this tile after a library change.
        /// </summary>
        internal void RefreshFromLibrary() => refreshAlias();

        private void refreshAlias()
        {
            string alias = samples.GetAlias(Filename);

            if (alias == displayedAlias)
                return;

            displayedAlias = alias;
            aliasText.Text = alias;
        }

        private void refreshPlayback()
        {
            bool playing = samples.IsPlaying(Filename);

            if (displayedPlaying == playing)
                return;

            displayedPlaying = playing;
            playIcon.Colour = playing ? colours.Highlight1 : colours.Content1;
        }

        private void handleAliasClick()
        {
            double now = Time.Current;

            if (now - lastAliasClick <= double_click_interval)
            {
                lastAliasClick = 0;
                beginAliasEdit();
            }
            else
                lastAliasClick = now;
        }

        private void beginAliasEdit()
        {
            if (IsEditingAlias)
                return;

            IsEditingAlias = true;
            gallery.BeginAliasEdit();

            aliasEditor.Text = displayedAlias;
            aliasEditor.Alpha = 1;
            aliasText.Alpha = 0;
            Schedule(() => GetContainingFocusManager()?.ChangeFocus(aliasEditor));
        }

        private void commitAlias(string value)
        {
            if (!IsEditingAlias)
                return;

            IsEditingAlias = false;

            aliasEditor.Alpha = 0;
            aliasText.Alpha = 1;

            samples.SetAlias(Filename, value.Trim());

            // Rebuilds are released last so this tile is not disposed while it is still handling the commit.
            gallery.EndAliasEdit();
        }

        private void cancelAliasEdit()
        {
            if (!IsEditingAlias)
                return;

            IsEditingAlias = false;

            aliasEditor.Alpha = 0;
            aliasText.Alpha = 1;
            gallery.EndAliasEdit();
        }

        private partial class AliasTooltipArea : Container, IHasCustomTooltip<LocalisableString>
        {
            public LocalisableString TooltipContent => HitSoundingStrings.TooltipFileTileAlias;

            public ITooltip<LocalisableString> GetCustomTooltip() => new HitSoundingTooltip(HitSoundingTooltipKind.Alias);
        }

        private partial class AliasEditor : OsuTextBox
        {
            public Action<string>? Committed { get; set; }

            public Action? Cancelled { get; set; }

            public AliasEditor()
            {
                Height = 18;
                FontSize = 14;
                LengthLimit = 64;
                SelectAllOnFocus = true;

                // The tile already provides the field background.
                BackgroundUnfocused = Color4.Transparent;
                BackgroundFocused = Color4.Transparent;

                OnCommit += (_, _) => Committed?.Invoke(Current.Value);
            }

            public void Cancel()
            {
                Cancelled?.Invoke();
                KillFocus();
            }

            protected override bool OnKeyDown(KeyDownEvent e)
            {
                if (e.Key == Key.Escape)
                {
                    Cancel();
                    return true;
                }

                return base.OnKeyDown(e);
            }
        }
    }
}
