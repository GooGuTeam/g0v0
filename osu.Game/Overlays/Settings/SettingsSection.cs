// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Overlays.Settings.Entries;
using osu.Game.Overlays.Settings.Rendering;
using osuTK;

namespace osu.Game.Overlays.Settings
{
    public abstract partial class SettingsSection : Container, IFilterable
    {
        protected FillFlowContainer FlowContent;
        protected override Container<Drawable> Content => FlowContent;

        private IBindable<SettingsSection> selectedSection;

        private ISettingsSectionFrame frame;

        public abstract Drawable CreateIcon();
        public abstract LocalisableString Header { get; }

        public bool UseSmallerSidebarButton { get; init; }

        public virtual IEnumerable<LocalisableString> FilterTerms => new[] { Header };

        public const int ITEM_SPACING = 14;
        public const int ITEM_SPACING_V2 = 4;


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

        [Resolved(canBeNull: true)]
        private SettingsPanel settingsPanel { get; set; }

        [Resolved(canBeNull: true)]
        private ISettingsRenderer renderer { get; set; }

        /// <summary>
        /// The renderer used for entries added directly to this section, as chosen by the containing <see cref="SettingsPanel"/>.
        /// </summary>
        protected ISettingsRenderer Renderer => renderer ?? LazerSettingsRenderer.DEFAULT;

        private readonly List<SettingsEntry> entries = new List<SettingsEntry>();

        /// <summary>
        /// Creates the drawable for <paramref name="entry"/> using <see cref="Renderer"/>.
        /// The entry's bindings are released when this section is disposed.
        /// </summary>
        protected Drawable CreateItem(SettingsEntry entry)
        {
            if (LoadState < LoadState.Loading)
                throw new InvalidOperationException($"{nameof(CreateItem)} must be called during or after load, so that the panel's renderer is used.");

            entries.Add(entry);
            return entry.CreateDrawable(Renderer);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            foreach (var entry in entries)
                entry.Unbind();

            entries.Clear();
        }

        protected SettingsSection()
        {
            AutoSizeAxes = Axes.Y;
            RelativeSizeAxes = Axes.X;

            FlowContent = new FillFlowContainer
            {
                Spacing = new Vector2(0, ITEM_SPACING_V2),
                Direction = FillDirection.Vertical,
                AutoSizeAxes = Axes.Y,
                RelativeSizeAxes = Axes.X,
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddInternal((Drawable)(frame = Renderer.CreateSectionFrame(Header, FlowContent)));

            selectedSection = settingsPanel?.CurrentSection.GetBoundCopy() ?? new Bindable<SettingsSection>(this);
            selectedSection.BindValueChanged(_ => updateContentFade(), true);
        }

        private bool isCurrentSection => selectedSection.Value == this;

        protected override bool OnHover(HoverEvent e)
        {
            updateContentFade();
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            updateContentFade();
            base.OnHoverLost(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (!isCurrentSection && frame.RequiresSelectionForInput)
            {
                Debug.Assert(settingsPanel != null);
                settingsPanel.SectionsContainer.ScrollTo(this);
            }

            return base.OnClick(e);
        }

        protected override bool ShouldBeConsideredForInput(Drawable child) =>
            // only the current section should accept input (when the frame asks for it).
            // this provides the behaviour of the first click scrolling the target section to the centre of the screen.
            isCurrentSection || !frame.RequiresSelectionForInput;

        private void updateContentFade() => frame.UpdateState(isCurrentSection, IsHovered);
    }
}
