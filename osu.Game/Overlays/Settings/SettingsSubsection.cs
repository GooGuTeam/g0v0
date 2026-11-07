// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osuTK;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Game.Overlays.Settings.Entries;
using osu.Game.Overlays.Settings.Rendering;

namespace osu.Game.Overlays.Settings
{
    public abstract partial class SettingsSubsection : FillFlowContainer, IFilterable
    {
        public const float VERTICAL_PADDING = (header_height - header_font_size) * 0.5f;

        protected override Container<Drawable> Content => FlowContent;

        protected readonly FillFlowContainer FlowContent;

        protected abstract LocalisableString Header { get; }

        public virtual IEnumerable<LocalisableString> FilterTerms => new[] { Header };

        public bool MatchingFilter
        {
            set => this.FadeTo(value ? 1 : 0);
        }

        public bool FilteringActive { get; set; }

        [Resolved(canBeNull: true)]
        private ISettingsRenderer? renderer { get; set; }

        /// <summary>
        /// The renderer used for this subsection's entries, as chosen by the containing <see cref="SettingsPanel"/>.
        /// Available from this subsection's <c>load</c> method onwards.
        /// </summary>
        protected ISettingsRenderer Renderer => renderer ?? LazerSettingsRenderer.DEFAULT;

        private readonly List<SettingsEntry> entries = new List<SettingsEntry>();

        /// <summary>
        /// Creates the drawable for <paramref name="entry"/> using <see cref="Renderer"/>.
        /// The entry's bindings are released when this subsection is disposed.
        /// </summary>
        protected Drawable CreateItem(SettingsEntry entry)
        {
            if (LoadState < LoadState.Loading)
                throw new InvalidOperationException($"{nameof(CreateItem)} must be called during or after load, so that the panel's renderer is used.");

            entries.Add(entry);
            return entry.CreateDrawable(Renderer);
        }

        protected SettingsSubsection()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;

            FlowContent = new FillFlowContainer
            {
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, SettingsSection.ITEM_SPACING_V2),
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
            };
        }

        private const int header_height = LazerSettingsRenderer.SUBSECTION_HEADER_HEIGHT;
        private const int header_font_size = LazerSettingsRenderer.SUBSECTION_HEADER_FONT_SIZE;

        [BackgroundDependencyLoader]
        private void load()
        {
            AddRangeInternal(new[]
            {
                CreateHeader(),
                // Separate container to ensure margin remains even if FlowContent is hidden.
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Margin = new MarginPadding { Top = SettingsSection.ITEM_SPACING_V2 },
                    Child = FlowContent
                },
            });
        }

        protected virtual Drawable CreateHeader() => Renderer.CreateSubsectionHeader(Header);

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            foreach (var entry in entries)
                entry.Unbind();

            entries.Clear();
        }
    }
}
