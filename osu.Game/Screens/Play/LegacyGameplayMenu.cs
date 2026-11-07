// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Audio;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Scoring;
using osu.Game.Screens.Select;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// osu!stable's pause and fail screens (<c>Player.togglePause</c>): the skin's <c>pause-overlay</c> or <c>fail-background</c> over a black
    /// dim, the skin's <c>pause-continue</c>, <c>pause-retry</c> and <c>pause-back</c> buttons, and the <c>arrow-pause</c> pointers that keyboard
    /// selection brings up.
    /// </summary>
    public partial class LegacyGameplayMenu : CompositeDrawable
    {
        // stable lays the screen out in 640x480 units, which g0v0's legacy screens draw at 1.6x.
        private const float scale = 1.6f;

        private const float arrow_x = 170 * scale;
        private const float arrow_bob = 45 * scale;

        private readonly bool failed;
        private readonly Action? resume;
        private readonly Action? retry;
        private readonly Action? quit;
        private readonly Func<Task<ScoreInfo>>? saveReplay;

        private Box dim = null!;
        private Sprite? overlay;
        private Sprite? failBackground;
        private OsuSpriteText? replayText;
        private Container content = null!;

        // stable's pauseSprites[2..4]: one slot per position, even when the button isn't offered.
        private readonly LegacyMenuButton?[] buttons = new LegacyMenuButton?[3];

        private readonly List<Sprite> arrows = new List<Sprite>();
        private Texture? arrowTexture;
        private Color4 arrowColour = Color4.White;

        private float dimAlpha;
        private int selected = -1;
        private bool presentingReplay;

        /// <summary>
        /// Whether the screen is up. It keeps fading out for a while after being dismissed, and takes no input meanwhile.
        /// </summary>
        private bool active;

        public override bool PropagatePositionalInputSubTree => active && base.PropagatePositionalInputSubTree;

        private SkinnableSound hoverSample = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        public LegacyGameplayMenu(bool failed, Action? resume, Action? retry, Action? quit, Func<Task<ScoreInfo>>? saveReplay)
        {
            this.failed = failed;
            this.resume = resume;
            this.retry = retry;
            this.quit = quit;
            this.saveReplay = saveReplay;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, SkinManager skins)
        {
            // stable dims further for skins made before its 2.0 layout (SkinManager.UseNewLayout).
            decimal? version = skin.GetConfig<SkinConfiguration.LegacySetting, decimal>(SkinConfiguration.LegacySetting.Version)?.Value;
            dimAlpha = version is decimal v && v <= 1 ? 0.95f : 0.8f;

            arrowTexture = skin.GetStableTexture(@"arrow-pause");

            if (arrowTexture == null)
            {
                // stable tints the old warning arrow the new layout's blue.
                arrowTexture = skin.GetStableTexture(@"play-warningarrow") ?? skins.DefaultClassicSkin.GetStableTexture(@"play-warningarrow");
                arrowColour = new Color4(0, 114, 255, 255);
            }

            content = new Container
            {
                RelativeSizeAxes = Axes.Both,
            };

            InternalChildren = new Drawable[]
            {
                dim = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0,
                },
                // stable scales its pause screen by the window's height, with its centre field following the width.
                new DrawSizePreservingFillContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    TargetDrawSize = new Vector2(1024, 768),
                    Strategy = DrawSizePreservationStrategy.Minimum,
                    Child = content,
                },
                hoverSample = new SkinnableSound(new SampleInfo(@"pause-hover")),
            };

            if (failed)
            {
                var texture = skin.GetStableTexture(@"fail-background");

                if (texture != null)
                {
                    content.Add(failBackground = new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Texture = texture,
                        // stable fits the fail background to the screen's height.
                        Scale = new Vector2(768 / texture.DisplayHeight),
                        Alpha = 0,
                    });
                }

                if (saveReplay != null)
                {
                    content.Add(replayText = new OsuSpriteText
                    {
                        Position = new Vector2(10 * scale),
                        Text = UserInterfaceStrings.LegacyFailViewReplay,
                        Font = LegacyFonts.Get(16 * scale, FontWeight.Bold),
                        Shadow = true,
                        Alpha = 0,
                    });
                }
            }
            else
            {
                var texture = skin.GetStableTexture(@"pause-overlay");

                if (texture != null)
                {
                    content.Add(overlay = new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Texture = texture,
                        Alpha = 0,
                    });
                }
            }

            if (!failed && resume != null)
                addButton(0, @"pause-continue", GameplayMenuOverlayStrings.Continue, new Color4(136, 179, 0, 255), -100, resume);

            if (retry != null)
                addButton(1, @"pause-retry", GameplayMenuOverlayStrings.Retry, new Color4(255, 170, 0, 255), 10, retry);

            if (quit != null)
                addButton(2, @"pause-back", GameplayMenuOverlayStrings.Quit, new Color4(170, 27, 39, 255), 120, quit);
        }

        private void addButton(int index, string name, LocalisableString fallbackText, Color4 fallbackColour, float y, Action action)
        {
            content.Add(buttons[index] = new LegacyMenuButton(name, fallbackText, fallbackColour)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Y = y * scale,
                Alpha = 0,
                Action = action,
            });
        }

        public void Appear()
        {
            active = true;
            selected = -1;
            clearArrows();

            dim.FadeTo(dimAlpha, 300);
            overlay?.FadeIn(300);

            // stable adds the fail background and its replay hint when the screen first comes up; they never fade out.
            failBackground?.FadeInFromZero(600);
            replayText?.Show();

            foreach (var button in buttons)
            {
                if (button == null) continue;

                button.ClearTransforms();
                button.Scale = Vector2.One;
                button.FadeIn(300);
            }
        }

        public void Disappear()
        {
            active = false;
            clearArrows();

            dim.FadeOut(800);
            overlay?.FadeOut(800);

            foreach (var button in buttons)
                button?.FadeOut(300);
        }

        /// <summary>
        /// stable's <c>pauseKey</c>: 0 confirms the selection (or resumes when nothing is selected), -1 and 1 move the selection up and down.
        /// </summary>
        public void PressKey(int direction)
        {
            if (!active)
                return;

            if (direction == 0)
            {
                if (selected >= 0)
                    buttons[selected]?.Activate();
                else if (!failed)
                    resume?.Invoke();

                return;
            }

            if (selected < 0)
            {
                selected = direction == -1 ? 2 : failed ? 1 : 0;
                createArrows();
                return;
            }

            hoverSample.Play();

            // a failed screen has no continue button, so its selection cycles between retry and back.
            selected = failed ? (selected + direction + 1) % 2 + 1 : (selected + direction + 3) % 3;

            foreach (var arrow in arrows)
                arrow.MoveToY(selectedY, 100, Easing.Out);
        }

        private float selectedY => (selected switch
        {
            0 => -100,
            1 => 10,
            _ => 120,
        }) * scale;

        private void createArrows()
        {
            if (arrowTexture == null)
                return;

            foreach (int side in new[] { -1, 1 })
            {
                float x = side * arrow_x;

                var arrow = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Texture = arrowTexture,
                    Colour = arrowColour,
                    // the right arrow is the left one flipped.
                    Scale = new Vector2(-side * 0.8f, 0.8f),
                    Position = new Vector2(2 * x, selectedY),
                };

                content.Add(arrow);
                arrows.Add(arrow);

                arrow.FadeInFromZero(300);
                // stable slides the arrows in, then bobs them outwards and back every 600ms.
                arrow.MoveToX(x, 300, Easing.In)
                     .Then()
                     .Loop(l => l.MoveToX(x + side * arrow_bob, 300, Easing.Out)
                                 .Then()
                                 .MoveToX(x, 300, Easing.In));
            }
        }

        private void clearArrows()
        {
            foreach (var arrow in arrows)
                arrow.FadeOut(200).Expire();

            arrows.Clear();
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            // stable submits the failed score and plays it back from the start.
            if (active && failed && e.Key == Key.F1 && !e.Repeat && saveReplay != null && !presentingReplay)
            {
                presentingReplay = true;

                Task.Run(saveReplay).ContinueWith(t =>
                {
                    ScoreInfo? score = null;

                    // if saving fails, F1 can be pressed again.
                    if (t.IsCompletedSuccessfully)
                        score = realm.Run(r => r.Find<ScoreInfo>(t.GetResultSafely().ID)?.Detach());
                    else
                        Logger.Error(t.Exception, @"Failed to save the failed score for its replay");

                    Schedule(() =>
                    {
                        if (score != null)
                            game?.PresentScore(score, ScorePresentType.Gameplay);
                        else
                            presentingReplay = false;
                    });
                });

                return true;
            }

            return base.OnKeyDown(e);
        }

        /// <summary>
        /// One of stable's pause buttons: the skin's texture, which grows on hover and flashes when clicked.
        /// </summary>
        private partial class LegacyMenuButton : CompositeDrawable
        {
            public Action? Action { get; init; }

            private readonly string name;
            private readonly LocalisableString fallbackText;
            private readonly Color4 fallbackColour;

            private Sprite? sprite;
            private LegacyMissingTexture? placeholder;
            private SkinnableSound hoverSample = null!;
            private SkinnableSound clickSample = null!;

            public LegacyMenuButton(string name, LocalisableString fallbackText, Color4 fallbackColour)
            {
                this.name = name;
                this.fallbackText = fallbackText;
                this.fallbackColour = fallbackColour;

                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(ISkinSource skin)
            {
                var texture = skin.GetStableTexture(name);

                InternalChildren = new Drawable[]
                {
                    texture != null
                        ? sprite = new Sprite { Texture = texture }
                        // osu!stable's own buttons come from osu!ui.dll, which can't be shipped; stand in for the texture instead.
                        : placeholder = new LegacyMissingTexture(name, fallbackText, fallbackColour, new Vector2(300, 66) * scale),
                    // stable plays the button's own hover sample, falling back to the shared one.
                    hoverSample = new SkinnableSound(new SampleInfo($@"{name}-hover", @"pause-hover")),
                    clickSample = new SkinnableSound(new SampleInfo($@"{name}-click")),
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverSample.Play();
                this.ScaleTo(1.1f, 120, Easing.Out);
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                this.ScaleTo(1, 120, Easing.Out);
                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                Activate();
                return true;
            }

            public void Activate()
            {
                clickSample.Play();

                if (sprite != null)
                {
                    // stable's animatePauseButton: an additive copy that swells slightly as it fades.
                    var flash = new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Texture = sprite.Texture,
                        Blending = BlendingParameters.Additive,
                        BypassAutoSizeAxes = Axes.Both,
                    };

                    AddInternal(flash);
                    flash.FadeOutFromOne(400);
                    flash.ScaleTo(1.01f, 400).Expire();
                }

                placeholder?.Flash();

                Action?.Invoke();
            }
        }
    }
}
