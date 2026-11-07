// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Input;
using osu.Framework.Input.StateChanges;
using osu.Game.Configuration;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Skinning;

namespace osu.Game.Graphics.Cursor
{
    /// <summary>
    /// A container which provides the main <see cref="MenuCursorContainer"/>.
    /// Also handles cases where a more localised cursor is provided by another component (via <see cref="IProvideCursor"/>).
    /// </summary>
    public partial class GlobalCursorDisplay : Container, IProvideCursor
    {
        /// <summary>
        /// Control whether any cursor should be displayed.
        /// </summary>
        internal bool ShowCursor = true;

        CursorContainer IProvideCursor.Cursor => UseGameplayCursor && gameplayCursor != null ? gameplayCursor : MenuCursor;

        internal bool UseGameplayCursor;
        private CursorContainer? gameplayCursor;
        private CursorContainer? displayedCursor;

        public MenuCursorContainer MenuCursor { get; }

        public bool ProvidingUserCursor => true;

        protected override Container<Drawable> Content { get; } = new Container { RelativeSizeAxes = Axes.Both };

        private Bindable<bool> showDuringTouch = null!;

        private InputManager inputManager = null!;

        private IProvideCursor? currentOverrideProvider;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        public GlobalCursorDisplay()
        {
            AddRangeInternal(new Drawable[]
            {
                Content = new Container { RelativeSizeAxes = Axes.Both },
                MenuCursor = new MenuCursorContainer { State = { Value = Visibility.Hidden } }
            });
        }

        [BackgroundDependencyLoader]
        private void load(RulesetStore rulesets)
        {
            var ruleset = rulesets.GetRuleset(0)?.CreateInstance();
            gameplayCursor = ruleset?.CreateMenuCursor();
            if (gameplayCursor == null || ruleset == null)
                return;

            gameplayCursor.Hide();

            // The gameplay cursor only dims itself when hidden (so a hidden cursor stays faintly visible during gameplay).
            // Outside gameplay a hidden menu cursor must disappear entirely, trail included, or it leaves a ghost behind the active cursor.
            Container gameplayCursorVisibility;
            AddInternal(gameplayCursorVisibility = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
                // keep tracking the mouse while invisible, so the cursor and its trail don't jump from a stale position when shown again.
                AlwaysPresent = true,
                Child = new RulesetSkinProvidingContainer(ruleset, new Beatmap(), null)
                {
                    Child = gameplayCursor,
                },
            });

            gameplayCursor.State.BindValueChanged(state => gameplayCursorVisibility.FadeTo(state.NewValue == Visibility.Visible ? 1 : 0, state.NewValue == Visibility.Visible ? 0 : 250, Easing.OutQuint));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            inputManager = GetContainingInputManager()!;
            showDuringTouch = config.GetBindable<bool>(OsuSetting.GameplayCursorDuringTouch);
        }

        protected override void Update()
        {
            base.Update();

            var lastMouseSource = inputManager.CurrentState.Mouse.LastSource;
            bool hasValidInput = lastMouseSource != null && (showDuringTouch.Value || lastMouseSource is not ISourcedFromTouch);

            if (!hasValidInput || !ShowCursor)
            {
                displayedCursor?.Hide();
                displayedCursor = null;
                currentOverrideProvider = null;
                return;
            }

            IProvideCursor newOverrideProvider = this;

            foreach (var d in inputManager.HoveredDrawables)
            {
                if (d is IProvideCursor p && p.ProvidingUserCursor)
                {
                    newOverrideProvider = p;
                    break;
                }
            }

            if (currentOverrideProvider == newOverrideProvider && displayedCursor == newOverrideProvider.Cursor)
                return;

            displayedCursor?.Hide();
            displayedCursor = newOverrideProvider.Cursor;
            displayedCursor?.Show();

            currentOverrideProvider = newOverrideProvider;
        }
    }
}
