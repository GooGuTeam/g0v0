// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Screens.Edit.HitSounding
{
    public partial class HitSoundingPreviewPanel : CompositeDrawable
    {
        private readonly HitSoundingEditModel model;
        private readonly HitSoundingPreviewState state;
        private FillFlowContainer toolbar = null!;
        private FillFlowContainer<HitSoundingPreviewCard> cards = null!;
        private FormTextBox search = null!;
        private FormNumberBox radius = null!;
        private FormCheckBox highlight = null!;
        private bool invalid = true;
        private float lastWidth;
        private Container previewContent = null!;
        private HitSoundingSampleGallery gallery = null!;

        [Resolved]
        private HitSoundingSampleLibrary library { get; set; } = null!;

        [Resolved]
        private EditorBeatmap beatmap { get; set; } = null!;

        [Resolved]
        private HitSoundingSamplePlayback playback { get; set; } = null!;

        public HitSoundingPreviewPanel(HitSoundingEditModel model, HitSoundingPreviewState state)
        {
            this.model = model;
            this.state = state;
            RelativeSizeAxes = Axes.Both;
            model.Changed += invalidate;
            model.SelectedNodes.CollectionChanged += selectionChanged;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both, Masking = true, CornerRadius = 5,
                    Child = new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background5 }
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both, Padding = new MarginPadding(20),
                    Children = new Drawable[]
                    {
                        new PreviewTabControl(showGallery)
                        {
                            RelativeSizeAxes = Axes.X, Height = 30,
                            Items = new[] { HitSoundingStrings.Preview, HitSoundingStrings.SampleGallery }
                        },
                        previewContent = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Top = 40 },
                            Child = new OsuScrollContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                ScrollbarOverlapsContent = true,
                                Child = new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical, Spacing = new Vector2(0, 10),
                                    Padding = new MarginPadding { Right = 10 },
                                    Children = new Drawable[]
                                    {
                                        toolbar = new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Full, Spacing = new Vector2(10),
                                            Children = new Drawable[]
                                            {
                                                search = new FormTextBox
                                                {
                                                    Caption = HitSoundingStrings.SetId, PlaceholderText = HitSoundingStrings.SearchSets, HintText = HitSoundingStrings.TooltipSearchSets,
                                                    Current = { Value = string.Empty }
                                                },
                                                radius = new FormNumberBox(true)
                                                {
                                                    Caption = HitSoundingStrings.PreviewLength, HintText = HitSoundingStrings.TooltipPreviewLength,
                                                    SelectAllOnFocus = true, Current = { Value = state.Radius.Value.ToString("0.000", CultureInfo.InvariantCulture) }
                                                },
                                                highlight = new FormCheckBox
                                                {
                                                    Caption = HitSoundingStrings.PreviewHighlights, HintText = HitSoundingStrings.TooltipPreviewHighlights,
                                                    ExtendedHeight = true, Current = { BindTarget = state.ShowHighlight }
                                                }
                                            }
                                        },
                                        cards = new FillFlowContainer<HitSoundingPreviewCard>
                                        {
                                            RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Full, Spacing = new Vector2(10)
                                        }
                                    }
                                }
                            }
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Top = 40 },
                            Child = gallery = new HitSoundingSampleGallery { Alpha = 0 }
                        }
                    }
                }
            };
            search.Current.BindValueChanged(_ => invalidate());
            radius.OnCommit += (_, _) =>
            {
                if (double.TryParse(radius.Current.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value))
                    state.Radius.Value = value;
                radius.Current.Value = state.Radius.Value.ToString("0.000", CultureInfo.InvariantCulture);
                playback.StopSetPreview();
            };
            library.Changed += invalidate;
            if (beatmap.BeatmapSkin != null)
                beatmap.BeatmapSkin.BeatmapSkinChanged += invalidate;
        }

        private void showGallery(bool visible)
        {
            if (!IsLoaded)
                return;

            previewContent.FadeTo(visible ? 0 : 1, 180, Easing.OutQuint);
            previewContent.MoveToX(visible ? -10 : 0, 180, Easing.OutQuint);
            gallery.FadeTo(visible ? 1 : 0, 180, Easing.OutQuint);
            gallery.MoveToX(visible ? 0 : 10, 180, Easing.OutQuint);
            playback.StopSetPreview();
            library.StopPreview();
        }

        private void invalidate() => invalid = true;

        private void selectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            invalid = true;

            if (IsLoaded)
                playback.StopSetPreview();
        }

        private void rebuildCards()
        {
            var sets = new SortedSet<int> { 0, 1 };
            sets.UnionWith(library.Sets);

            if (beatmap.BeatmapSkin != null)
            {
                foreach (var set in beatmap.BeatmapSkin.GetAvailableSampleSets())
                    sets.Add(set.SampleSetIndex);
            }

            foreach (var node in model.SelectedNodes)
                sets.Add(node.SetIndex);

            string filter = search.Current.Value.Trim();
            cards.Clear();

            foreach (int index in sets)
            {
                if (index.ToString(CultureInfo.InvariantCulture).Contains(filter, StringComparison.OrdinalIgnoreCase))
                    cards.Add(new HitSoundingPreviewCard(index, play));
            }

            lastWidth = 0;
        }

        private void play(int setIndex)
        {
            if (playback.PreviewSetIndex == setIndex)
            {
                playback.StopSetPreview();
                return;
            }

            if (state.StartTime is double start && state.EndTime is double end)
                playback.StartSetPreview(setIndex, start, end, model.SelectedNodes.ToArray());
        }

        protected override void Update()
        {
            base.Update();

            if (invalid)
            {
                rebuildCards();
                invalid = false;
            }

            float width = Math.Max(0, toolbar.DrawWidth);

            if (lastWidth != width)
            {
                lastWidth = width;
                int columns = Math.Clamp((int)((width + 10) / 250), 1, 5);
                float cardWidth = Math.Max(1, (width - (columns - 1) * 10) / columns);
                search.RelativeSizeAxes = radius.RelativeSizeAxes = Axes.None;
                search.Width = radius.Width = cardWidth;
                highlight.RelativeSizeAxes = Axes.None;
                highlight.Width = Math.Min(276, width);

                foreach (var card in cards)
                    card.Width = cardWidth;
            }

            int? selectedSet = model.SelectedNodes.Count == 0 ? null : model.SelectedNodes[0].SetIndex;

            foreach (var node in model.SelectedNodes)
            {
                if (node.SetIndex != selectedSet)
                {
                    selectedSet = null;
                    break;
                }
            }

            foreach (var card in cards)
                card.Refresh(selectedSet == card.SetIndex, playback.PreviewSetIndex == card.SetIndex, model.SelectedNodes.Count > 0);
        }

        private partial class PreviewTabControl : OverlayTabControl<LocalisableString>
        {
            public PreviewTabControl(Action<bool> showGallery)
            {
                Current.BindValueChanged(e => showGallery(e.NewValue.Equals(HitSoundingStrings.SampleGallery)));
                BarHeight = 0;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours) => StripColour = colours.Highlight1;

            protected override TabItem<LocalisableString> CreateTabItem(LocalisableString value) => new PreviewTabItem(value);

            private partial class PreviewTabItem : OverlayTabItem, IHasTooltip
            {
                public LocalisableString TooltipText => Value.Equals(HitSoundingStrings.SampleGallery)
                    ? HitSoundingStrings.TooltipSampleGallery
                    : HitSoundingStrings.TooltipPreview;

                public PreviewTabItem(LocalisableString value)
                    : base(value)
                {
                    Text.Text = value;
                }
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            model.Changed -= invalidate;
            model.SelectedNodes.CollectionChanged -= selectionChanged;
            if (IsLoaded)
                library.Changed -= invalidate;
            if (IsLoaded && beatmap.BeatmapSkin != null)
                beatmap.BeatmapSkin.BeatmapSkinChanged -= invalidate;
            base.Dispose(isDisposing);
        }
    }
}
