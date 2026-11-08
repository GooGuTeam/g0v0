// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osuTK;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Rendering;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    [Cached]
    public abstract partial class SettingsPanel : OsuFocusedOverlayContainer
    {
        public const float CONTENT_MARGINS = 20;

        // extra right padding to give room to the revert-to-default button in settings controls.
        public static readonly MarginPadding CONTENT_PADDING = new MarginPadding { Left = 12, Right = 22 };

        public const float TRANSITION_LENGTH = 600;

        private const float sidebar_width = SettingsSidebar.EXPANDED_WIDTH;

        /// <summary>
        /// The width of the settings panel content, excluding the sidebar.
        /// </summary>
        public const float PANEL_WIDTH = 400;

        /// <summary>
        /// The full width of the settings panel, including the sidebar.
        /// </summary>
        public const float WIDTH = sidebar_width + PANEL_WIDTH;

        protected Container<Drawable> ContentContainer;

        protected override Container<Drawable> Content => ContentContainer;

        protected SettingsSidebar Sidebar;
        private ISettingsSidebarButton selectedSidebarButton;

        public SettingsSectionsContainer SectionsContainer { get; private set; }

        protected SeekLimitedSearchTextBox SearchTextBox { get; private set; }

        protected override string PopInSampleName => "UI/settings-pop-in";
        protected override double PopInOutSampleBalance => -OsuGameBase.SFX_STEREO_STRENGTH;

        private readonly bool showBackButton;

        private LoadingLayer loading;

        private readonly List<SettingsSection> loadableSections = new List<SettingsSection>();

        private Task sectionsLoadingTask;

        public IBindable<SettingsSection> CurrentSection = new Bindable<SettingsSection>();

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        protected SettingsPanel(bool showBackButton)
        {
            this.showBackButton = showBackButton;
            RelativeSizeAxes = Axes.Y;
            AutoSizeAxes = Axes.X;
        }

        /// <summary>
        /// The renderer used for the settings entries of this panel. Cached for all sections and subsections.
        /// </summary>
        public ISettingsRenderer Renderer { get; private set; }

        /// <summary>
        /// Chooses the control style of this panel. The choice is made once, when the panel is loaded.
        /// </summary>
        protected virtual ISettingsRenderer CreateRenderer(OsuConfigManager config)
            => config?.Get<bool>(OsuSetting.ToriiLegacyOptions) == true ? StableSettingsRenderer.DEFAULT : LazerSettingsRenderer.DEFAULT;

        private readonly SettingsFlyoutLayer flyoutLayer = new SettingsFlyoutLayer();

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs(Renderer = CreateRenderer(parent.Get<OsuConfigManager>()));
            // sections load with this panel's dependencies (before they join the sections container), so the flyout layer is cached here.
            dependencies.Cache(flyoutLayer);
            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = ContentContainer = new NonMaskedContent
            {
                X = -WIDTH + ExpandedPosition,
                Width = PANEL_WIDTH,
                RelativeSizeAxes = Axes.Y,
                Children = new Drawable[]
                {
                    Renderer.CreatePanelBackground(),
                    loading = new LoadingLayer
                    {
                        State = { Value = Visibility.Visible }
                    }
                }
            };

            Add(new PopoverContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = SectionsContainer = new SettingsSectionsContainer(flyoutLayer)
                {
                    Masking = true,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Colour = Color4.Black.Opacity(0),
                        Type = EdgeEffectType.Shadow,
                        Hollow = true,
                        Radius = 10
                    },
                    MaskingSmoothness = 0,
                    RelativeSizeAxes = Axes.Both,
                    ExpandableHeader = CreateHeader(),
                    SelectedSection = { BindTarget = CurrentSection },
                    FixedHeader = Renderer.CreateSearchBar((SettingsSearchTextBox)(SearchTextBox = new SettingsSearchTextBox())),
                    Footer = CreateFooter().With(f => f.Alpha = 0)
                }
            });

            AddInternal(Sidebar = Renderer.CreateSidebar(showBackButton).With(s =>
            {
                s.BackButtonAction = Hide;
                s.Width = sidebar_width;
            }));

            // a sub-panel (one with a sidebar back button) never gets a root back button.
            if (!showBackButton && Renderer.CreateRootBackButton(GoBack) is Drawable rootBackButton)
                AddInternal(rootBackButton);
        }

        /// <summary>
        /// Steps back one level from the root back button (see <see cref="ISettingsRenderer.CreateRootBackButton"/>).
        /// </summary>
        protected virtual void GoBack() => Hide();

        protected void AddSection(SettingsSection section)
        {
            if (IsLoaded)
                // just to keep things simple. can be accommodated for if we ever need it.
                throw new InvalidOperationException("All sections must be added before the panel is loaded.");

            loadableSections.Add(section);
        }

        protected virtual Drawable CreateHeader() => new Container();

        protected virtual Drawable CreateFooter() => new Container();

        protected override void PopIn()
        {
            ContentContainer.MoveToX(ExpandedPosition, TRANSITION_LENGTH, Easing.OutQuint);

            SectionsContainer.FadeEdgeEffectTo(WaveContainer.SHADOW_OPACITY, WaveContainer.APPEAR_DURATION, Easing.Out);

            // delay load enough to ensure it doesn't overlap with the initial animation.
            // this is done as there is still a brief stutter during load completion which is more visible if the transition is in progress.
            // the eventual goal would be to remove the need for this by splitting up load into smaller work pieces, or fixing the remaining
            // load complete overheads.
            Scheduler.AddDelayed(loadSections, TRANSITION_LENGTH / 3);

            Sidebar?.MoveToX(0, TRANSITION_LENGTH, Easing.OutQuint);
            this.FadeTo(1, TRANSITION_LENGTH / 2, Easing.OutQuint);

            SearchTextBox.TakeFocus();
            SearchTextBox.HoldFocus = true;
        }

        protected virtual float ExpandedPosition => 0;

        protected override void PopOut()
        {
            base.PopOut();

            SectionsContainer.FadeEdgeEffectTo(0, WaveContainer.DISAPPEAR_DURATION, Easing.In);
            ContentContainer.MoveToX(-WIDTH + ExpandedPosition, TRANSITION_LENGTH, Easing.OutQuint);

            Sidebar?.MoveToX(-sidebar_width, TRANSITION_LENGTH, Easing.OutQuint);
            this.FadeTo(0, TRANSITION_LENGTH / 2, Easing.OutQuint);

            SearchTextBox.HoldFocus = false;
            if (SearchTextBox.HasFocus)
                GetContainingFocusManager()!.ChangeFocus(null);
        }

        public override bool AcceptsFocus => true;

        protected override void OnFocus(FocusEvent e)
        {
            SearchTextBox.TakeFocus();
            base.OnFocus(e);
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            ContentContainer.Margin = new MarginPadding { Left = Sidebar?.ContentOffset ?? 0 };
        }

        private const double fade_in_duration = 500;

        private void loadSections()
        {
            if (sectionsLoadingTask != null)
                return;

            sectionsLoadingTask = LoadComponentsAsync(loadableSections, sections =>
            {
                SectionsContainer.AddRange(sections);
                SectionsContainer.Footer.FadeInFromZero(fade_in_duration, Easing.OutQuint);
                SectionsContainer.SearchContainer.FadeInFromZero(fade_in_duration, Easing.OutQuint);

                loading.Hide();

                SearchTextBox.Current.BindValueChanged(term => SectionsContainer.SearchTerm = term.NewValue, true);

                loadSidebarButtons();
            });
        }

        private void loadSidebarButtons()
        {
            if (Sidebar == null)
                return;

            LoadComponentsAsync(createSidebarButtons(), buttons =>
            {
                float delay = 0;

                foreach (var button in buttons)
                {
                    Sidebar.Add(button);

                    button.FadeOut()
                          .Delay(delay)
                          .FadeInFromZero(fade_in_duration, Easing.OutQuint);

                    delay += 40;
                }

                SectionsContainer.SelectedSection.BindValueChanged(section =>
                {
                    selectedSidebarButton?.Selected = false;

                    selectedSidebarButton = Sidebar.Children.OfType<ISettingsSidebarButton>().FirstOrDefault(b => b.Section == section.NewValue);

                    selectedSidebarButton?.Selected = true;
                }, true);
            });
        }

        private IEnumerable<Drawable> createSidebarButtons()
        {
            foreach (var section in SectionsContainer)
            {
                var button = Renderer.CreateSidebarButton(section, () =>
                {
                    if (!SectionsContainer.IsLoaded)
                        return;

                    SectionsContainer.ScrollTo(section);
                });

                if (button != null)
                    yield return (Drawable)button;
            }
        }

        private partial class NonMaskedContent : Container<Drawable>
        {
            // masking breaks the pan-out transform with nested sub-settings panels.
            protected override bool ComputeIsMaskedAway(RectangleF maskingBounds) => false;
        }

        public partial class SettingsSectionsContainer : SectionsContainer<SettingsSection>
        {
            public SearchContainer<SettingsSection> SearchContainer;

            /// <summary>
            /// Floating content of the settings (see <see cref="SettingsFlyoutLayer"/>), drawn above the sections and below the pinned headers,
            /// which are added after construction.
            /// </summary>
            public readonly SettingsFlyoutLayer FlyoutLayer;

            public SettingsSectionsContainer(SettingsFlyoutLayer flyoutLayer)
            {
                AddInternal(FlyoutLayer = flyoutLayer);
            }

            public string SearchTerm
            {
                get => SearchContainer.SearchTerm;
                set => SearchContainer.SearchTerm = value;
            }

            protected override FlowContainer<SettingsSection> CreateScrollContentContainer()
                => SearchContainer = new SearchContainer<SettingsSection>
                {
                    AutoSizeAxes = Axes.Y,
                    RelativeSizeAxes = Axes.X,
                    Direction = FillDirection.Vertical,
                };

            [BackgroundDependencyLoader]
            private void load(ISettingsRenderer renderer)
            {
                HeaderBackground = renderer.CreatePinnedHeaderBackground();

                SearchContainer.FilterCompleted += InvalidateScrollPosition;
            }

            protected override void UpdateAfterChildren()
            {
                base.UpdateAfterChildren();

                // no null check because the usage of this class is strict
                HeaderBackground!.Alpha = -ExpandableHeader!.Y / ExpandableHeader.LayoutSize.Y;
            }
        }
    }
}
