// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;
using M = osu.Game.Overlays.Settings.Rendering.Stable.StableOptionsMetrics;

namespace osu.Game.Overlays.Settings.Rendering.Stable
{
    /// <summary>
    /// osu!stable's options tab (<c>OptionTab</c>): an icon only, grey until hovered or active;
    /// the active tab gets a dark background and the main colour "light" on its right edge. The section name is shown as a tooltip.
    /// </summary>
    public partial class StableSidebarButton : OsuClickableContainer, ISettingsSidebarButton, IHasCustomTooltip<LocalisableString>
    {
        private const double active_fade_duration = 300;
        private const double hover_fade_duration = 100;

        public SettingsSection Section { get; }

        private readonly Box background;
        private readonly Box light;
        private readonly ConstrainedIconContainer icon;

        private bool selected;

        public bool Selected
        {
            get => selected;
            set
            {
                if (selected == value)
                    return;

                selected = value;

                if (IsLoaded)
                    updateState();
            }
        }

        // always consider as part of flow, even when not visible (for the sake of the initial animation).
        public override bool IsPresent => true;

        public LocalisableString TooltipContent => Section.Header;

        public ITooltip<LocalisableString> GetCustomTooltip() => new StableTooltip();

        public StableSidebarButton(SettingsSection section)
            : base(HoverSampleSet.ButtonSidebar)
        {
            Section = section;

            RelativeSizeAxes = Axes.X;
            Height = M.TAB_HEIGHT;

            float iconScale = section.UseSmallerSidebarButton ? 0.8f : 1;

            Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = M.TAB_ACTIVE_BACKGROUND,
                    Alpha = 0.01f,
                },
                light = new Box
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    RelativeSizeAxes = Axes.Y,
                    Width = M.TAB_LIGHT_WIDTH,
                    Colour = StableSettingsColours.MAIN,
                    Alpha = 0,
                },
                icon = new ConstrainedIconContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(M.TAB_ICON_SIZE * iconScale),
                    Icon = section.CreateIcon(),
                    Colour = M.TAB_ICON_COLOUR,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            updateState();
            FinishTransforms(true);
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateState();
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            updateState();
            base.OnHoverLost(e);
        }

        private void updateState()
        {
            icon.FadeColour(Selected || IsHovered ? Color4.White : M.TAB_ICON_COLOUR, hover_fade_duration);

            if (Selected)
            {
                background.FadeIn(hover_fade_duration);
                light.FadeIn(active_fade_duration, Easing.OutQuint);
            }
            else
            {
                background.FadeTo(0.01f, active_fade_duration);
                light.FadeOut(active_fade_duration, Easing.OutQuint);
            }
        }
    }
}
