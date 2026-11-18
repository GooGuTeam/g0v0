// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Configuration;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.RankingV2.Legacy
{
    /// <seealso href="https://github.com/peppy/osu-stable-reference/blob/6b3838c41c4b8005cadf7db0b503ae967776d9f9/osu!/GameModes/Ranking/Ranking.cs#L445-L451"/>
    public partial class LegacyRankingPerfectIndicator : CompositeDrawable, ISerialisableDrawable, IAnimatableSkinnable
    {
        public bool CanBeScaled(Axes axes) => axes == Axes.Both;

        private Sprite perfectIndicator = null!;

        [Resolved]
        private IBindable<IScoreInfo> score { get; set; } = null!;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private SkinManager? skins { get; set; }

        [Resolved]
        private DefaultLegacySkin? defaultClassicSkin { get; set; }

        private ISkin? classicFallback => skins?.DefaultClassicSkin ?? defaultClassicSkin;

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = perfectIndicator = new Sprite
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };

            updateTexture();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            skin.SourceChanged += onSkinChanged;
            score.BindValueChanged(_ => updateState(), true);
        }

        private void onSkinChanged() => Scheduler.AddOnce(updateTexture);

        private void updateTexture()
        {
            perfectIndicator.Size = Vector2.Zero;
            perfectIndicator.Texture = skin.GetStableTexture(@"ranking-perfect") ?? classicFallback?.GetStableTexture(@"ranking-perfect");
            Size = perfectIndicator.Size;
        }

        private void updateState()
        {
            perfectIndicator.Alpha = score.Value.IsPerfect() ? 1 : 0;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;
        }

        #region ISerialisableDrawable

        public bool UsesFixedAnchor { get; set; }

        #endregion

        #region IAnimatableSkinnable

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.AnimationSequence))]
        public BindableInt GroupNumber { get; } = new BindableInt(1)
        {
            MinValue = 0,
            MaxValue = 10,
        };

        int IAnimatableSkinnable.GroupNumber => GroupNumber.Value;

        public double StartAnimating(double startTime)
        {
            FinishAnimating();

            // https://github.com/peppy/osu-stable-reference/blob/6b3838c41c4b8005cadf7db0b503ae967776d9f9/osu!/GameModes/Ranking/Ranking.cs#L448
            perfectIndicator
                .ScaleTo(1.1f)
                .FadeOut();

            if (score.Value.IsPerfect())
            {
                using (BeginAbsoluteSequence(startTime))
                {
                    perfectIndicator.ScaleTo(1f, 300, Easing.Out)
                                    .FadeIn(300, Easing.Out);
                }
            }

            return perfectIndicator.LatestTransformEndTime;
        }

        public void FinishAnimating()
        {
            perfectIndicator.FinishTransforms();
        }

        #endregion
    }
}
