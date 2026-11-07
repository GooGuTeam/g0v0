// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Play
{
    public abstract partial class GameplayMenuOverlay : OverlayContainer, IKeyBindingHandler<GlobalAction>
    {
        protected const int TRANSITION_DURATION = 200;

        private const int button_height = 80;
        private const float background_alpha = 0.75f;

        protected override bool BlockScrollInput => false;

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true;

        public Action? OnResume { get; init; }
        public Action? OnRetry { get; init; }
        public Action? OnQuit { get; init; }

        /// <summary>
        /// Action that is invoked when <see cref="GlobalAction.Back"/> is triggered.
        /// </summary>
        protected virtual Action BackAction => () =>
        {
            // We prefer triggering the button click as it will animate...
            // but sometimes buttons aren't present (see FailOverlay's constructor as an example).
            if (Buttons.Any())
                Buttons.Last().TriggerClick();
            else
                OnQuit?.Invoke();
        };

        /// <summary>
        /// Action that is invoked when <see cref="GlobalAction.Select"/> is triggered.
        /// </summary>
        protected virtual Action SelectAction => () => InternalButtons.Selected?.TriggerClick();

        public abstract LocalisableString Header { get; }

        public Container FooterContent { get; private set; } = null!;

        protected SelectionCycleFillFlowContainer<DialogButton> InternalButtons = null!;
        public IReadOnlyList<DialogButton> Buttons => InternalButtons;

        private TextFlowContainer playInfoText = null!;

        /// <summary>
        /// osu!stable's version of this screen, shown instead of the standard one when the legacy pause screen is enabled.
        /// </summary>
        private LegacyGameplayMenu? legacyMenu;
        protected LegacyGameplayMenu? LegacyMenu => legacyEnabled.Value ? legacyMenu : null;
        private readonly BindableBool legacyEnabled = new BindableBool();
        private Container modernContent = null!;

        /// <summary>
        /// Whether this screen is shown after failing, which stable's version uses to drop its continue button.
        /// </summary>
        protected virtual bool IsFailScreen => false;

        /// <summary>
        /// Saves the failed score so that it can be played back from the legacy fail screen.
        /// </summary>
        protected virtual Func<Task<ScoreInfo>>? LegacySaveReplay => null;

        [Resolved]
        private GlobalActionContainer globalAction { get; set; } = null!;

        protected GameplayMenuOverlay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, GameHost? host, OsuConfigManager? config)
        {
            config?.BindWith(OsuSetting.ToriiLegacyGameplayScreens, legacyEnabled);

            Children = new Drawable[]
            {
                pauseLoop = new SkinnableSound(new SampleInfo("Gameplay/pause-loop"))
                {
                    Looping = true,
                    Volume = { Value = 0 }
                },
                // the legacy screen reuses the standard one's buttons and actions without showing them.
                modernContent = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = legacyEnabled.Value ? 0 : 1,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black,
                            Alpha = background_alpha,
                        },
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            RowDimensions = new[]
                            {
                                new Dimension(),
                                new Dimension(GridSizeMode.AutoSize),
                                new Dimension(),
                                new Dimension(GridSizeMode.AutoSize),
                            },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Text = Header,
                                        Font = OsuFont.GetFont(typeface: Typeface.MapleMono, size: 48, weight: FontWeight.SemiBold),
                                        Spacing = new Vector2(5),
                                        Origin = Anchor.Centre,
                                        Anchor = Anchor.Centre,
                                        Colour = colours.Yellow,
                                    },
                                },
                                new Drawable[]
                                {
                                    InternalButtons = new SelectionCycleFillFlowContainer<DialogButton>
                                    {
                                        Origin = Anchor.Centre,
                                        Anchor = Anchor.Centre,
                                        RelativeSizeAxes = Axes.X,
                                        Padding = new MarginPadding { Horizontal = 50 },
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(2),
                                        Masking = true,
                                    },
                                },
                                new Drawable[]
                                {
                                    playInfoText = new OsuTextFlowContainer(cp => cp.Font = OsuFont.GetFont(size: 18))
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        TextAnchor = Anchor.Centre,
                                        AutoSizeAxes = Axes.Both,
                                    }
                                },
                                new Drawable[]
                                {
                                    FooterContent = new Container
                                    {
                                        AutoSizeAxes = Axes.Y,
                                        RelativeSizeAxes = Axes.X,
                                    },
                                }
                            }
                        }
                    }
                },
            };

            Add(legacyMenu = new LegacyGameplayMenu(IsFailScreen,
                OnResume == null ? null : () => actAndHide(OnResume),
                OnRetry == null ? null : () => actAndHide(OnRetry),
                OnQuit == null ? null : () => actAndHide(OnQuit),
                LegacySaveReplay)
            {
                Alpha = legacyEnabled.Value ? 1 : 0,
            });

            if (OnResume != null)
                AddButton(GameplayMenuOverlayStrings.Continue, colours.Green, () => OnResume.Invoke());

            if (OnRetry != null)
                AddButton(GameplayMenuOverlayStrings.Retry, colours.YellowDark, () => OnRetry.Invoke());

            if (OnQuit != null)
                AddButton(GameplayMenuOverlayStrings.Quit, new Color4(170, 27, 39, 255), () => OnQuit.Invoke());

            State.ValueChanged += _ => InternalButtons.Deselect();

            updateInfoText();

            if (host != null)
                windowActive.BindTo(host.IsActive);
        }

        private int retries;

        public int Retries
        {
            set
            {
                if (value == retries)
                    return;

                retries = value;

                if (IsLoaded)
                    updateInfoText();
            }
        }

        protected override void PopIn()
        {
            if (LegacyMenu != null)
            {
                this.FadeIn();
                LegacyMenu.Appear();
            }
            else
                this.FadeIn(TRANSITION_DURATION, Easing.In);

            updateInfoText();

            startPauseLoop();
        }

        protected override void PopOut()
        {
            if (LegacyMenu != null)
            {
                // stable fades its dim and overlay out over 800ms, while gameplay resumes beneath them.
                LegacyMenu.Disappear();
                this.Delay(800).FadeOut();
            }
            else
                this.FadeOut(TRANSITION_DURATION, Easing.In);

            stopPauseLoop();
        }

        private void actAndHide(Action action)
        {
            action.Invoke();
            Hide();
        }

        protected void AddButton(LocalisableString text, Color4 colour, Action? action)
        {
            var button = new Button
            {
                Text = text,
                ButtonColour = colour,
                Origin = Anchor.TopCentre,
                Anchor = Anchor.TopCentre,
                Height = button_height,
                Action = delegate
                {
                    action?.Invoke();
                    Hide();
                }
            };

            InternalButtons.Add(button);
        }

        public virtual bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (LegacyMenu != null)
            {
                switch (e.Action)
                {
                    case GlobalAction.SelectPrevious:
                        LegacyMenu.PressKey(-1);
                        return true;

                    case GlobalAction.SelectNext:
                        LegacyMenu.PressKey(1);
                        return true;

                    case GlobalAction.Select:
                        LegacyMenu.PressKey(0);
                        return true;
                }
            }

            switch (e.Action)
            {
                case GlobalAction.SelectPrevious:
                    InternalButtons.SelectPrevious();
                    return true;

                case GlobalAction.SelectNext:
                    InternalButtons.SelectNext();
                    return true;

                case GlobalAction.Back:
                    BackAction.Invoke();
                    return true;

                case GlobalAction.Select:
                    SelectAction.Invoke();
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        [Resolved]
        private IGameplayClock? gameplayClock { get; set; }

        [Resolved]
        private GameplayState? gameplayState { get; set; }

        private void updateInfoText()
        {
            playInfoText.Clear();
            playInfoText.AddText(GameplayMenuOverlayStrings.RetryCount);
            playInfoText.AddText(retries.ToString(), cp => cp.Font = cp.Font.With(weight: FontWeight.Bold));

            if (getSongProgress() is int progress)
            {
                playInfoText.NewLine();
                playInfoText.AddText(GameplayMenuOverlayStrings.SongProgress);
                playInfoText.AddText($"{progress}%", cp => cp.Font = cp.Font.With(weight: FontWeight.Bold));
            }

            if (gameplayState != null)
            {
                playInfoText.NewLine();
                playInfoText.AddText(BeatmapsetsStrings.ShowScoreboardHeadersAccuracy);
                playInfoText.AddText(": ");
                playInfoText.AddText(gameplayState!.ScoreProcessor.Accuracy.Value.FormatAccuracy(), cp => cp.Font = cp.Font.With(weight: FontWeight.Bold));
            }
        }

        private int? getSongProgress()
        {
            if (gameplayClock == null || gameplayState == null)
                return null;

            (double firstHitTime, double lastHitTime) = gameplayState.Beatmap.CalculatePlayableBounds();

            double playableLength = (lastHitTime - firstHitTime);

            if (playableLength == 0)
                return 0;

            return (int)Math.Clamp(((gameplayClock.CurrentTime - firstHitTime) / playableLength) * 100, 0, 100);
        }

        private partial class Button : DialogButton
        {
            // required to ensure keyboard navigation always starts from an extremity (unless the cursor is moved)
            protected override bool OnHover(HoverEvent e) => true;

            protected override bool OnMouseMove(MouseMoveEvent e)
            {
                State = SelectionState.Selected;
                return base.OnMouseMove(e);
            }
        }

        protected override bool Handle(UIEvent e)
        {
            switch (e)
            {
                case ScrollEvent:
                    if (ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
                        return globalAction.TriggerEvent(e);

                    break;
            }

            return base.Handle(e);
        }

        #region Pause loop sound handling

        public override bool IsPresent => base.IsPresent || pauseLoop.IsPlaying;

        private SkinnableSound pauseLoop = null!;

        private readonly IBindable<bool> windowActive = new Bindable<bool>(true);

        private float targetVolume => windowActive.Value && State.Value == Visibility.Visible ? 1.0f : 0;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            legacyEnabled.BindValueChanged(_ =>
            {
                modernContent.FadeTo(legacyEnabled.Value ? 0 : 1);
                legacyMenu!.FadeTo(legacyEnabled.Value ? 1 : 0);
                InternalButtons.Deselect();

                if (State.Value == Visibility.Visible)
                {
                    // Cancel a pending dismissal from the previous presentation without changing pause state.
                    this.FadeIn();
                    if (legacyEnabled.Value)
                        legacyMenu!.Appear();
                }
            });

            // Schedule required because host.IsActive doesn't seem to always run on the update thread.
            windowActive.BindValueChanged(_ => Schedule(() => pauseLoop.VolumeTo(targetVolume, 1000, Easing.Out)));
        }

        public void StopAllSamples()
        {
            if (!IsLoaded)
                return;

            pauseLoop.Stop();
        }

        private void startPauseLoop()
        {
            pauseLoop.VolumeTo(targetVolume, TRANSITION_DURATION, Easing.InQuint);
            pauseLoop.Play();
        }

        private void stopPauseLoop()
        {
            pauseLoop.VolumeTo(targetVolume, TRANSITION_DURATION, Easing.OutQuad).Finally(_ => pauseLoop.Stop());
        }

        #endregion
    }
}
