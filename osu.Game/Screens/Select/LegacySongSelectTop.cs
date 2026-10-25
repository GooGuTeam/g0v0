// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Screens.Select.Filter;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// The osu!stable style song select top bar for the legacy UI. Draws the skin's "songselect-top"
    /// texture and, on top of it, the beatmap info block (left) plus the Group/Sort dropdowns, the
    /// grouping tabs and the search box (right). Everything sits on stable's exact coordinates scaled
    /// by 1.6 (the legacy UI lives in a 1366x768 container = stable's 480 space x1.6), so it lines up
    /// with stable and with the skin textures.
    /// </summary>
    public partial class LegacySongSelectTop : CompositeDrawable
    {
        // stable's highlight colours (SongSelection.cs:634/623).
        private static readonly Color4 group_colour = new Color4(146 / 255f, 195 / 255f, 230 / 255f, 1f);
        private static readonly Color4 sort_colour = new Color4(174 / 255f, 210 / 255f, 139 / 255f, 1f);

        private const float dropdown_width = 193;

        // Height of stable's songselect-top strip in the 1366x768 logical space (the stock art is 1366x155).
        private const float panel_height = 155;

        // Logical width of the legacy UI space (the DrawSizePreservingFillContainer's TargetDrawSize.X).
        private const float logical_width = 1366;

        /// <summary>
        /// The song select filter control these legacy controls drive (the search query).
        /// Group/Sort go through config so they stay in sync with the modern (hidden) dropdowns.
        /// </summary>
        public FilterControl FilterControl { get; init; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        // Only the interactive controls take input; the decorative parts (the songselect-top texture,
        // the beatmap info, the Group/Sort labels) let input through so the carousel can be
        // drag-scrolled wherever it peeks out behind the panel.
        private Drawable groupDropdown = null!;
        private Drawable sortDropdown = null!;
        private Drawable groupTabs = null!;
        private Drawable searchBox = null!;

        private ISkinSource skin = null!;
        private SkinManager skins = null!;

        // Kept as fields (NOT locals) so the config.GetBindable copies survive load().
        // osu!framework binds with weak references, so a local copy gets eaten by the GC as soon as
        // load() returns and silently disconnects the tabs/dropdowns from config (the click sets the
        // local Current but never reaches config, so nothing regroups). A strong ref keeps it alive.
        private Bindable<GroupMode> groupBindable = null!;
        private Bindable<SortMode> sortBindable = null!;

        // The layers of the songselect-top texture (the default one plus the skin's own). They are
        // rebuilt on skin change so the panel updates instantly instead of showing the previous skin's
        // top until you leave.
        private readonly List<Drawable> topLayers = new List<Drawable>();

        [BackgroundDependencyLoader]
        private void load(ISkinSource skinSource, SkinManager skinManager)
        {
            skin = skinSource;
            skins = skinManager;

            RelativeSizeAxes = Axes.Both;

            groupBindable = config.GetBindable<GroupMode>(OsuSetting.SongSelectGroupMode);
            sortBindable = config.GetBindable<SortMode>(OsuSetting.SongSelectSortingMode);

            InternalChildren = new Drawable[]
            {
                // The skin's skinnable "mode button" decoration (selection-mode + mode-*-small), drawn
                // BETWEEN the songselect-top (behind, added by rebuildTopLayers) and the beatmap info /
                // Group / Sort / Rankings (in front). stable's order: labels > skinnable top > default.
                new LegacyTopDecoration { Depth = 2 },
                new LegacyBeatmapInfoPanel
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    // A few px of left padding so the status badge and the text don't stick to the edge.
                    Position = new Vector2(5, 0),
                },
                label(@"Group", group_colour, x: -481),
                // Group label + dropdown (stable: x = WidthScaled-140-118-sortLabelWidth). The whole
                // right-hand cluster has to fit above the panel's blue outline (~y78), so the dropdowns
                // sit well up top and the tabs tuck in right below.
                groupDropdown = new LegacyStableDropdown<GroupMode>
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    // In front so the open menu stays above the tabs + the search box below.
                    Depth = -1,
                    Position = new Vector2(-284, 28),
                    AccentColour = group_colour,
                    Width = dropdown_width,
                    Items = Enum.GetValues<GroupMode>(),
                    Current = { BindTarget = groupBindable },
                },
                label(@"Sort", sort_colour, x: -212),
                // Sort label + dropdown (stable: x = WidthScaled-130).
                sortDropdown = new LegacyStableDropdown<SortMode>
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Depth = -1,
                    Position = new Vector2(-15, 28),
                    AccentColour = sort_colour,
                    Width = dropdown_width,
                    Items = Enum.GetValues<SortMode>(),
                    Current = { BindTarget = sortBindable },
                },
                // Grouping tabs (top right). They draw BEHIND the decoration (Depth 2.5 > 2) so the
                // skin's mode-button graphic can stand in for where they are. stable hides the real
                // buttons but keeps them clickable (the decoration is an input-less sprite, so clicks
                // pass through).
                groupTabs = new LegacyGroupTabs
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Depth = 2.5f,
                    Margin = new MarginPadding { Top = 54, Right = 28 },
                    Current = { BindTarget = groupBindable },
                },
                // Search box (top right, just below the panel's outline).
                searchBox = new LegacySearchBox
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Margin = new MarginPadding { Top = 82, Right = 14 },
                    Current = { BindTarget = FilterControl.SearchQuery },
                },
            };

            rebuildTopLayers();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Rebuild the songselect-top layers on skin change so a live change updates the panel
            // instantly (the decoration refreshes itself via its own SourceChanged handler).
            skin.SourceChanged += rebuildTopLayers;
        }

        /// <summary>
        /// Rebuilds the two stacked songselect-top layers, like stable:
        ///   (back) the always-present bundled classic top; its opaque left panel backs the beatmap
        ///          info so it stays readable even when the skin's own top is transparent there.
        ///   (middle) the skin's OWN songselect-top, only if it ships one (many skins don't).
        /// With their higher Depth they sit behind the decoration (Depth 2) and the chrome.
        /// </summary>
        private void rebuildTopLayers()
        {
            if (topLayers.Count > 0)
            {
                foreach (var layer in topLayers)
                    RemoveInternal(layer, true);

                topLayers.Clear();
            }

            var bundledTop = skins.DefaultClassicSkin.GetTexture(@"songselect-top");
            var skinTop = skins.CurrentSkin.Value.GetTexture(@"songselect-top");

            topLayers.Add(topLayer(bundledTop, depth: 4));

            if (skinTop != null && skins.CurrentSkin.Value != skins.DefaultClassicSkin)
                topLayers.Add(topLayer(skinTop, depth: 3));

            AddRangeInternal(topLayers);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (skin.IsNotNull())
                skin.SourceChanged -= rebuildTopLayers;

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// A single songselect-top texture layer: full-width at its natural aspect (pinned to the top),
        /// clipped to stable's 155px strip so a tall/opaque skin texture doesn't cover the controls band.
        /// Higher <paramref name="depth"/> = further back, so the default backing sits behind the skin's own.
        /// </summary>
        private Drawable topLayer(Texture? tex, float depth)
        {
            // No texture (not even the bundled classic one): fall back to a dark gradient so the beatmap
            // info and the controls stay legible over any background.
            if (tex == null)
            {
                return new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Width = 1,
                    Height = panel_height,
                    Masking = true,
                    Depth = depth,
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientVertical(new Color4(0f, 0f, 0f, 0.78f), new Color4(0f, 0f, 0f, 0f)),
                    },
                };
            }

            float h = tex.DisplayWidth > 0
                ? logical_width * (tex.DisplayHeight / tex.DisplayWidth)
                : panel_height;

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Width = 1,
                Height = panel_height,
                Masking = true,
                Depth = depth,
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Child = new Sprite
                {
                    Texture = tex,
                    RelativeSizeAxes = Axes.X,
                    Width = 1,
                    Height = h,
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                },
            };
        }

        // This overlay is full-screen, but only the interactive controls should take input. Everything
        // else (including the decorative songselect-top texture / the beatmap info) lets input through so
        // the carousel can be drag-scrolled wherever it peeks out behind the panel.
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
            => groupDropdown.ReceivePositionalInputAt(screenSpacePos)
               || sortDropdown.ReceivePositionalInputAt(screenSpacePos)
               || groupTabs.ReceivePositionalInputAt(screenSpacePos)
               || searchBox.ReceivePositionalInputAt(screenSpacePos);

        /// <summary>
        /// A stable style coloured label ("Group" / "Sort"), right-edge anchored <paramref name="x"/> px
        /// from the right edge of the screen, right above its dropdown (stable y=40 x1.6).
        /// </summary>
        private static Drawable label(LocalisableString text, Color4 colour, float x) => new StrokedLegacyText
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.BottomRight,
            // stable places the large label low enough that the descenders (the "p" in Group) spill over
            // the tab row, exactly as it looks in stable. Behind the decoration (2.6 > 2), like the tabs,
            // so the skin's mode-button graphic draws on top.
            Position = new Vector2(x, 56),
            Depth = 2.6f,
            Text = text,
            Font = LegacyFonts.Get(34, FontWeight.Light),
            // Text tinted with stable's thin dark outline (replaces the soft drop shadow).
            Colour = colour,
        };
    }
}
