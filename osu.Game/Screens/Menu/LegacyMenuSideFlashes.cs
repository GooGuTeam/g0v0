// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
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
    /// Pulses with the music beat and alternates left/right during kiai.
    /// </summary>
    public partial class LegacyMenuSideFlashes : BeatSyncedContainer
    {
        private Sprite flashLeft = null!;
        private Sprite flashRight = null!;

        public LegacyMenuSideFlashes()
        {
            RelativeSizeAxes = Axes.Both;
            AlwaysPresent = true;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, SkinManager skins)
        {
            Texture? flashTex = skin.GetStableTexture(@"menu-flash") ?? skins.DefaultClassicSkin.GetStableTexture(@"menu-flash");

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
        }

        protected override void OnNewBeat(int beatIndex, TimingControlPoint timingPoint, EffectControlPoint effectPoint, ChannelAmplitudes amplitudes)
        {
            base.OnNewBeat(beatIndex, timingPoint, effectPoint, amplitudes);

            double beatLength = timingPoint.BeatLength;
            bool kiai = effectPoint.KiaiMode;

            float flashIntensity = 0.35f;

            if (!kiai || beatIndex % 2 == 0)
            {
                flashLeft.ClearTransforms();
                flashLeft.FadeTo(flashIntensity, 64)
                         .Then()
                         .FadeOut(beatLength, Easing.In);
            }

            if (!kiai || beatIndex % 2 == 1)
            {
                flashRight.ClearTransforms();
                flashRight.FadeTo(flashIntensity, 64)
                          .Then()
                          .FadeOut(beatLength, Easing.In);
            }
        }
    }
}
