// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Allocation;
using osu.Game.Graphics.Containers;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Entries;
using osuTK.Graphics;
using M = osu.Game.Overlays.Settings.Rendering.Stable.StableOptionsMetrics;

namespace osu.Game.Overlays.Settings.Rendering.Stable
{
    /// <summary>
    /// The common frame of an osu!stable options element (<c>OptionElement</c> as laid out by <c>Options.performLayout</c>):
    /// the section guide line, the full-width hover highlight, the "modified" indicator (click to restore the default),
    /// the disabled tint, notes, search filtering and the hint tooltip. See <see cref="StableOptionsMetrics"/> for the sources of all metrics.
    /// </summary>
    public abstract partial class StableSettingsItem : CompositeDrawable, ISettingsItem, IConditionalFilterable, IHasCustomTooltip<LocalisableString>
    {
        /// <summary>
        /// Half of the gap between elements, less the flow spacing that <see cref="SettingsSubsection"/> already adds.
        /// </summary>
        public static readonly float ROW_PADDING = M.ELEMENT_GAP / 2 - SettingsSection.ITEM_SPACING_V2 / 2f;

        /// <summary>
        /// Stable lays elements out on its 274 unit panel; this panel is narrower, so the right margin is kept at stable's 10 units instead.
        /// </summary>
        public const float CONTENT_RIGHT = 10 * M.U;

        protected readonly SettingsEntry Entry;

        private readonly Container content;
        private readonly Box hoverBackground;
        private readonly DefaultIndicator indicator;
        private readonly Box sectionLine;
        private readonly OsuTextFlowContainer noteText;

        private bool? lastDisabled;

        protected StableSettingsItem(SettingsEntry entry)
        {
            Entry = entry;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChildren = new Drawable[]
            {
                hoverBackground = new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Colour = M.HOVER_COLOUR,
                    Alpha = 0,
                    BypassAutoSizeAxes = Axes.Both,
                },
                sectionLine = new Box
                {
                    X = M.SECTION_LINE_X,
                    Width = M.SECTION_LINE_WIDTH,
                    Colour = M.SECTION_LINE_COLOUR,
                    Blending = BlendingParameters.Additive,
                    BypassAutoSizeAxes = Axes.Both,
                },
                indicator = new DefaultIndicator
                {
                    X = M.INDICATOR_X,
                    Width = M.INDICATOR_WIDTH,
                    Alpha = 0,
                    Action = ApplyDefault,
                    BypassAutoSizeAxes = Axes.Both,
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Left = M.ELEMENT_X, Right = CONTENT_RIGHT, Vertical = ROW_PADDING },
                    Spacing = new osuTK.Vector2(0, M.ELEMENT_GAP / 2),
                    Children = new Drawable[]
                    {
                        content = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                        },
                        noteText = new OsuTextFlowContainer(s => s.Font = M.Font(M.LABEL_FONT_SIZE))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Alpha = 0,
                        },
                    },
                },
            };
        }

        /// <summary>
        /// Creates the control itself. Called once during load. Return <c>null</c> for an element that only shows its note.
        /// </summary>
        protected abstract Drawable? CreateContent();

        /// <summary>
        /// Invoked when the row is clicked outside of an input-handling control. Stable forwards such clicks to the element's control.
        /// </summary>
        protected virtual void OnRowClicked()
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var control = CreateContent();

            if (control != null)
                content.Child = control;
            else
                content.Hide();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Entry.Note.BindValueChanged(note => updateNote(note.NewValue), true);
            updateState();
            FinishTransforms(true);
        }

        protected override void Update()
        {
            base.Update();

            float spacing = SettingsSection.ITEM_SPACING_V2;

            // the highlight and indicator cover the element plus half the gap on each side, like stable's (y - 5, height + 10).
            hoverBackground.Y = indicator.Y = -spacing / 2;
            hoverBackground.Height = indicator.Height = DrawHeight + spacing;

            // the guide line covers the gap below as well, so consecutive elements form one continuous line.
            sectionLine.Height = DrawHeight + spacing;

            updateState();
        }

        private void updateState()
        {
            bool disabled = Entry.IsDisabled;
            bool showIndicator = Entry.ShowRevertToDefaultButton && !disabled && !Entry.IsDefault;

            indicator.FadeTo(showIndicator ? 1 : 0, 100);

            if (disabled == lastDisabled)
                return;

            lastDisabled = disabled;
            content.FadeColour(disabled ? StableSettingsColours.DISABLED : Color4.White, 200, Easing.OutQuint);
        }

        private void updateNote(SettingsNote.Data? note)
        {
            noteText.Clear();

            if (note == null)
            {
                noteText.Hide();
                return;
            }

            noteText.AddText(note.Text);
            noteText.Colour = note.Type switch
            {
                SettingsNote.Type.Critical => StableSettingsColours.CRITICAL_TEXT,
                SettingsNote.Type.Warning => M.WARNING_TEXT_COLOUR,
                _ => Color4.White,
            };
            noteText.Show();
        }

        protected override bool OnHover(HoverEvent e)
        {
            hoverBackground.FadeTo(M.HOVER_ALPHA, M.HOVER_DURATION, Easing.OutQuint);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hoverBackground.FadeOut(M.HOVER_DURATION, Easing.OutQuint);
            base.OnHoverLost(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (Entry.IsDisabled)
                return false;

            OnRowClicked();
            return true;
        }

        public virtual LocalisableString TooltipContent => Entry.HintText;

        public ITooltip<LocalisableString> GetCustomTooltip() => new StableTooltip();

        #region ISettingsItem

        public bool HasClassicDefault => Entry.HasClassicDefault;

        public void ApplyClassicDefault() => Entry.ApplyClassicDefault();

        public void ApplyDefault()
        {
            if (!Entry.IsDisabled)
                Entry.SetDefault();
        }

        public event Action SettingChanged
        {
            add => Entry.ValueChanged += value;
            remove => Entry.ValueChanged -= value;
        }

        #endregion

        #region Filtering

        public IEnumerable<LocalisableString> FilterTerms
        {
            get
            {
                foreach (var term in Entry.FilterTerms)
                    yield return term;

                if (HasClassicDefault)
                    yield return SettingsItemV2.CLASSIC_DEFAULT_SEARCH_TERM;
            }
        }

        private bool matchingFilter = true;

        public bool MatchingFilter
        {
            get => matchingFilter;
            set
            {
                bool wasPresent = IsPresent;

                matchingFilter = value;

                if (IsPresent != wasPresent)
                    Invalidate(Invalidation.Presence);
            }
        }

        public override bool IsPresent => base.IsPresent && MatchingFilter;

        public bool FilteringActive { get; set; }

        IBindable<bool> IConditionalFilterable.CanBeShown => Entry.CanBeShown;

        #endregion

        /// <summary>
        /// Stable's <c>defaultLine</c>: an additive yellow gradient beside modified options. Clicking it restores the default.
        /// </summary>
        public partial class DefaultIndicator : OsuClickableContainer, IHasCustomTooltip<LocalisableString>
        {
            public LocalisableString TooltipContent => CommonStrings.RevertToDefault;

            public ITooltip<LocalisableString> GetCustomTooltip() => new StableTooltip();

            public DefaultIndicator()
            {
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Blending = BlendingParameters.Additive,
                    Colour = ColourInfo.GradientHorizontal(M.INDICATOR_COLOUR, M.INDICATOR_COLOUR.Opacity(5 / 255f)),
                };
            }
        }
    }
}
