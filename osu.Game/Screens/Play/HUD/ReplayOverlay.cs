// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.Input.Bindings;
using osuTK.Input;

namespace osu.Game.Screens.Play.HUD
{
    public partial class ReplayOverlay : CompositeDrawable, IKeyBindingHandler<GlobalAction>
    {
        public ReplaySettingsOverlay Settings { get; private set; } = null!;

        private const int fade_duration = 200;

        [Resolved]
        private HUDOverlay? hudOverlay { get; set; }

        [Resolved]
        private Player? player { get; set; }

        private readonly IBindable<bool> showHud = new BindableBool(true);
        private Bindable<bool> configSettingsOverlay = null!;
        private Container messageContainer = null!;
        private Container content = null!;

        private bool isVisible => configSettingsOverlay.Value && showHud.Value;

        private bool canToggleSettings => showHud.Value && player?.Configuration.AllowUserInteraction != false;

        public override bool PropagatePositionalInputSubTree => isVisible && base.PropagatePositionalInputSubTree;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            RelativeSizeAxes = Axes.Both;

            configSettingsOverlay = config.GetBindable<bool>(OsuSetting.ReplaySettingsOverlay);

            InternalChild = content = new Container
            {
                Alpha = 0,
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    messageContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Depth = float.MaxValue,
                    },
                    Settings = new ReplaySettingsOverlay(),
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (hudOverlay != null)
                showHud.BindTo(hudOverlay.ShowHud);

            showHud.BindValueChanged(_ => updateVisibility());
            configSettingsOverlay.BindValueChanged(_ => updateVisibility(), true);
        }

        private void updateVisibility()
        {
            if (isVisible)
                content.FadeIn(fade_duration, Easing.OutQuint);
            else
            {
                Settings.Expanded.Value = false;
                content.FadeOut(fade_duration, Easing.OutQuint);
            }
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key == Key.H && !e.Repeat && !e.ControlPressed && !e.AltPressed && !e.SuperPressed)
            {
                if (!canToggleSettings)
                    return false;

                configSettingsOverlay.Value = !configSettingsOverlay.Value;
                return true;
            }

            return base.OnKeyDown(e);
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat || !canToggleSettings || e.AltPressed || e.SuperPressed)
                return false;

            switch (e.Action)
            {
                case GlobalAction.ToggleReplaySettings:
                    configSettingsOverlay.Value = !configSettingsOverlay.Value;
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        public override void Show() => this.FadeIn(fade_duration, Easing.OutQuint);
        public override void Hide() => this.FadeOut(fade_duration, Easing.OutQuint);

        public void SetMessage(ScrollingMessage scrollingMessage) => messageContainer.Child = scrollingMessage;
    }
}
