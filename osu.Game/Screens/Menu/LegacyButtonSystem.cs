// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics.Containers;
using osu.Game.Input.Bindings;
using osu.Game.Online.Rooms;
using osu.Game.Skinning;
using osuTK;
using osuTK.Input;

namespace osu.Game.Screens.Menu
{
    /// <summary>
    /// Replicates osu!stable's Main Menu button system and central cookie (from Menu.cs).
    /// Features two-tier navigation (Main / Play), beat bounce animations, elastic hover effects,
    /// 6-second inactivity auto-idle, Direct shortcut button, and hotkeys (P, E, O, Esc).
    /// </summary>
    public partial class LegacyButtonSystem : CompositeDrawable, IKeyBindingHandler<GlobalAction>
    {
        public enum MenuTier
        {
            Main,
            Play
        }

        public event Action<ButtonSystemState>? StateChanged;

        public Action? OnEditBeatmap;
        public Action? OnEditSkin;
        public Action<UIEvent>? OnExit;
        public Action? OnBeatmapListing;
        public Action? OnSolo;
        public Action? OnSettings;
        public Action? OnMultiplayer;
        public Action? OnQuickPlay;
        public Action? OnRankedPlay;
        public Action? OnPlaylists;
        public Action<Room>? OnDailyChallenge;

        public bool IsIdle { get; private set; } = true;
        public MenuTier CurrentTier { get; private set; } = MenuTier.Main;

        private Container contentContainer = null!;
        private LegacyCookie cookie = null!;
        private LegacyMenuVisualisation visualisation = null!;
        private Sample? cookieHitSample;
        private Box backgroundDim = null!;
        private Container libraryStatus = null!;

        private readonly List<LegacyMenuButton> mainTierButtons = new List<LegacyMenuButton>();
        private readonly List<LegacyMenuButton> playTierButtons = new List<LegacyMenuButton>();

        private LegacyMenuButton buttonPlay = null!;
        private LegacyMenuButton buttonEdit = null!;
        private LegacyMenuButton buttonOptions = null!;
        private LegacyMenuButton buttonExit = null!;

        private LegacyMenuButton buttonSolo = null!;
        private LegacyMenuButton buttonMulti = null!;
        private LegacyMenuButton buttonBack = null!;

        private ClickableContainer directButton = null!;

        private double lastActivityTime;
        private const double idle_timeout = 6000;

