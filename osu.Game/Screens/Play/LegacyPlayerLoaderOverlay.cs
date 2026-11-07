// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Rendering.Stable;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;
using M = osu.Game.Overlays.Settings.Rendering.Stable.StableOptionsMetrics;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// What osu!stable shows while a beatmap loads (<c>Player.Initialize</c> and <c>VisualSettings</c>): a spinner that appears if loading takes over
    /// a second, and the "Visual Settings" bar along the bottom, which peeks out and extends while hovered. Loading waits while it is extended.
    /// </summary>
    public partial class LegacyPlayerLoaderOverlay : CompositeDrawable
    {
        private const float u = M.U;

        private const float peek_height = 45 * u;
        private const float extended_height = 150 * u;
        private const float line_height = 3 * u;

        /// <summary>
        /// Whether the bar is extended, which holds gameplay back as it does in stable.
        /// </summary>
        public bool Extended { get; private set; }

        private Container bar = null!;

        // stable places "Settings" at (50, 15), which in Aller tucks it under the end of "Visual" with its top on Visual's baseline.
        // Inter sits lower in its line and runs narrower, so the heading is placed from Inter's metrics to keep that interlock.
        private static readonly Vector2 heading_offset = new Vector2(2.7f, -4.1f) * u;
        private const float settings_x_fraction = 0.88f;
        private const float settings_y = 13 * u;

        private OsuSpriteText headingVisual = null!;
        private OsuSpriteText headingSettings = null!;
        private Drawable spinner = null!;

        private double showUntil;
        private Vector2? initialMouse;
        private bool mouseHasMoved;

        public LegacyPlayerLoaderOverlay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, ISkinSource skin)
        {
            // stable picks one of the skin's combo colours for its loading spinner.
            var colours = skin.GetConfig<GlobalSkinColours, IReadOnlyList<Color4>>(GlobalSkinColours.ComboColours)?.Value;
            Color4 spinnerColour = colours?.Count > 0 ? colours[RNG.Next(colours.Count)] : new Color4(255, 96, 171, 255);

            var dim = config.GetBindable<double>(OsuSetting.DimLevel);

            InternalChildren = new Drawable[]
            {
                spinner = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Icon = FontAwesome.Solid.CircleNotch,
                    Size = new Vector2(40 * u),
                    Colour = spinnerColour,
                    Alpha = 0,
                },
                bar = new Container
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.TopLeft,
                    RelativeSizeAxes = Axes.X,
                    Height = line_height + extended_height,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = line_height,
                            Colour = new Color4(0, 78, 155, 255),
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Top = line_height },
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = new Color4(0, 0, 0, 180),
                                },
                                headingVisual = new OsuSpriteText
                                {
                                    Position = heading_offset,
                                    // stable draws this heading in English whatever the language.
                                    Text = @"Visual",
                                    Font = M.Font(M.Text(24), FontWeight.Bold),
                                    Colour = new Color4(173, 216, 230, 255),
                                },
                                headingSettings = new OsuSpriteText
                                {
                                    Text = @"Settings",
                                    Font = M.Font(M.Text(22), FontWeight.Bold),
                                },
                                new OsuSpriteText
                                {
                                    Position = new Vector2(20, 45) * u,
                                    Text = UserInterfaceStrings.LegacyVisualSettingsBackgroundDim,
                                    Font = M.Font(M.Text(10), FontWeight.Bold),
                                },
                                new OsuSpriteText
                                {
                                    Position = new Vector2(260, 45) * u,
                                    Text = UserInterfaceStrings.LegacyVisualSettingsToggles,
                                    Font = M.Font(M.Text(10), FontWeight.Bold),
                                },
                                new StableSliderBar<double>(v => UserInterfaceStrings.LegacyVisualSettingsDimTooltip((int)Math.Round(v * 100)))
                                {
                                    Position = new Vector2(20, 65) * u,
                                    Size = new Vector2(200 * u, M.SLIDER_NUB_SIZE),
                                    Current = dim,
                                },
                                createToggle(UserInterfaceStrings.LegacyVisualSettingsDisableStoryboard, config.GetBindable<bool>(OsuSetting.ShowStoryboard), new Vector2(260, 65) * u),
                                createToggle(UserInterfaceStrings.LegacyVisualSettingsIgnoreBeatmapSkin, config.GetBindable<bool>(OsuSetting.BeatmapSkins), new Vector2(260, 85) * u),
                                createToggle(UserInterfaceStrings.LegacyVisualSettingsIgnoreBeatmapHitsounds, config.GetBindable<bool>(OsuSetting.BeatmapHitsounds), new Vector2(260, 105) * u),
                            },
                        },
                    },
                },
            };
        }

        /// <summary>
        /// Starts a new load: the spinner fades in after a second, and the bar comes up extended for half a second before it may retract.
        /// </summary>
        public void BeginLoading()
        {
            // pStatusDialog.CreateSpinner: spin every 1.5s, popping in from half size.
            spinner.ClearTransforms();
            spinner.Alpha = 0;
            spinner.Delay(1000).FadeIn(400);
            spinner.RotateTo(0).Loop(s => s.RotateTo(360, 1500));
            spinner.ScaleTo(0.5f)
                   .ScaleTo(1.3f, 200, Easing.Out)
                   .Then().ScaleTo(0.9f, 200, Easing.In)
                   .Then().ScaleTo(1, 100, Easing.Out);

            initialMouse = null;
            mouseHasMoved = false;

            showUntil = Time.Current + 500;
            setExtended(true);
        }

        /// <summary>
        /// The beatmap finished loading: stable fades the spinner out over 500ms.
        /// </summary>
        public void FinishLoading()
        {
            spinner.FadeOut(500);
        }

        /// <summary>
        /// Gameplay is starting, so the bar goes away entirely.
        /// </summary>
        public void Leave()
        {
            spinner.FadeOut(500);
            Extended = false;
            showUntil = double.MaxValue;
            bar.MoveToY(0, 400, Easing.In);
        }

        protected override void Update()
        {
            base.Update();

            headingSettings.Position = headingVisual.Position + new Vector2(headingVisual.DrawWidth * settings_x_fraction, settings_y);

            var mouse = GetContainingInputManager()?.CurrentState.Mouse.Position;

            if (mouse == null)
                return;

            Vector2 position = ToLocalSpace(mouse.Value);

            // stable ignores the cursor until it has moved a little from where it was when loading began.
            initialMouse ??= position;
            if (!mouseHasMoved && Vector2.Distance(initialMouse.Value, position) > 30 * u)
                mouseHasMoved = true;

            if (Time.Current < showUntil)
                return;

            // stable extends the bar while the cursor is over it, and keeps its top edge peeking out otherwise.
            bool hovering = mouseHasMoved && position.Y > DrawHeight - (Extended ? extended_height : peek_height);

            if (hovering != Extended)
                setExtended(hovering);
        }

        private void setExtended(bool extended)
        {
            Extended = extended;

            if (extended)
                bar.MoveToY(-(line_height + extended_height), 400, Easing.Out);
            else
            {
                // stable eases up into place, and drops back down with a bounce.
                bool movingUp = bar.Y > -(line_height + peek_height);
                bar.MoveToY(-(line_height + peek_height), movingUp ? 400 : 600, movingUp ? Easing.In : Easing.OutBounce);
            }
        }

        /// <summary>
        /// stable's toggles disable things, where lazer's settings enable them.
        /// </summary>
        private StableCheckboxControl createToggle(LocalisableString text, Bindable<bool> enabled, Vector2 position)
        {
            var disabled = new BindableBool(!enabled.Value);

            bool synchronising = false;

            void syncFromConfiguration()
            {
                synchronising = true;
                try
                {
                    disabled.Disabled = false;
                    disabled.Value = !enabled.Value;
                    disabled.Disabled = enabled.Disabled;
                }
                finally
                {
                    synchronising = false;
                }
            }

            enabled.BindValueChanged(_ => syncFromConfiguration());
            enabled.BindDisabledChanged(_ => syncFromConfiguration());
            disabled.BindValueChanged(d =>
            {
                // Restoring settings is silent in stable. A leased configuration change is not a user click.
                if (!synchronising && !enabled.Disabled)
                    enabled.Value = !d.NewValue;
            });
            syncFromConfiguration();

            return new StableCheckboxControl(text)
            {
                Position = position,
                Current = disabled,
            };
        }
    }
}
