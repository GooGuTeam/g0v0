// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;

namespace osu.Game.Skinning.Select
{
    public partial class LegacyBackButton : CompositeDrawable
    {
        // Torii: the housing PR ships the legacy back button visual-only ("buttons
        // hooked up at a later stage"). We wire it so the legacy footer is actually
        // functional in song select. The child pieces handle the click sound/anim and
        // return unhandled, so the click bubbles up here to fire the action.
        public Action? Action { get; init; }

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            AutoSizeAxes = Axes.Both;

            rebuild();

            // long-lived hosts (the options' back button) outlive skin changes.
            skin.SourceChanged += onSkinChanged;
        }

        private void onSkinChanged() => Schedule(rebuild);

        private void rebuild()
        {
            bool old = skin.GetAnimation("menu-back", true, false) != null;

            if (old)
            {
                InternalChild = new LegacyOldBackButtonPiece
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                };
            }
            else
            {
                InternalChild = new LegacyNewBackButtonPiece
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                };
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;

            base.Dispose(isDisposing);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (Action == null)
                return false;

            Action.Invoke();
            return true;
        }
    }
}