        public LegacyButtonSystem()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, SkinManager skins, AudioManager audio, IAPIProvider api, BeatmapManager beatmaps, LoginOverlay? login)
        {
            Texture? getTex(string name) => skin.GetStableTexture(name) ?? skins.DefaultClassicSkin.GetStableTexture(name);

            cookieHitSample = audio.Samples.Get(@"menuhit");

            InternalChild = new DrawSizePreservingFillContainer
            {
                TargetDrawSize = new Vector2(640, 480),
                RelativeSizeAxes = Axes.Both,
                Child = contentContainer = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        backgroundDim = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black,
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 54,
                            Colour = new Color4(0, 0, 0, 0.4f),
                        },
                        new Box
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            RelativeSizeAxes = Axes.X,
                            Height = 54,
                            Colour = new Color4(0, 0, 0, 0.4f),
                        },
                        new ClickableContainer
                        {
                            Size = new Vector2(205, 52),
                            Action = () => login?.ToggleVisibility(),
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Position = new Vector2(53, 2),
                                    Font = OsuFont.Inter.With(size: 14),
                                    Text = api.IsLoggedIn ? api.LocalUser.Value.Username : UserInterfaceStrings.LegacyGuest,
                                    Shadow = true,
                                },
                                new OsuSpriteText
                                {
                                    Position = new Vector2(53, 17),
                                    Font = OsuFont.Inter.With(size: 10),
                                    Text = api.IsLoggedIn ? string.Empty : UserInterfaceStrings.LegacySignIn,
                                    Shadow = true,
                                },
                                new Sprite
                                {
                                    Position = new Vector2(80, 43),
                                    Texture = getTex(@"levelbar"),
                                    Scale = new Vector2(1 / LegacySkin.STABLE_MAGIC_SCALE_FACTOR),
                                    Alpha = 0.5f,
                                },
                            },
                        },
                        libraryStatus = new Container
                        {
                            Position = new Vector2(212, 0),
                            AutoSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Font = OsuFont.Inter.With(size: 14),
                                    Text = UserInterfaceStrings.LegacyBeatmapCount(beatmaps.GetAllUsableBeatmapSets().Sum(set => set.Beatmaps.Count)),
                                    Shadow = true,
                                },
                                new OsuSpriteText
                                {
                                    Y = 14,
                                    Font = OsuFont.Inter.With(size: 12),
                                    Text = api.IsLoggedIn ? string.Empty : UserInterfaceStrings.LegacyOnlinePrompt,
                                    Shadow = true,
                                },
                            },
                        },
                        // Direct button on right edge
                        directButton = new ClickableContainer
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Position = Vector2.Zero,
                            Scale = new Vector2(1 / LegacySkin.STABLE_MAGIC_SCALE_FACTOR),
                            AutoSizeAxes = Axes.Both,
                            Action = () =>
                            {
                                recordActivity();
                                OnBeatmapListing?.Invoke();
                            },
                            Children = new Drawable[]
                            {
                                new Sprite
                                {
                                    Texture = getTex(@"menu-osudirect"),
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                },
                                new Sprite
                                {
                                    Texture = getTex(@"menu-osudirect-over"),
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    Blending = BlendingParameters.Additive,
                                    Alpha = 0,
                                }
                            }
                        },
                        // stable draws the spectrum behind the buttons and the cookie; the cookie positions and sizes it every frame.
                        visualisation = new LegacyMenuVisualisation
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                        },
                        // Main tier buttons (stable Menu.cs line 324-327)
                        buttonPlay = new LegacyMenuButton(@"play", new Vector2(-100f, -125f))
                        {
                            Action = () =>
                            {
                                recordActivity();
                                ChangeTier(MenuTier.Play);
                            }
                        },
                        buttonEdit = new LegacyMenuButton(@"edit", new Vector2(-100f, -60f))
                        {
                            Action = () =>
                            {
                                recordActivity();
                                OnEditBeatmap?.Invoke();
                            }
                        },
                        buttonOptions = new LegacyMenuButton(@"options", new Vector2(-100f, 5f))
                        {
                            Action = () =>
                            {
                                recordActivity();
                                OnSettings?.Invoke();
                            }
                        },
                        buttonExit = new LegacyMenuButton(@"exit", new Vector2(-100f, 70f))
                        {
                            ActionWithEvent = e =>
                            {
                                recordActivity();
                                OnExit?.Invoke(e);
                            }
                        },
                        // Play tier buttons (stable Menu.cs line 328-330)
                        buttonSolo = new LegacyMenuButton(@"freeplay", new Vector2(-100f, -92.5f))
                        {
                            Action = () =>
                            {
                                recordActivity();
                                OnSolo?.Invoke();
                            }
                        },
                        buttonMulti = new LegacyMenuButton(@"multiplayer", new Vector2(-100f, -27.5f))
                        {
                            Action = () =>
                            {
                                recordActivity();
                                OnMultiplayer?.Invoke();
                            }
                        },
                        buttonBack = new LegacyMenuButton(@"back", new Vector2(-100f, 37.5f))
                        {
                            Action = () =>
                            {
                                recordActivity();
                                ChangeTier(MenuTier.Main);
                            }
                        },
                        // Cookie (osu! circle button)
                        cookie = new LegacyCookie(visualisation)
                        {
                            Action = onCookieClicked
                        }
                    }
                }
            };

            mainTierButtons.AddRange(new[] { buttonPlay, buttonEdit, buttonOptions, buttonExit });
            playTierButtons.AddRange(new[] { buttonSolo, buttonMulti, buttonBack });

            SetIdle(true, instant: true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            recordActivity();
        }

        private void onCookieClicked()
        {
            recordActivity();

            if (IsIdle)
            {
                // stable's CheckIdle plays the cookie's hit sound when waking the menu; otherwise the triggered button plays its own.
                cookieHitSample?.Play();
                SetIdle(false, instant: false);
            }
            else
            {
                if (CurrentTier == MenuTier.Main)
                    buttonPlay.TriggerClick();
                else
                    buttonSolo.TriggerClick();
            }
        }

        public void ChangeTier(MenuTier tier)
        {
            if (CurrentTier == tier)
                return;

            bool movingUp = tier < CurrentTier;
            CurrentTier = tier;

            if (tier == MenuTier.Main)
            {
                foreach (var b in playTierButtons)
                    b.TransitionOut(movingUp);

                foreach (var b in mainTierButtons)
                    b.TransitionIn(movingUp);

                StateChanged?.Invoke(ButtonSystemState.TopLevel);
            }
            else
            {
                foreach (var b in mainTierButtons)
                    b.TransitionOut(movingUp);

                foreach (var b in playTierButtons)
                    b.TransitionIn(movingUp);

                StateChanged?.Invoke(ButtonSystemState.Play);
            }
        }

        /// <summary>
        /// Stable's <c>Menu.SetIdle</c>. Idle: the background shows fully, the buttons fade out over 500ms while drifting left, and the cookie
        /// drifts to the centre over 2000ms. Active: the background is dimmed to 40%, the current tier's buttons slide in and the cookie returns
        /// to its place left of centre over 400ms.
        /// </summary>
        public void SetIdle(bool idle, bool instant = false)
        {
            IsIdle = idle;
            backgroundDim.FadeTo(idle ? 0 : 0.6f, instant ? 0 : 500);
            libraryStatus.FadeTo(idle ? 0 : 1, instant ? 0 : 500);

            if (IsIdle)
            {
                cookie.MoveTo(Vector2.Zero, instant ? 0 : 2000, Easing.Out);

                foreach (var b in mainTierButtons.Concat(playTierButtons))
                    b.SlideOut(instant);

                StateChanged?.Invoke(ButtonSystemState.Initial);
            }
            else
            {
                cookie.MoveTo(new Vector2(-120f, 0f), 400, Easing.Out);

                CurrentTier = MenuTier.Main;

                foreach (var b in playTierButtons)
                    b.SlideOut(true);

                foreach (var b in mainTierButtons)
                    b.SlideIn();

                StateChanged?.Invoke(ButtonSystemState.TopLevel);
            }
        }

        /// <summary>
        /// Stable's exit fade (<c>GameBase.OnExitFade</c>): every button moves 50 units left and fades out over 100ms, while the cookie
        /// shrinks, turns and fades out on its own.
        /// </summary>
        public void BeginExit()
        {
            foreach (var b in mainTierButtons.Concat(playTierButtons))
                b.FadeAway();

            cookie.Exiting = true;
        }

        private void recordActivity()
        {
            lastActivityTime = Time.Current;
        }

        protected override void Update()
        {
            base.Update();

            if (!IsIdle && Time.Current - lastActivityTime > idle_timeout)
                SetIdle(true, instant: false);
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            recordActivity();
            return base.OnMouseMove(e);
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            recordActivity();

            if (e.Key == Key.P)
            {
                if (IsIdle)
                    SetIdle(false);
                else if (CurrentTier == MenuTier.Main)
                    buttonPlay.TriggerClick();
                else
                    buttonSolo.TriggerClick();
                return true;
            }

            if (e.Key == Key.E)
            {
                if (IsIdle)
                    SetIdle(false);
                buttonEdit.TriggerClick();
                return true;
            }

            if (e.Key == Key.O)
            {
                if (IsIdle)
                    SetIdle(false);
                buttonOptions.TriggerClick();
                return true;
            }

            return base.OnKeyDown(e);
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            recordActivity();

            switch (e.Action)
            {
                case GlobalAction.Back:
                    if (CurrentTier == MenuTier.Play)
                    {
                        ChangeTier(MenuTier.Main);
                        return true;
                    }

                    if (!IsIdle)
                    {
                        SetIdle(true);
                        return true;
                    }

                    OnExit?.Invoke(e);
                    return true;

                case GlobalAction.Select:
                    cookie.TriggerClick();
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        /// <summary>
        /// osu!stable's menu cookie (<c>Menu.s_Osu</c> and its glow copy <c>s_Osu2</c>), sized every frame as in <c>Menu.Update</c>:
        /// it eases from 1.05x down to 1x over each beat, pushed back up by sudden rises in loudness, plus up to 0.1 while hovered.
        /// The glow copy tracks it slightly larger and fades with the beat; a shockwave ripples out on every beat.
        /// The spectrum's radius follows the same curve, so the bars always start just inside the cookie's edge.
        /// </summary>
        private partial class LegacyCookie : BeatSyncedContainer
        {
            /// <summary>
            /// Stable's hover radius is 300 (texture pixels at 2x) scaled with the cookie: 150 units.
            /// </summary>
            private const float hover_radius = 150;

            /// <summary>
            /// The spectrum starts at 150 units times the cookie's beat scale.
            /// </summary>
            private const float visualisation_radius = 150;

            /// <summary>
            /// The cookie's place while the menu is open; the spectrum fades as the cookie moves there.
            /// </summary>
            private const float open_x = -120;

            private const double frame_length = 1000 / 60.0;

            public Action? Action { get; set; }

            /// <summary>
            /// Set when the game is exiting: the cookie then shrinks, turns and fades out.
            /// </summary>
            public bool Exiting { get; set; }

            private readonly LegacyMenuVisualisation visualisation;

            private Sprite baseCookie = null!;
            private Sprite glowCookie = null!;
            private Container ripples = null!;
            private Texture? shockwaveTexture;
            private Sample? heartbeatSample;

            private float hoverBonus;
            private float progress;
            private float audioVelocityAverage;
            private float menuAlpha = 1;
            private float savedProgressMultiplier = 1;

            public LegacyCookie(LegacyMenuVisualisation visualisation)
            {
                this.visualisation = visualisation;

                Anchor = Anchor.Centre;
                Origin = Anchor.Centre;
                Size = new Vector2(337.5f);
            }

            public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
            {
                var localPos = ToLocalSpace(screenSpacePos) - DrawSize / 2f;
                float radius = hover_radius * baseCookie.Scale.X;
                return localPos.LengthSquared <= radius * radius;
            }

            [BackgroundDependencyLoader]
            private void load(ISkinSource skin, SkinManager skins, AudioManager audio, TextureStore textures)
            {
                Texture? cookieTex = skin.GetStableTexture(@"menu-osu")
                                  ?? skins.DefaultClassicSkin.GetStableTexture(@"menu-osu")
                                  ?? textures.Get(@"Menu/logo");

                shockwaveTexture = skin.GetStableTexture(@"menu-osu-shockwave") ?? skins.DefaultClassicSkin.GetStableTexture(@"menu-osu-shockwave");
                heartbeatSample = audio.Samples.Get(@"heartbeat");

                Children = new Drawable[]
                {
                    ripples = new Container
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                    },
                    baseCookie = new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Texture = cookieTex,
                    },
                    // stable's s_Osu2 is a second copy of menu-osu, not the shockwave.
                    glowCookie = new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Texture = cookieTex,
                        Alpha = 0,
                    },
                };

                if (cookieTex == null)
                {
                    AddInternal(new CircularContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = new Color4(255, 102, 170, 255),
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Text = @"osu!",
                                Font = OsuFont.GetFont(size: 80, weight: FontWeight.Bold),
                            }
                        }
                    });
                }
            }

            protected override void Update()
            {
                base.Update();

                float frameRatio = (float)(Time.Elapsed / frame_length);

                if (Exiting)
                {
                    hoverBonus -= frameRatio * 0.003f;
                    Rotation += float.RadiansToDegrees(frameRatio * 0.001f);
                    Alpha = Math.Max(0, Alpha - frameRatio * 0.002f);
                }
                else if (IsHovered || hoverBonus < 0)
                    hoverBonus = Math.Min(hoverBonus + 0.012f * frameRatio, 0.1f);
                else
                    hoverBonus = Math.Max(hoverBonus - 0.012f * frameRatio, 0);

                var track = BeatSyncSource.Clock;
                bool playing = track?.IsRunning == true;
                bool kiai = playing && EffectPoint.KiaiMode;

                // stable's channel levels are 0..32768 per side.
                var amplitudes = BeatSyncSource.CurrentAmplitudes;
                float level = (amplitudes.LeftChannel + amplitudes.RightChannel) * 32768;

                float loudness = kiai ? 1 : 0.6f + Math.Clamp((level - 30000) / 35536, 0, 1) * 0.4f;
                float audioVelocity = level / 65536;

                // stable updates these at 60fps.
                float averageDecay = MathF.Pow(0.9f, frameRatio);
                audioVelocityAverage = audioVelocityAverage * averageDecay + (1 - averageDecay) * audioVelocity;

                float alphaDecay = MathF.Pow(0.8f, frameRatio);
                menuAlpha = menuAlpha * alphaDecay + (1 - alphaDecay) * loudness;

                float beatProgress = 0.5f;

                if (!playing)
                    beatProgress = (float)(Time.Current % 1000 / 1000);
                else if (TimingPoint.BeatLength > 0)
                    beatProgress = (float)(((track!.CurrentTime - TimingPoint.Time) % TimingPoint.BeatLength + TimingPoint.BeatLength) % TimingPoint.BeatLength / TimingPoint.BeatLength);

                float velocityKick = 1 - (audioVelocity - audioVelocityAverage) / 0.5f;
                float progressDecay = MathF.Pow(0.5f, frameRatio);
                progress = progress * progressDecay + Math.Clamp(1 - (beatProgress * 0.5f + velocityKick * 0.5f), 0, 1) * (1 - progressDecay);

                baseCookie.Scale = new Vector2(easeOutQuad(progress, 1.05f + hoverBonus, 1 + hoverBonus));

                glowCookie.Alpha = easeOutQuad(progress, kiai ? 0.1f : 0.4f, 0) * savedProgressMultiplier;
                glowCookie.Scale = new Vector2(easeOutQuad(progress, 1.05f + hoverBonus, 1.08f + hoverBonus));
                glowCookie.Blending = kiai ? BlendingParameters.Additive : BlendingParameters.Inherit;

                visualisation.Position = Position;
                visualisation.Radius = easeInQuad(1 - progress, 1.05f + hoverBonus, 1.08f + hoverBonus) * visualisation_radius;
                visualisation.VisualisationAlpha = (1 - X / open_x * 0.7f) * (kiai ? 1 : 0.7f) * Alpha;
            }

            /// <summary>
            /// Stable's <c>OsuMathHelper.easeOutVal</c> (quadratic, over a unit duration).
            /// </summary>
            private static float easeOutQuad(float t, float start, float end) => start + (end - start) * t * (2 - t);

            /// <summary>
            /// Stable's <c>OsuMathHelper.easeInVal</c> (quadratic, over a unit duration).
            /// </summary>
            private static float easeInQuad(float t, float start, float end) => start + (end - start) * t * t;

            protected override void OnNewBeat(int beatIndex, TimingControlPoint timingPoint, EffectControlPoint effectPoint, ChannelAmplitudes amplitudes)
            {
                base.OnNewBeat(beatIndex, timingPoint, effectPoint, amplitudes);

                savedProgressMultiplier = menuAlpha;

                if (IsHovered && !Exiting)
                    heartbeatSample?.Play();

                generateRipple();
            }

            /// <summary>
            /// Stable's <c>GenerateRipple</c>: an additive shockwave at 10% of the loudness, growing 1.4x and fading over a second.
            /// </summary>
            private void generateRipple()
            {
                if (shockwaveTexture == null || Exiting)
                    return;

                var ripple = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Texture = shockwaveTexture,
                    Blending = BlendingParameters.Additive,
                    Alpha = 0.1f * menuAlpha,
                    Scale = baseCookie.Scale,
                };

                ripples.Add(ripple);

                ripple.FadeOut(1000);
                ripple.ScaleTo(baseCookie.Scale * 1.4f, 1000, Easing.Out).Expire();
            }

            protected override bool OnHover(HoverEvent e) => true;

            protected override bool OnClick(ClickEvent e)
            {
                hoverBonus -= 0.08f;
                Action?.Invoke();
                return true;
            }
        }
    }
}
