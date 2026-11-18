// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Scoring;
using osu.Game.Skinning;

namespace osu.Game.Screens.RankingV2.Legacy
{
    public abstract partial class LegacyRankingButton : CompositeDrawable, ISerialisableDrawable
    {
        public Action? Action { get; set; }

        private readonly string[] spriteNames;

        private Sprite buttonSprite = null!;
        private PoolableSkinnableSample sample = null!;

        [Resolved]
        protected IBindable<IScoreInfo> Score { get; private set; } = null!;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private SkinManager? skins { get; set; }

        [Resolved]
        private DefaultLegacySkin? defaultClassicSkin { get; set; }

        private ISkin? classicFallback => skins?.DefaultClassicSkin ?? defaultClassicSkin;

        protected LegacyRankingButton(params string[] spriteNames)
        {
            this.spriteNames = spriteNames;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AutoSizeAxes = Axes.Both;
            InternalChildren =
            [
                buttonSprite = new Sprite(),
                // https://github.com/peppy/osu-stable-reference/blob/6b3838c41c4b8005cadf7db0b503ae967776d9f9/osu!/GameModes/Ranking/Ranking.cs#L895
                // https://github.com/peppy/osu-stable-reference/blob/6b3838c41c4b8005cadf7db0b503ae967776d9f9/osu!/GameModes/Ranking/Ranking.cs#L923
                sample = new PoolableSkinnableSample(new SampleInfo(@"menuhit")),
            ];

            updateTexture();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            skin.SourceChanged += onSkinChanged;
            updateState();
            buttonSprite.FinishTransforms();
        }

        private void onSkinChanged() => Scheduler.AddOnce(updateTexture);

        private void updateTexture()
        {
            Texture? texture = null;

            foreach (string spriteName in spriteNames)
                texture ??= skin.GetStableTexture(spriteName);

            foreach (string spriteName in spriteNames)
                texture ??= classicFallback?.GetStableTexture(spriteName);

            buttonSprite.Texture = texture;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateState();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            updateState();
            base.OnHoverLost(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            Action?.Invoke();
            sample.Play();
            return true;
        }

        private void updateState()
        {
            // https://github.com/peppy/osu-stable-reference/blob/6b3838c41c4b8005cadf7db0b503ae967776d9f9/osu!/GameModes/Ranking/Ranking.cs#L841
            // https://github.com/peppy/osu-stable-reference/blob/6b3838c41c4b8005cadf7db0b503ae967776d9f9/osu!/GameModes/Ranking/Ranking.cs#L868
            buttonSprite.FadeTo(IsHovered ? 1f : 0.7f, 200);
        }

        #region ISerialisableDrawable

        public bool UsesFixedAnchor { get; set; }

        #endregion
    }

    public partial class LegacyRankingRetryButton : LegacyRankingButton
    {
        // https://github.com/peppy/osu-stable-reference/blob/6b3838c41c4b8005cadf7db0b503ae967776d9f9/osu!/GameModes/Ranking/Ranking.cs#L833
        public LegacyRankingRetryButton()
            : base(@"ranking-retry", @"pause-retry")
        {
        }
    }

    public partial class LegacyRankingWatchReplayButton : LegacyRankingButton
    {
        // https://github.com/peppy/osu-stable-reference/blob/6b3838c41c4b8005cadf7db0b503ae967776d9f9/osu!/GameModes/Ranking/Ranking.cs#L855-L860
        public LegacyRankingWatchReplayButton()
            : base(@"ranking-replay", @"pause-replay")
        {
        }
    }
}
