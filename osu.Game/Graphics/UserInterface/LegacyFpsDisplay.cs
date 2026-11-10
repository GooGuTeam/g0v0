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
using osu.Framework.Platform;
using osu.Framework.Timing;
using osu.Game.Configuration;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Graphics.UserInterface
{
    /// <summary>
    /// osu!stable's frame rate counter (<c>FpsDisplay</c>), in the bottom-right corner of a 768 px tall space: the frame rate over its limit
    /// in a box tinted by how close it gets, the frame time in a box below it, and a row of dots that light up as frames drop. Once the frame
    /// rate holds its limit, the frame rate box fades away and the frame time box dims.
    /// </summary>
    /// <remarks>
    /// Stable draws these from its own <c>fps-*</c> textures, which skins can't replace and aren't bundled, so this draws them: dark digits on
    /// white rounded boxes, tinted as stable tints them.
    /// </remarks>
    public partial class LegacyFpsDisplay : CompositeDrawable
    {
        private const float unit = 1.6f;

        private static readonly Color4 colour_holyfuck = new Color4(255, 36, 0, 255);
        private static readonly Color4 colour_danger = new Color4(255, 149, 24, 255);
        private static readonly Color4 colour_warning = new Color4(255, 204, 34, 255);
        private static readonly Color4 colour_okay = new Color4(172, 220, 25, 255);

        private readonly Bindable<bool> showFpsDisplay = new Bindable<bool>();
        private readonly Bindable<bool> legacy = new Bindable<bool>();

        private Container fpsBox = null!;
        private Container delayBox = null!;
        private DigitText fpsText = null!;
        private DigitText limitText = null!;
        private DigitText delayText = null!;
        private readonly List<Container> droppedFrames = new List<Container>();

        private GameHost host = null!;
        private ThrottledFrameClock drawClock = null!;
        private ThrottledFrameClock updateClock = null!;
        private ThrottledFrameClock inputClock = null!;

        // PerformanceMonitor: frames are counted over 250ms windows, the frame time is smoothed by 0.05^(elapsed / 1000).
        private int framesCounted;
        private double timeCounted;
        private double framesPerSecond;
        private double averageFrameTime = 99;
        private double lastDrawTime;

        private double displayedFps;
        private double droppedFramesLast;
        private bool faded;

        public LegacyFpsDisplay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, GameHost gameHost)
        {
            host = gameHost;
            drawClock = gameHost.DrawThread.Clock;
            updateClock = gameHost.UpdateThread.Clock;
            inputClock = gameHost.InputThread.Clock;

            config.BindWith(OsuSetting.ShowFpsDisplay, showFpsDisplay);
            config.BindWith(OsuSetting.ToriiLegacyFpsCounter, legacy);

            var corner = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    // positions are stable's, measured in units from the bottom-right corner (Fields.BottomRight), with their own origins.
                    fpsBox = box(new Vector2(23, 25)),
                    // the frame rate centred 35 units in, its limit hanging from 29 units in, lower and smaller, as stable lays them out.
                    fpsText = new DigitText(9)
                    {
                        Anchor = Anchor.BottomRight,
                        Origin = Anchor.Centre,
                        Position = -new Vector2(35, 25) * unit,
                        Font = digits(15),
                        Colour = Color4.Black,
                    },
                    limitText = new DigitText(7)
                    {
                        Anchor = Anchor.BottomRight,
                        Origin = Anchor.TopLeft,
                        // Inter's line sits lower in its box than stable's glyphs; lifted so the text ends inside the box, as stable's does.
                        Position = -new Vector2(29, 25) * unit - new Vector2(0, 2),
                        Font = digits(11),
                        Colour = new Color4(85, 85, 85, 255),
                    },
                    delayBox = box(new Vector2(23, 10)),
                    delayText = new DigitText(9)
                    {
                        Anchor = Anchor.BottomRight,
                        Origin = Anchor.Centre,
                        Position = -new Vector2(23, 10) * unit,
                        Font = digits(15),
                        Colour = Color4.Black,
                    },
                },
            };

            for (int i = 0; i < 10; i++)
            {
                var dot = new Container
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.Centre,
                    Position = -new Vector2(49 + i * 7, 6) * unit,
                    Size = new Vector2(8),
                    CornerRadius = 2,
                    Masking = true,
                    Alpha = 0,
                    Colour = i == 0 ? colour_warning : i < 3 ? colour_danger : colour_holyfuck,
                    Child = new Box { RelativeSizeAxes = Axes.Both },
                };

                droppedFrames.Add(dot);
                corner.Add(dot);
            }

            InternalChild = new DrawSizePreservingFillContainer
            {
                RelativeSizeAxes = Axes.Both,
                TargetDrawSize = new Vector2(1024, 768),
                Strategy = DrawSizePreservationStrategy.Minimum,
                Child = corner,
            };

            // Inter stands in for stable's Aller glyphs, sized to their height.
            static FontUsage digits(float size) => OsuFont.Inter.With(size: size, fixedWidth: true);

            static Container box(Vector2 position) => new Container
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.Centre,
                Position = -position * unit,
                Size = new Vector2(65, 20),
                CornerRadius = 5,
                Masking = true,
                Colour = Color4.Black,
                Child = new Box { RelativeSizeAxes = Axes.Both },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            showFpsDisplay.BindValueChanged(_ => updateVisibility());
            legacy.BindValueChanged(_ => updateVisibility(), true);
        }

        private void updateVisibility()
        {
            bool visible = showFpsDisplay.Value && legacy.Value;

            Alpha = visible ? 1 : 0;

            // FpsDisplay.Reset: starts over from zero whenever it's turned on.
            if (visible)
            {
                displayedFps = 0;
                fpsText.Text = @"0";
            }
        }

        protected override void Update()
        {
            base.Update();

            if (Alpha == 0)
                return;

            monitorDrawFrame();

            double target = targetFrameRate;
            char suffix = 'f';

            if (FrameRateTarget.IsUnlimited(target))
            {
                target = Math.Round(FrameRateTarget.Effective(target, host));
                suffix = 'h';
            }

            // the shown frame rate eases towards the measured one, capped at the target.
            if (Math.Abs(framesPerSecond - displayedFps) > 0.5)
            {
                double decay = Math.Pow(0.05, Time.Elapsed / 1000);
                displayedFps = displayedFps * decay + Math.Min(target, framesPerSecond) * (1 - decay);

                int rounded = (int)Math.Round(displayedFps);
                fpsText.Text = rounded == 0 ? string.Empty : $"{rounded:#,#}";
                limitText.Text = $"/{target}{(suffix == 'h' ? "hz" : "fps")}";
            }

            setFaded(Math.Abs(displayedFps - target) < 0.5);

            delayText.Text = averageFrameTime >= 10 ? $"{averageFrameTime:0}ms" : averageFrameTime >= 1 ? $"{averageFrameTime:0.0}ms" : $"{averageFrameTime:0.00}ms";

            if (faded)
                fpsBox.Colour = colour_okay;
            else if (displayedFps > target - 10 && displayedFps > target * 0.95)
                fpsBox.Colour = colour_warning;
            else
                fpsBox.Colour = colour_danger;

            delayBox.Colour = averageFrameTime < 8 ? colour_okay : averageFrameTime < 16 ? colour_warning : colour_danger;
        }

        /// <summary>
        /// The draw limiter, or the input thread's when the update thread isn't throttled, as lazer's own counter takes it.
        /// </summary>
        private double targetFrameRate => updateClock.Throttling ? drawClock.MaximumUpdateHz : inputClock.MaximumUpdateHz;

        /// <summary>
        /// PerformanceMonitor.NewFrame, once per drawn frame.
        /// </summary>
        private void monitorDrawFrame()
        {
            double drawTime = drawClock.CurrentTime;

            if (drawTime == lastDrawTime)
            {
                updateDroppedFrames(false);
                return;
            }

            double elapsed = drawClock.ElapsedFrameTime;
            lastDrawTime = drawTime;

            // a long pause (a debugger, or the window in the background) isn't a frame.
            if (elapsed > 10000)
                return;

            framesCounted++;
            timeCounted += elapsed;

            if (timeCounted >= 250)
            {
                framesPerSecond = Math.Ceiling(Math.Min(framesCounted * 1000 / timeCounted, FrameRateTarget.Effective(targetFrameRate, host)));
                framesCounted = 0;
                timeCounted = 0;
            }

            float refreshRate = host.Window?.CurrentDisplayMode.Value.RefreshRate ?? 60;
            double spikeTime = Math.Max(averageFrameTime * 2, Math.Min(6, 1000 / (refreshRate > 0 ? refreshRate : 60)));
            bool dropped = elapsed > spikeTime;

            averageFrameTime = Math.Pow(0.05, elapsed / 1000) * averageFrameTime + (1 - Math.Pow(0.05, elapsed / 1000)) * elapsed;

            updateDroppedFrames(dropped);
        }

        /// <summary>
        /// One more dot lights per dropped frame, up to ten, and they go out one every 800ms, each later one waiting longer.
        /// </summary>
        private void updateDroppedFrames(bool newDroppedFrame)
        {
            double count = newDroppedFrame
                ? Math.Min(10, Math.Ceiling(droppedFramesLast + 1))
                : Math.Max(0, droppedFramesLast - Time.Elapsed / 800);

            int before = (int)Math.Ceiling(droppedFramesLast);
            int after = (int)Math.Ceiling(count);

            if (after < before)
                droppedFrames[after].Delay(after * 320).FadeOut(200);
            else if (after > before)
                droppedFrames[after - 1].FadeIn(40);

            droppedFramesLast = count;
        }

        /// <summary>
        /// At its limit, the frame rate box and text fade out after four seconds and the frame time box dims; off it, they come back.
        /// </summary>
        private void setFaded(bool value)
        {
            if (value == faded)
                return;

            faded = value;

            if (faded)
            {
                delayBox.FadeTo(0.8f, 10000);
                fpsBox.Delay(4000).FadeOut(400);
                fpsText.Delay(4000).FadeOut(400);
                limitText.Delay(4000).FadeOut(400);
            }
            else
            {
                delayBox.FadeIn(200);
                fpsBox.FadeIn(1000);
                fpsText.FadeIn(1000);
                limitText.FadeIn(1000);
            }
        }

        /// <summary>
        /// Text whose digits all advance by the same width, as stable's fps glyphs do (<c>pSpriteText.TextConstantSpacing</c>), while its
        /// units and separators keep their own widths.
        /// </summary>
        private partial class DigitText : OsuSpriteText
        {
            private const float inter_digit_width = 0.52f;

            private readonly float advance;

            /// <param name="advance">how far each digit advances: stable's glyph width, 9 px for the frame rate and 7 px for its limit.</param>
            public DigitText(float advance)
            {
                this.advance = advance;
            }

            protected override char FixedWidthReferenceCharacter => '0';

            protected override char[] FixedWidthExcludeCharacters => new[] { '/', ',', '.', 'f', 'p', 's', 'h', 'z', 'm' };

            protected override void LoadComplete()
            {
                base.LoadComplete();

                // fixed width spaces digits by Inter's digit width, about 0.52 of its size; the rest of stable's advance goes between them.
                Spacing = new Vector2(Math.Max(0, advance - Font.Size * inter_digit_width), 0);
            }
        }
    }
}
