// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// One sample set: the set heading followed by its Normal / Soft / Drum banks and their four sample slots.
    /// </summary>
    public partial class HitSoundingSampleSetColumn : CompositeDrawable
    {
        public int SetIndex { get; }

        /// <summary>
        /// The twelve slot rows of this set, in display order.
        /// </summary>
        public IReadOnlyList<HitSoundingSampleSlot> Slots => slots;

        private readonly HitSoundingSampleGallery gallery;
        private readonly List<HitSoundingSampleSlot> slots = new List<HitSoundingSampleSlot>();

        public HitSoundingSampleSetColumn(int setIndex, HitSoundingSampleGallery gallery)
        {
            SetIndex = setIndex;
            this.gallery = gallery;

            Name = $"Sample set: {setIndex}";
            Size = new Vector2(HitSoundingSampleSetsTree.COLUMN_WIDTH, HitSoundingSampleSetsTree.TREE_HEIGHT);
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 1),
                Children = new[]
                {
                    createHeading(colours),
                    createBank(HitSoundingSampleTokens.NORMAL, colours),
                    createBank(HitSoundingSampleTokens.SOFT, colours),
                    createBank(HitSoundingSampleTokens.DRUM, colours),
                },
            };
        }

        private Drawable createHeading(OverlayColourProvider colours) => new Container
        {
            RelativeSizeAxes = Axes.X,
            Height = HitSoundingSampleSetsTree.SET_HEADING_HEIGHT,
            Child = new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = HitSoundingStrings.Set(SetIndex),
                Font = OsuFont.Default.With(size: 16, weight: FontWeight.Bold),
                Colour = colours.Content1,
            },
        };

        private Drawable createBank(string bank, OverlayColourProvider colours)
        {
            var bankSlots = new List<Drawable>
            {
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = HitSoundingSampleSetsTree.BANK_HEADING_HEIGHT,
                    Padding = new MarginPadding { Left = HitSoundingSampleSetsTree.BANK_INDENT },
                    Child = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = HitSoundingSampleTokens.BankName(bank),
                        Font = OsuFont.Default.With(size: 12, weight: FontWeight.Medium),
                        Colour = colours.Light3,
                    },
                },
            };

            foreach (string sample in new[] { HitSoundingSampleTokens.HIT_NORMAL, HitSoundingSampleTokens.WHISTLE, HitSoundingSampleTokens.FINISH, HitSoundingSampleTokens.CLAP })
            {
                var slot = new HitSoundingSampleSlot(SetIndex, bank, sample, gallery);
                slots.Add(slot);

                bankSlots.Add(new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = HitSoundingSampleSetsTree.SLOT_HEIGHT,
                    Padding = new MarginPadding { Left = HitSoundingSampleSetsTree.SLOT_INDENT },
                    Child = slot,
                });
            }

            return new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 1),
                Children = bankSlots,
            };
        }
    }

    /// <summary>
    /// A single assignable sample slot. Assigned rows audition their sample when clicked; empty rows open a file
    /// chooser. Either accepts a dragged file tile.
    /// </summary>
    public partial class HitSoundingSampleSlot : CompositeDrawable, IHasTooltip
    {
        private const float alias_left = 20;
        private const float glyph_reserve = 20;
        private const float corner_radius = 5;

        private readonly HitSoundingSampleGallery gallery;

        private OverlayColourProvider colours = null!;
        private Container surface = null!;
        private Box background = null!;
        private TruncatingSpriteText aliasText = null!;
        private OsuSpriteText glyphText = null!;

        private string displayedAlias = string.Empty;
        private string? dropSourceName;
        private bool initialised;
        private bool playbackActive;

        [Resolved]
        private HitSoundingSampleLibrary samples { get; set; } = null!;

        public int SetIndex { get; }
        public string Bank { get; }
        public string Sample { get; }

        /// <summary>
        /// The assigned sample file, or <c>null</c> when this slot has no beatmap sample.
        /// </summary>
        public string? Filename { get; private set; }

        /// <summary>
        /// Whether a dragged file would be dropped onto this slot.
        /// </summary>
        public bool IsDropTarget { get; private set; }

        public SampleSlotReference Reference => new SampleSlotReference(SetIndex, Bank, Sample);

        public LocalisableString TooltipText
        {
            get
            {
                if (IsDropTarget && dropSourceName != null)
                    return HitSoundingStrings.TooltipDropTarget(dropSourceName, slotLabel);

                if (Filename != null)
                    return HitSoundingStrings.TooltipAssignedSlotRow;

                return LocalisableString.Format("{0}\n{1}", HitSoundingStrings.TooltipEmptySlotClick, HitSoundingStrings.TooltipEmptySlotDrop);
            }
        }

        private LocalisableString slotLabel => LocalisableString.Format("{0} / {1} / {2}",
            HitSoundingStrings.Set(SetIndex),
            HitSoundingSampleTokens.BankName(Bank),
            HitSoundingSampleTokens.SampleName(Sample));

        public HitSoundingSampleSlot(int setIndex, string bank, string sample, HitSoundingSampleGallery gallery)
        {
            SetIndex = setIndex;
            Bank = bank;
            Sample = sample;
            this.gallery = gallery;

            Name = $"Sample slot: {setIndex}/{bank}/{sample}";
            RelativeSizeAxes = Axes.X;
            Height = HitSoundingSampleSetsTree.SLOT_HEIGHT;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            this.colours = colours;

            InternalChild = surface = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = corner_radius,
                Children = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both },
                    new OsuSpriteText
                    {
                        Position = new Vector2(6, 1),
                        Text = HitSoundingSampleTokens.TypeLetter(Sample),
                        Font = OsuFont.Default.With(size: 12, weight: FontWeight.Medium),
                        Colour = colours.Content2,
                    },
                    aliasText = new TruncatingSpriteText
                    {
                        Position = new Vector2(alias_left, 1),
                        ShowTooltip = false,
                        Font = OsuFont.Default.With(size: 12),
                    },
                    glyphText = new OsuSpriteText
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Position = new Vector2(-6, 1),
                        Font = OsuFont.Default.With(size: 12),
                        Colour = colours.Light3,
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            refresh();
        }

        protected override void Update()
        {
            base.Update();

            float aliasWidth = Math.Max(0, DrawWidth - alias_left - glyph_reserve);

            if (aliasText.Width != aliasWidth)
                aliasText.Width = aliasWidth;

            if (!gallery.IsVisibleInHierarchy)
                return;

            refresh();
            updatePlayback();
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (Filename != null)
                samples.TogglePreview(Filename);
            else
                gallery.AssignFromFilePicker(Reference);

            return true;
        }

        /// <summary>
        /// Marks this slot as the drop target of a dragged file tile.
        /// </summary>
        public void SetDropTarget(bool active, string? sourceName)
        {
            if (IsDropTarget == active && dropSourceName == sourceName)
                return;

            IsDropTarget = active;
            dropSourceName = sourceName;

            updateSurface();
            updateGlyph();
        }

        private void refresh()
        {
            string? assignment = samples.GetAssignment(SetIndex, Bank, Sample);
            string alias = assignment == null ? string.Empty : samples.GetAlias(assignment);

            if (initialised && assignment == Filename && alias == displayedAlias)
                return;

            Filename = assignment;
            displayedAlias = alias;
            initialised = true;

            aliasText.Text = assignment == null ? @"—" : alias;
            aliasText.Colour = assignment == null ? colours.Light3 : colours.Content1;

            updateSurface();
            updateGlyph();
        }

        private void updateSurface()
        {
            background.Colour = IsDropTarget ? colours.Dark3 : colours.Background4.Darken(0.1f);
            surface.BorderThickness = IsDropTarget ? 1 : 0;
            surface.BorderColour = colours.Highlight1;
        }

        private void updateGlyph()
        {
            glyphText.Text = IsDropTarget ? @"↓" : Filename != null ? @"▶" : @"+";
            updateGlyphColour();
        }

        private void updatePlayback()
        {
            bool playing = Filename != null && samples.IsPlaying(Filename);

            if (playbackActive == playing)
                return;

            playbackActive = playing;
            updateGlyphColour();
        }

        private void updateGlyphColour() => glyphText.Colour = playbackActive ? colours.Highlight1 : colours.Light3;
    }
}
