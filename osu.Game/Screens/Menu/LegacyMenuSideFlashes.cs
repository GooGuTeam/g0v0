// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics.Containers;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Menu
{
    /// <summary>
    /// Replicates osu!stable's side flashes ("menu-flash") from Menu.cs.
    /// Pulses on measure downbeats outside kiai and alternates left/right on every beat during kiai.
    /// </summary>
    public partial class LegacyMenuSideFlashes : BeatSyncedContainer
    {
        private Sprite flashLeft = null!;
        private Sprite flashRight = null!;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        private bool wasPaused;

        public LegacyMenuSideFlashes()
        {
            RelativeSizeAxes = Axes.Both;
            AlwaysPresent = true;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Texture? flashTex = getFlashTexture();

            Children = new Drawable[]
            {
                flashLeft = new Sprite
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Texture = flashTex,
                    Colour = Color4.White,
                    Blending = BlendingParameters.Additive,
                    Scale = new Vector2(-1f, 4f),
                    Alpha = 0,
                },
                flashRight = new Sprite
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Texture = flashTex,
                    Colour = Color4.White,
                    Blending = BlendingParameters.Additive,
                    Scale = new Vector2(1f, 4f),
                    Alpha = 0,
                }
            };

            skin.SourceChanged += onSkinChanged;
        }

        private Texture? getFlashTexture() => skin.GetStableTexture(@"menu-flash") ?? skins.DefaultClassicSkin.GetStableTexture(@"menu-flash");

        private void onSkinChanged() => Schedule(() =>
        {
            Texture? flashTex = getFlashTexture();
            flashLeft.Texture = flashTex;
            flashRight.Texture = flashTex;
        });

        protected override void Update()
        {
            base.Update();

            bool paused = BeatSyncSource.Clock?.IsRunning != true;

            if (paused && !wasPaused)
            {
                flashLeft.FadeOut(50);
                flashRight.FadeOut(50);
            }

            wasPaused = paused;
        }

        protected override void OnNewBeat(int beatIndex, TimingControlPoint timingPoint, EffectControlPoint effectPoint, ChannelAmplitudes amplitudes)
        {
            base.OnNewBeat(beatIndex, timingPoint, effectPoint, amplitudes);

            if (beatIndex < 0)
                return;

            bool kiai = effectPoint.KiaiMode;
            int numerator = Math.Max(1, timingPoint.TimeSignature.Numerator);

            // Stable Menu.cs:1126: AudioEngine.KiaiEnabled || (AudioEngine.SyncBeat % (int)AudioEngine.TimeSignature == 0 && AudioEngine.SyncBeat > 0)
            if (!kiai && (beatIndex == 0 || beatIndex % numerator != 0))
                return;

            const double fade_in_duration = 64;
            double decayDuration = Math.Max(300, timingPoint.BeatLength) - fade_in_duration;

            bool hasAmplitudes = amplitudes.LeftChannel > 0 || amplitudes.RightChannel > 0;
            float leftIntensity = hasAmplitudes
                ? Math.Clamp((amplitudes.LeftChannel * 65536f - 20000f) / 45536f * 0.5f, 0f, 1f)
                : 0.35f;
            float rightIntensity = hasAmplitudes
                ? Math.Clamp((amplitudes.RightChannel * 65536f - 20000f) / 45536f * 0.5f, 0f, 1f)
                : 0.35f;

            if (!kiai || beatIndex % 2 == 0)
            {
                flashLeft.ClearTransforms();
                flashLeft.FadeTo(leftIntensity, fade_in_duration)
                         .Then()
                         .FadeOut(decayDuration, Easing.In);
            }

            if (!kiai || beatIndex % 2 == 1)
            {
                flashRight.ClearTransforms();
                flashRight.FadeTo(rightIntensity, fade_in_duration)
                          .Then()
                          .FadeOut(decayDuration, Easing.In);
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;

            base.Dispose(isDisposing);
        }
    }
}
