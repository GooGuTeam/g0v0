// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Entries;
using osu.Game.Overlays.Settings.Rendering;
using osu.Game.Screens.Play.PlayerSettings;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections.Audio
{
    public partial class AudioOffsetAdjustControl : CompositeDrawable
    {
        private const int notch_container_height = 10;

        public Bindable<double> Current
        {
            get => current.Current;
            set => current.Current = value;
        }

        private readonly BindableNumberWithCurrent<double> current = new BindableNumberWithCurrent<double>();

        private readonly IBindableList<SessionAverageHitErrorTracker.DataPoint> averageHitErrorHistory = new BindableList<SessionAverageHitErrorTracker.DataPoint>();

        public readonly Bindable<double?> SuggestedOffset = new Bindable<double?>();

        private Container<Circle> notchContainer = null!;
        private SettingsNote hintNote = null!;
        private RoundedButton applySuggestion = null!;

        // the stable layout (see ISettingsRenderer.UseStableLayouts): a stable slider, the hint as stable text, and a stable button.
        private NoteEntry? stableHint;
        private ButtonEntry? stableApply;

        [Resolved(canBeNull: true)]
        private ISettingsRenderer? renderer { get; set; }

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(SessionAverageHitErrorTracker hitErrorTracker)
        {
            averageHitErrorHistory.BindTo(hitErrorTracker.AverageHitErrorHistory);

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            // the notches have no stable counterpart; they are kept (unshown) so the history bookkeeping stays the same.
            notchContainer = new Container<Circle>();

            if (renderer?.UseStableLayouts == true)
            {
                loadStable(renderer, hitErrorTracker);
                return;
            }

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(SettingsSection.ITEM_SPACING_V2),
                Children = new Drawable[]
                {
                    new SettingsItemV2(new FormSliderBar<double>
                    {
                        Caption = AudioSettingsStrings.AudioOffset,
                        RelativeSizeAxes = Axes.X,
                        Current = { BindTarget = Current },
                        KeyboardStep = 1,
                        LabelFormat = v => $"{v:N0} ms",
                        TooltipFormat = BeatmapOffsetControl.GetOffsetExplanatoryText,
                    }),
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = SettingsPanel.CONTENT_PADDING,
                        Children = new Drawable[]
                        {
                            notchContainer = new Container<Circle>
                            {
                                RelativeSizeAxes = Axes.X,
                                Width = 0.5f,
                                Height = notch_container_height,
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Padding = new MarginPadding
                                {
                                    Horizontal = FormSliderBar<double>.InnerSlider.NUB_WIDTH / 2
                                },
                            },
                            hintNote = new SettingsNote { RelativeSizeAxes = Axes.X },
                        }
                    },
                    applySuggestion = new RoundedButton
                    {
                        RelativeSizeAxes = Axes.X,
                        Text = AudioSettingsStrings.ApplySuggestedOffset,
                        Padding = SettingsPanel.CONTENT_PADDING,
                        Action = () =>
                        {
                            if (SuggestedOffset.Value.HasValue)
                                current.Value = SuggestedOffset.Value.Value;
                            hitErrorTracker.ClearHistory();
                        }
                    }
                }
            };
        }

        private void loadStable(ISettingsRenderer stable, SessionAverageHitErrorTracker hitErrorTracker)
        {
            stableHint = new NoteEntry { AttachedToPrevious = true };
            stableApply = new ButtonEntry
            {
                Caption = AudioSettingsStrings.ApplySuggestedOffset,
                Action = () =>
                {
                    if (SuggestedOffset.Value.HasValue)
                        current.Value = SuggestedOffset.Value.Value;
                    hitErrorTracker.ClearHistory();
                },
            };

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(SettingsSection.ITEM_SPACING_V2),
                Children = new[]
                {
                    stable.CreateSlider(new SliderEntry<double>
                    {
                        Caption = AudioSettingsStrings.AudioOffset,
                        Current = current,
                        KeyboardStep = 1,
                        LabelFormat = v => $"{v:N0} ms",
                        TooltipFormat = BeatmapOffsetControl.GetOffsetExplanatoryText,
                    }),
                    stable.CreateNote(stableHint),
                    stable.CreateButton(stableApply),
                },
            };
        }

        private void setHint(LocalisableString text, bool canApply, bool showNotches)
        {
            var note = new SettingsNote.Data(text, SettingsNote.Type.Informational);

            if (stableHint != null)
            {
                stableHint.Note.Value = note;
                stableApply!.Enabled.Value = canApply;
                return;
            }

            applySuggestion.Enabled.Value = canApply;
            notchContainer.FadeTo(showNotches ? 1 : 0);
            hintNote.Current.Value = note;
            hintNote.MoveToY(showNotches ? notch_container_height : -SettingsSection.ITEM_SPACING_V2, 200, Easing.OutQuint);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            averageHitErrorHistory.BindCollectionChanged(updateDisplay, true);
            current.BindValueChanged(_ => updateHintText());
            SuggestedOffset.BindValueChanged(_ => updateHintText(), true);
        }

        private void updateDisplay(object? _, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (SessionAverageHitErrorTracker.DataPoint dataPoint in e.NewItems!)
                    {
                        notchContainer.ForEach(n => n.Alpha *= 0.95f);
                        notchContainer.Add(new Circle
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = 2,
                            RelativePositionAxes = Axes.X,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Colour = colourProvider.Light1,
                            X = getXPositionForOffset(dataPoint.SuggestedGlobalAudioOffset)
                        });
                    }

                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (SessionAverageHitErrorTracker.DataPoint dataPoint in e.OldItems!)
                    {
                        var notch = notchContainer.FirstOrDefault(n => n.X == getXPositionForOffset(dataPoint.SuggestedGlobalAudioOffset));
                        Debug.Assert(notch != null);
                        notchContainer.Remove(notch, true);
                    }

                    break;

                case NotifyCollectionChangedAction.Reset:
                    notchContainer.Clear();
                    break;
            }

            SuggestedOffset.Value = averageHitErrorHistory.Any() ? Math.Round(averageHitErrorHistory.Average(dataPoint => dataPoint.SuggestedGlobalAudioOffset)) : null;
        }

        private float getXPositionForOffset(double offset) => (float)(Math.Clamp(offset, current.MinValue, current.MaxValue) / (2 * current.MaxValue));

        private void updateHintText()
        {
            if (SuggestedOffset.Value == null)
                setHint(AudioSettingsStrings.SuggestedOffsetNote, false, false);
            else if (Math.Abs(SuggestedOffset.Value.Value - current.Value) < 1)
                setHint(AudioSettingsStrings.SuggestedOffsetCorrect(averageHitErrorHistory.Count), false, true);
            else
            {
                setHint(AudioSettingsStrings.SuggestedOffsetValueReceived(averageHitErrorHistory.Count, SuggestedOffset.Value.Value.ToStandardFormattedString(0)),
                    true, true);
            }
        }
    }
}
